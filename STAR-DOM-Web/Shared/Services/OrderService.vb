Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class OrderService

        Private ReadOnly _orders As New OrderRepository()
        Private ReadOnly _receipts As New ReceiptRepository()
        Private ReadOnly _cart As New CartService()
        Private ReadOnly _products As New ProductRepository()

        ''' <summary>
        ''' Raised inside the checkout transaction when a conditional stock decrement
        ''' matches no row. Rolling back is what we want here: a half-written order
        ''' (items inserted, stock taken, cart not cleared) is worse than no order.
        ''' </summary>
        Private Class StockShortageException
            Inherits Exception
            Public ReadOnly ProductName As String
            Public Sub New(productName As String)
                Me.New(productName, "Insufficient stock for '" & productName & "'.")
            End Sub
            Public Sub New(productName As String, message As String)
                MyBase.New(message)
                Me.ProductName = productName
            End Sub
        End Class

        ''' <summary>
        ''' Full checkout: validate stock, create order + items + payment/shipping records,
        ''' decrement stock, clear the cart. The order number is derived from the auto-increment
        ''' id so it can never collide under concurrent checkouts.
        ''' </summary>
        Public Function Checkout(paymentMethod As String, shippingAddress As String, contactPhone As String,
                                 Optional notes As String = "", Optional eventId As Integer? = Nothing,
                                 Optional orderStatus As String = "PENDING",
                                 Optional fulfillment As String = "DELIVERY",
                                 Optional pickupEventId As Integer? = Nothing) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")

            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then Return ServiceResult.Fail("Your cart is empty.")

            ' Orders are online-only: delivered via J&T Express, or claimed in person at
            ' a pop-up stall that is open now (or about to open). Card is not offered —
            ' the accepted e-payments are GCash and Maya.
            Dim isPickup As Boolean = String.Equals(fulfillment, "PICKUP", StringComparison.OrdinalIgnoreCase)
            Dim method As String = paymentMethod.Trim().ToUpperInvariant()
            If method <> "GCASH" AndAlso method <> "MAYA" AndAlso method <> "COD" Then
                Return ServiceResult.Fail("Please choose GCash, Maya, or Cash on Delivery.")
            End If

            Dim pickupEvent As PopUpEvent = Nothing
            If isPickup Then
                If Not pickupEventId.HasValue Then
                    Return ServiceResult.Fail("Please choose a pop-up stall for pick-up.")
                End If
                pickupEvent = New EventRepository().GetEvent(pickupEventId.Value)
                If pickupEvent Is Nothing OrElse
                   (pickupEvent.Status <> "NOW OPEN" AndAlso pickupEvent.Status <> "UPCOMING") Then
                    Return ServiceResult.Fail("That stall is not open for pick-up — please choose an active or upcoming stall.")
                End If
            End If

            ' Address of record: the stall itself for pick-ups (receipts + merchant view).
            Dim addressOnFile As String =
                If(isPickup, Trim(pickupEvent.Name & " · " & pickupEvent.LocationName), shippingAddress)

            Dim errors As New List(Of String)()
            If Not isPickup Then errors.Add(Validators.Required(shippingAddress, "Shipping address"))
            errors.Add(Validators.Phone(contactPhone))
            Dim clean As String() = errors.Where(Function(e) e IsNot Nothing).ToArray()
            If clean.Length > 0 Then
                Validators.Alert(clean, "Checkout incomplete")
                Return ServiceResult.Fail(clean(0))
            End If

            ' Fail fast on anything already known to be wrong, before opening a transaction.
            ' The stock half of this is only a courtesy: it produces a friendlier message
            ' than the in-transaction guard below, which is what actually enforces the limit.
            For Each it As CartItem In items
                If Not it.IsActive Then Return ServiceResult.Fail("'" & it.ProductName & "' is no longer available.")
                If it.StockQuantity < it.Quantity Then
                    Return ServiceResult.Fail("Insufficient stock for '" & it.ProductName & "' (available: " &
                                              it.StockQuantity.ToString() & ").")
                End If
            Next

            Dim subtotal As Decimal = items.Sum(Function(i) i.LineTotal)
            Dim bundleDiscount As Decimal = _cart.BundleDiscount(items)
            ' No shipping formula: the courier fee is not knowable at checkout. A delivery
            ' order is created with ShippingFee 0, ShippingFeeConfirmed FALSE, and a total
            ' that is still subtotal minus bundle savings. The merchant quotes the real J&T
            ' fee when confirming the order (ConfirmWithShippingFee), which is what makes
            ' the total final. Pick-up never carries a fee, so its total is final at once.
            Dim shippingFee As Decimal = 0D
            Dim total As Decimal = Math.Max(subtotal - bundleDiscount, 0D)
            Dim orderNumber As String = ""
            Dim orderId As Integer = 0

            ' The whole write path is one transaction: order header, order items, stock
            ' decrements, payment, shipping and the cart clear either all land or none do.
            ' The pre-check above is only a courtesy message — the authoritative guard is
            ' the conditional stock UPDATE inside the transaction, which is what actually
            ' prevents overselling when two shoppers race for the last unit.
            Try
                orderId = Db.InTransaction(Of Integer)(Function() As Integer
                    ' Reserve the auto-increment id first; the public number is derived from it so
                    ' two orders created at the same moment can never collide (COUNT(*)+1 could).
                    ' The reserved-id placeholder must fit OrderNumber VARCHAR(40); the real
                    ' SD-yyyyMMdd-nnnn number (16 chars) replaces it right after.
                    Dim oid As Integer = Db.ExecIdentity(
                        "INSERT INTO Orders (OrderNumber, UserId, EventId, Status, Subtotal, DiscountAmount, ShippingFee, " &
                        "TotalAmount, PaymentMethod, PaymentStatus, ShippingAddress, ContactPhone, Notes, Fulfillment, PickupEventId, CreatedAt, UpdatedAt) " &
                        "VALUES (@num, @u, @e, @st, @sub, @d, @sf, @tot, @pm, 'PENDING', @addr, @ph, @n, @ful, @pk, NOW(), NOW())",
                        Db.P("@num", "SD-TMP-" & Guid.NewGuid().ToString("N").Substring(0, 20)),
                        Db.P("@u", Session.CurrentUser.Id),
                        Db.P("@e", If(eventId.HasValue, CObj(eventId.Value), DBNull.Value)), Db.P("@st", orderStatus),
                        Db.P("@sub", subtotal), Db.P("@d", bundleDiscount), Db.P("@sf", shippingFee), Db.P("@tot", total),
                        Db.P("@pm", method), Db.P("@addr", addressOnFile),
                        Db.P("@ph", contactPhone), Db.P("@n", notes),
                        Db.P("@ful", If(isPickup, "PICKUP", "DELIVERY")),
                        Db.P("@pk", If(isPickup AndAlso pickupEventId.HasValue, CObj(pickupEventId.Value), DBNull.Value)))
                    orderNumber = "SD-" & Date.Now.ToString("yyyyMMdd") & "-" & oid.ToString("D4")
                    Db.Exec("UPDATE Orders SET OrderNumber = @num WHERE Id = @id",
                            Db.P("@num", orderNumber), Db.P("@id", oid))

                    ' Items + stock decrement
                    For Each it As CartItem In items
                        Db.Exec(
                            "INSERT INTO OrderItems (OrderId, ProductId, VariantId, Quantity, UnitPrice, LineTotal) " &
                            "VALUES (@o, @p, @v, @q, @u, @t)",
                            Db.P("@o", oid), Db.P("@p", it.ProductId),
                            Db.P("@v", If(it.VariantId.HasValue, CObj(it.VariantId.Value), DBNull.Value)),
                            Db.P("@q", it.Quantity), Db.P("@u", it.UnitPrice), Db.P("@t", it.LineTotal))

                        ' WHERE StockQuantity >= @q makes the decrement conditional. If a
                        ' concurrent order took the last unit first this matches 0 rows, we
                        ' throw, and the transaction rolls the whole checkout back.
                        Dim moved As Integer = Db.Exec(
                            "UPDATE Products SET StockQuantity = StockQuantity - @q, SoldCount = SoldCount + @q " &
                            "WHERE Id = @p AND StockQuantity >= @q",
                            Db.P("@q", it.Quantity), Db.P("@p", it.ProductId))
                        If moved = 0 Then Throw New StockShortageException(it.ProductName)
                    Next

                    ' Payment + shipping records
                    Db.Exec(
                        "INSERT INTO Payments (OrderId, PaymentMethod, Amount, ReferenceNumber, Status, CreatedAt) " &
                        "VALUES (@o, @m, @a, @r, @s, NOW())",
                        Db.P("@o", oid), Db.P("@m", paymentMethod), Db.P("@a", total),
                        Db.P("@r", ""), Db.P("@s", "PENDING"))
                    ' A pick-up order has no parcel and no Shipping row — the claim is
                    ' tracked by the two-side confirm flags on the order itself.
                    If Not isPickup Then
                        Db.Exec(
                            "INSERT INTO Shipping (OrderId, Courier, TrackingNumber, Status, Address) VALUES (@o, @c, @t, @s, @a)",
                            Db.P("@o", oid), Db.P("@c", "J&T Express"), Db.P("@t", ""), Db.P("@s", "PENDING"),
                            Db.P("@a", addressOnFile))
                    End If

                    ' Clear cart (PostgreSQL: no "DELETE alias FROM ... JOIN")
                    Db.Exec("DELETE FROM CartItems WHERE CartId IN (SELECT Id FROM Cart WHERE UserId = @u)",
                            Db.P("@u", Session.CurrentUser.Id))

                    Return oid
                End Function)
            Catch stockErr As StockShortageException
                Return ServiceResult.Fail(stockErr.Message)
            Catch ex As Exception
                Db.LogError("Checkout", ex)
                Return ServiceResult.Fail("Checkout failed: " & ex.Message)
            End Try

            ' Receipt and notifications run AFTER the commit on purpose: they are
            ' best-effort side effects, and a failure in either must never be able to
            ' roll back an order the customer has already successfully placed.
            ' Cash on Delivery warrants an official receipt at checkout, even before
            ' the cash is collected.
            Dim placed As Order = _orders.GetById(orderId)
            If paymentMethod.Trim().ToUpperInvariant() = "COD" AndAlso placed IsNot Nothing Then
                IssueReceiptFor(placed)
            End If

            Dim notif As New NotificationService()
            notif.Notify(Session.CurrentUser.Id, "Order placed – " & orderNumber,
                         "Your order of " & Fmt.PHP(total) & " via " & method & " has been received. " &
                         If(isPickup,
                             "We'll prepare it for pick-up at " & pickupEvent.Name & ".",
                             "We'll confirm the J&T shipping fee and send you the final total before it ships. Track it in My Orders."),
                         "ORDER", "my-orders")
            notif.NotifyRole("ADMIN", "New order – " & orderNumber,
                             Fmt.PHP(total) & " – " & items.Count.ToString() & " item(s) ready for processing" &
                             If(isPickup, ".", ". Quote the J&T shipping fee to confirm it."),
                             "ORDER", "merchant-orders")

            Return ServiceResult.Ok("Order " & orderNumber & " placed!", orderNumber)
        End Function

        ''' <summary>
        ''' Payment confirmation is password-gated for whoever records it — the buyer
        ''' confirming their own e-wallet payment, or a merchant/admin recording one.
        ''' GCash/Maya (the only accepted e-payments) must carry the reference number
        ''' from the e-wallet receipt; COD / pay-on-pick-up need only the password.
        ''' </summary>
        Public Function ConfirmPayment(orderNumber As String, reference As String, password As String) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            Dim order As Order = _orders.GetByNumber(orderNumber)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.UserId <> Session.CurrentUser.Id AndAlso Not Session.CanManageStore Then
                Return ServiceResult.Fail("You don't have access to this order.")
            End If
            If order.Status = "CANCELLED" Then
                Return ServiceResult.Fail("This order was cancelled and cannot be paid.")
            End If
            If order.PaymentStatus = "PAID" Then Return ServiceResult.Ok("Payment already recorded.")

            ' Password re-entry gate. Verified against a fresh DB read (not the session
            ' copy) so a password changed mid-session is honoured immediately.
            If String.IsNullOrEmpty(password) Then
                Return ServiceResult.Fail("Enter your password to confirm the payment.")
            End If
            Dim freshUser As User = New UserRepository().GetById(Session.CurrentUser.Id)
            If freshUser Is Nothing OrElse Not PasswordHasher.Verify(password, freshUser.PasswordHash) Then
                Return ServiceResult.Fail("Password incorrect — payment was not confirmed.")
            End If

            Dim method As String = order.PaymentMethod.Trim().ToUpperInvariant()
            Dim ref As String = If(reference, "").Trim()
            If method = "GCASH" OrElse method = "MAYA" Then
                If ref = "" Then
                    Return ServiceResult.Fail("Enter the " & If(method = "GCASH", "GCash", "Maya") &
                                              " reference number from your payment receipt.")
                End If
            End If
            If ref = "" Then ref = "REF-" & Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant()
            Try
                Db.InTransaction(Of Boolean)(Function() As Boolean
                    Db.Exec(
                        "UPDATE Payments SET Status = 'PAID', ReferenceNumber = @r, PaidAt = NOW(), GatewayResponse = @g WHERE OrderId = @o",
                        Db.P("@r", ref), Db.P("@g", "SIMULATED_OK::" & Date.Now.ToString("yyyyMMddHHmmss")), Db.P("@o", order.Id))
                    Db.Exec(
                        "UPDATE Orders SET PaymentStatus = 'PAID', Status = CASE WHEN Status = 'PENDING' THEN 'CONFIRMED' ELSE Status END, UpdatedAt = NOW() WHERE Id = @o",
                        Db.P("@o", order.Id))
                    Return True
                End Function)
            Catch ex As Exception
                Db.LogError("ConfirmPayment", ex)
                Return ServiceResult.Fail("Payment update failed: " & ex.Message)
            End Try

            IssueReceiptFor(order)

            Dim notif As New NotificationService()
            notif.Notify(order.UserId, "Payment received – " & orderNumber,
                         "Your " & order.PaymentMethod & " payment of " & Fmt.PHP(order.TotalAmount) &
                         " (ref " & ref & ") was confirmed.",
                         "ORDER", "my-orders")
            Return ServiceResult.Ok("Payment confirmed (ref " & ref & ").")
        End Function

        ''' <summary>
        ''' Pick-up orders are only closed when BOTH sides confirm the claim: the
        ''' customer taps "order received" and the merchant/stall team taps "handed
        ''' over". When the second confirmation lands, the order becomes DELIVERED
        ''' (and PAID, for pay-on-claim methods) with an official receipt.
        ''' </summary>
        Public Function ConfirmPickup(orderId As Integer, customerSide As Boolean) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If Not order.IsPickup Then Return ServiceResult.Fail("This order is not a pick-up order.")
            If order.Status = "CANCELLED" Then Return ServiceResult.Fail("This order was cancelled.")

            If customerSide Then
                If order.UserId <> Session.CurrentUser.Id Then Return ServiceResult.Fail("You don't have access to this order.")
                If order.PickupCustomerConfirmed Then Return ServiceResult.Ok("You already confirmed this pick-up.")
                _orders.SetPickupConfirm(orderId, True, True)
            Else
                If Not Session.CanManageStore Then Return ServiceResult.Fail("Only the store team can do this.")
                If order.PickupMerchantConfirmed Then Return ServiceResult.Ok("Hand-over already confirmed.")
                _orders.SetPickupConfirm(orderId, False, True)
            End If

            Dim fresh As Order = _orders.GetById(orderId)
            Dim notif As New NotificationService()
            If fresh.PickupCustomerConfirmed AndAlso fresh.PickupMerchantConfirmed Then
                _orders.UpdateStatus(orderId, "DELIVERED")
                Dim wasPaid As Boolean = fresh.PaymentStatus = "PAID"
                _orders.UpdatePaymentStatus(orderId, "PAID")
                If Not wasPaid Then IssueReceiptFor(fresh)
                notif.Notify(fresh.UserId, "Pick-up complete – " & fresh.OrderNumber,
                             "Both sides confirmed the claim — order delivered successfully. Enjoy your art!",
                             "ORDER", "my-orders")
                Return ServiceResult.Ok("Pick-up confirmed by both sides — order delivered!")
            End If

            If customerSide Then
                notif.NotifyRole("ADMIN", "Customer confirmed pick-up – " & fresh.OrderNumber,
                                 "The customer has the order. Waiting for the stall team to confirm hand-over.",
                                 "ORDER", "merchant-orders")
                Return ServiceResult.Ok("Thanks! Waiting for the stall team to confirm hand-over.")
            End If
            notif.Notify(fresh.UserId, "Your order is ready for claim – " & fresh.OrderNumber,
                         "The stall confirmed the hand-over. Tap 'Confirm order received' in My Orders once you have it.",
                         "ORDER", "my-orders")
            Return ServiceResult.Ok("Hand-over confirmed — waiting for the customer to confirm.")
        End Function

        Public Function ListMyOrders(Optional page As Integer = 1, Optional pageSize As Integer = 0) As List(Of Order)
            If Not Session.IsAuthenticated Then Return New List(Of Order)()
            Return _orders.ListByUser(Session.CurrentUser.Id, "", page, pageSize)
        End Function

        Public Function CountMyOrders() As Integer
            If Not Session.IsAuthenticated Then Return 0
            Return _orders.CountByUser(Session.CurrentUser.Id)
        End Function

        Public Function GetOrder(id As Integer) As Order
            Return _orders.GetById(id)
        End Function

        Public Function GetOrderByNumber(orderNumber As String) As Order
            If String.IsNullOrWhiteSpace(orderNumber) Then Return Nothing
            Return _orders.GetByNumber(orderNumber.Trim())
        End Function

        Public Function GetOrderItems(orderId As Integer) As List(Of OrderItem)
            Return _orders.GetItems(orderId)
        End Function

        Public Function AllOrders(Optional search As String = "", Optional status As String = "", Optional page As Integer = 1, Optional pageSize As Integer = 0) As List(Of Order)
            Return _orders.ListAll(search, status, page, pageSize)
        End Function

        Public Function CountAllOrders(Optional search As String = "", Optional status As String = "") As Integer
            Return _orders.CountAll(search, status)
        End Function

        ''' <summary>
        ''' Confirm a delivery order by quoting the courier fee the customer will be
        ''' charged. Entering the fee IS the confirmation: it sets Status = CONFIRMED and
        ''' finalises TotalAmount in a single statement, so the order can never sit in
        ''' CONFIRMED while showing a total that still has no shipping on it.
        ''' </summary>
        Public Function ConfirmWithShippingFee(orderId As Integer, feeText As String, password As String) As ServiceResult
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Only the store team can do this.")
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.IsPickup Then Return ServiceResult.Fail("Pick-up orders carry no shipping fee and are confirmed as usual.")
            If order.Status = "CANCELLED" Then Return ServiceResult.Fail("This order was cancelled.")
            ' PROCESSING is refused alongside SHIPPED/DELIVERED on purpose. Confirming
            ' writes Status = CONFIRMED, so letting an in-flight order back in would
            ' drag it a step backwards in the pipeline. Re-quoting a CONFIRMED order is
            ' still allowed — that is how a mis-typed fee gets corrected.
            If order.Status = "PROCESSING" OrElse order.Status = "SHIPPED" OrElse order.Status = "DELIVERED" Then
                Return ServiceResult.Fail("This order is already on its way — the shipping fee can no longer be changed.")
            End If

            ' Parsed with InvariantCulture so a comma decimal separator cannot turn a
            ' fee into something wildly larger (or negative) than intended.
            Dim fee As Decimal = 0D
            Dim rawFee As String = If(feeText, "").Trim()
            If rawFee = "" Then rawFee = "0"
            If Not Decimal.TryParse(rawFee, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, fee) Then
                Return ServiceResult.Fail("Enter the shipping fee as a number, e.g. 145.")
            End If
            If fee < 0D OrElse fee > 10000D Then
                Return ServiceResult.Fail("Shipping fee must be between ₱0 and ₱10,000.")
            End If

            ' Same password re-entry gate as ConfirmPayment, so a left-open merchant
            ' session cannot quietly commit a customer's total.
            Dim pwd As String = If(password, "").Trim()
            If pwd = "" Then Return ServiceResult.Fail("Enter your password to confirm the order.")
            Dim freshUser As User = New UserRepository().GetById(Session.CurrentUser.Id)
            If freshUser Is Nothing OrElse Not PasswordHasher.Verify(pwd, freshUser.PasswordHash) Then
                Return ServiceResult.Fail("Password incorrect — the order was not confirmed.")
            End If

            Try
                Db.InTransaction(Of Boolean)(Function() As Boolean
                    _orders.ConfirmWithShippingFee(orderId, Math.Round(fee, 2), Session.CurrentUser.Id)
                    Return True
                End Function)
            Catch ex As Exception
                Db.LogError("ConfirmWithShippingFee", ex)
                Return ServiceResult.Fail("Could not save the shipping fee: " & ex.Message)
            End Try

            ' Re-read: the transaction is the authority on the new total, and the
            ' receipt and payment row both have to be brought in line with it.
            Dim confirmed As Order = _orders.GetById(orderId)
            If confirmed Is Nothing Then Return ServiceResult.Fail("Order not found.")
            _orders.UpdatePaymentAmountForOrder(confirmed.Id, confirmed.TotalAmount)
            _receipts.RefreshTotals(confirmed)

            Dim notif As New NotificationService()
            notif.Notify(confirmed.UserId, "Order confirmed – " & confirmed.OrderNumber,
                         "Shipping is " & Fmt.PHP(confirmed.ShippingFee) & " via J&T Express. " &
                         "Your final total is " & Fmt.PHP(confirmed.TotalAmount) & " — we'll let you know when it ships.",
                         "ORDER", "my-orders")

            Return ServiceResult.Ok("Order confirmed — final total " & Fmt.PHP(confirmed.TotalAmount) &
                                    " (includes " & Fmt.PHP(confirmed.ShippingFee) & " shipping).")
        End Function

        ''' <summary>Merchant updates fulfilment state, keeping the payment status consistent.</summary>
        Public Function UpdateOrderState(orderId As Integer, newStatus As String, Optional tracking As String = "") As ServiceResult
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")

            ' Pick-up orders never ride a courier: they close when both sides confirm
            ' the claim (ConfirmPickup), not through the ship/deliver path.
            If order.IsPickup AndAlso (newStatus = "SHIPPED" OrElse newStatus = "DELIVERED") Then
                Return ServiceResult.Fail("Pick-up orders are completed by the two-sided claim confirmation, not by shipping.")
            End If

            ' A delivery order cannot be confirmed on a bare status change: the courier
            ' fee is what the customer is quoted, so it has to be entered through
            ' ConfirmWithShippingFee. A zero fee is still a decision worth recording, so
            ' this gate is on ShippingFeeConfirmed rather than on the fee being non-zero.
            If newStatus = "CONFIRMED" AndAlso Not order.IsPickup AndAlso Not order.ShippingFeeConfirmed Then
                Return ServiceResult.Fail("Enter the J&T shipping fee to confirm this order — the customer is quoted the final total at that point.")
            End If

            Dim wasCancelled As Boolean = order.Status = "CANCELLED"

            ' Validate state machine loosely (same-state is allowed for re-saves)
            _orders.UpdateStatus(orderId, newStatus)

            If newStatus = "SHIPPED" OrElse newStatus = "DELIVERED" Then
                _orders.UpdateShipping(orderId, "J&T Express", tracking, newStatus)
            End If
            If newStatus = "DELIVERED" Then
                Dim wasPaid As Boolean = order.PaymentStatus = "PAID"
                _orders.UpdatePaymentStatus(orderId, "PAID")
                If Not wasPaid Then IssueReceiptFor(order)
            End If
            If newStatus = "CANCELLED" Then
                _orders.UpdatePaymentStatus(orderId, "REFUNDED")
                If Not wasCancelled Then _orders.RestoreOrderStock(orderId)
            End If

            Dim notif As New NotificationService()
            notif.Notify(order.UserId, "Order " & newStatus.Replace("_", " ") & " – " & order.OrderNumber,
                         "Your order is now: " & newStatus.Replace("_", " ") & ". " &
                         If(tracking.Length > 0, "Tracking: " & tracking, ""), "ORDER", "my-orders")
            Return ServiceResult.Ok("Order updated to " & newStatus & ".")
        End Function

        ' ----- Reviews ----------------------------------------------------------

        Public Function SubmitReview(productId As Integer, orderId As Integer?, rating As Integer, comment As String) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If rating < 1 OrElse rating > 5 Then Return ServiceResult.Fail("Rating must be 1–5 stars.")
            If _orders.UserReviewedProduct(Session.CurrentUser.Id, productId) Then
                Return ServiceResult.Fail("You already reviewed this product.")
            End If
            ' Reviews are only allowed for verified purchases
            Dim purchaseId As Integer? = _orders.PurchaseOrderId(Session.CurrentUser.Id, productId)
            If Not purchaseId.HasValue Then
                Return ServiceResult.Fail("Only verified purchasers can review this product.")
            End If
            _orders.AddReview(Session.CurrentUser.Id, productId, If(orderId.HasValue, orderId, purchaseId), rating, comment)
            Return ServiceResult.Ok("Thank you for your review!")
        End Function

        Public Function ReviewsForProduct(productId As Integer) As List(Of Review)
            Return _orders.ListForProduct(productId)
        End Function

        Public Function HasReviewed(productId As Integer) As Boolean
            If Not Session.IsAuthenticated Then Return False
            Return _orders.UserReviewedProduct(Session.CurrentUser.Id, productId)
        End Function

        Public Function ListAllReviews(Optional search As String = "") As List(Of Review)
            Return _orders.ListAllReviews(search)
        End Function

        Public Sub SetReviewApproved(reviewId As Integer, approved As Boolean)
            _orders.SetReviewApproved(reviewId, approved)
        End Sub

        ''' <summary>Whether the current user bought this product (non-cancelled order).</summary>
        Public Function CanReview(productId As Integer) As Boolean
            If Not Session.IsAuthenticated Then Return False
            Return _orders.PurchaseOrderId(Session.CurrentUser.Id, productId).HasValue
        End Function

        Public Function PurchaseOrderId(productId As Integer) As Integer?
            If Not Session.IsAuthenticated Then Return Nothing
            Return _orders.PurchaseOrderId(Session.CurrentUser.Id, productId)
        End Function

        Public Sub DeleteReview(reviewId As Integer)
            _orders.DeleteReview(reviewId)
        End Sub

        Public Function ListPayments(Optional search As String = "", Optional limit As Integer = 0) As List(Of Payment)
            Return _orders.ListPayments(search, limit)
        End Function

        Public Function ReceiptForPaymentId(paymentId As Integer) As Receipt
            Return _receipts.GetByPaymentId(paymentId)
        End Function

        Public Function ReceiptsForPaymentIds(paymentIds As List(Of Integer)) As Dictionary(Of Integer, Receipt)
            Return _receipts.GetByPaymentIds(paymentIds)
        End Function

        Public Function ReceiptsForOrder(orderId As Integer) As List(Of Receipt)
            Return _receipts.GetByOrderId(orderId)
        End Function

        Public Function PaymentsForOrder(orderId As Integer) As List(Of Payment)
            Return _orders.ListPaymentsByOrder(orderId)
        End Function

        Public Function ReceiptByNumber(receiptNumber As String) As Receipt
            Return _receipts.GetByNumber(receiptNumber)
        End Function

        ''' <summary>Merchant-side: force-issue a receipt for a payment. Prepaid payments need PAID; COD warrants one even unpaid.</summary>
        Public Function IssueReceiptByPaymentId(paymentId As Integer) As Receipt
            Dim p As Payment = _orders.GetPaymentById(paymentId)
            If p Is Nothing Then Return Nothing
            If p.Status <> "PAID" AndAlso p.PaymentMethod.Trim().ToUpperInvariant() <> "COD" Then Return Nothing
            Dim o As Order = _orders.GetById(p.OrderId)
            If o Is Nothing Then Return Nothing
            Dim items As List(Of OrderItem) = _orders.GetItems(o.Id)
            Dim paidAt As Date? = If(p.Status = "PAID", p.PaidAt, Nothing)
            Return _receipts.Issue(o, items, p.Id, p.PaymentMethod, paidAt)
        End Function

        ''' <summary>Issue an official receipt whenever a payment becomes PAID (prepaid or COD on delivery).</summary>
        Private Sub IssueReceiptFor(order As Order)
            If order Is Nothing Then Return
            Dim pays As List(Of Payment) = _orders.ListPaymentsByOrder(order.Id)
            If pays.Count = 0 Then Return
            Dim paid As Payment = pays.FirstOrDefault(Function(p) p.Status = "PAID")
            If paid Is Nothing Then paid = pays(0)
            Dim items As List(Of OrderItem) = _orders.GetItems(order.Id)
            _receipts.Issue(order, items, paid.Id, paid.PaymentMethod, paid.PaidAt)
        End Sub

    End Class

End Namespace