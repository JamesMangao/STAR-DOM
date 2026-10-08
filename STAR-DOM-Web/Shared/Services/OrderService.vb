Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class OrderService

        ''' <summary>
        ''' Shown whenever a payment cannot be confirmed. The store never guesses at a
        ''' customer's money: a failed confirmation leaves the order exactly where it
        ''' was and hands the customer a human to talk to, which is the only honest
        ''' answer available when a reference number does not check out.
        ''' </summary>
        Public Shared Function PaymentFailedMessage(detail As String) As String
            Return PaymentSetting.PaymentFailedMessage(detail)
        End Function

        Private ReadOnly _orders As New OrderRepository()
        Private ReadOnly _paySettings As New PaymentSettingRepository()
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
                                 Optional orderStatus As String = "PENDING") As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")

            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then Return ServiceResult.Fail("Your cart is empty.")

            ' Orders are online-only: delivered nationwide via J&T Express. Card is not
            ' offered — the accepted e-payments are GCash and GOtyme.
            '
            ' "PENDING" is the parked method for the quote-first flow: the customer
            ' places the order before choosing a channel, and picks GCash / GOtyme
            ' on the order page once the studio has returned the final total.
            ' Orders.PaymentMethod is NOT NULL, so the column needs a value now.
            ' Cash on Delivery is gone: every parcel is booked with J&T and paid
            ' for up front against the final total.
            Dim method As String = If(paymentMethod, "").Trim().ToUpperInvariant()
            If method = PaymentSettingRepository.LegacyMaya Then method = PaymentSettingRepository.Gotyme
            If method = "" Then method = "PENDING"
            If method <> "PENDING" AndAlso
               method <> "GCASH" AndAlso method <> "GOTYME" Then
                Return ServiceResult.Fail("Please choose GCash or GOtyme.")
            End If


            Dim addressOnFile As String = shippingAddress

            Dim errors As New List(Of String)()
            errors.Add(Validators.Required(shippingAddress, "Shipping address"))
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
            ' the total final.
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
                        "TotalAmount, PaymentMethod, PaymentStatus, ShippingAddress, ContactPhone, Notes, CreatedAt, UpdatedAt) " &
                        "VALUES (@num, @u, @e, @st, @sub, @d, @sf, @tot, @pm, 'PENDING', @addr, @ph, @n, NOW(), NOW())",
                        Db.P("@num", "SD-TMP-" & Guid.NewGuid().ToString("N").Substring(0, 20)),
                        Db.P("@u", Session.CurrentUser.Id),
                        Db.P("@e", If(eventId.HasValue, CObj(eventId.Value), DBNull.Value)), Db.P("@st", orderStatus),
                        Db.P("@sub", subtotal), Db.P("@d", bundleDiscount), Db.P("@sf", shippingFee), Db.P("@tot", total),
                        Db.P("@pm", method), Db.P("@addr", addressOnFile),
                        Db.P("@ph", contactPhone), Db.P("@n", notes))
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
                    Db.Exec(
                        "INSERT INTO Shipping (OrderId, Courier, TrackingNumber, Status, Address) VALUES (@o, @c, @t, @s, @a)",
                        Db.P("@o", oid), Db.P("@c", "J&T Express"), Db.P("@t", ""), Db.P("@s", "PENDING"),
                        Db.P("@a", addressOnFile))

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
            ' No receipt is issued here: nothing is paid for yet. The official
            ' receipt is issued when the payment is confirmed.
            Dim notif As New NotificationService()
            notif.Notify(Session.CurrentUser.Id, "Order placed – " & orderNumber,
                         "Your order of " & Fmt.PHP(total) & " via " & method & " has been received. " &
                         "We'll confirm the J&T shipping fee and send you the final total before it ships. Track it in My Orders.",
                         "ORDER", "my-orders")
            notif.NotifyRole("ADMIN", "New order – " & orderNumber,
                             Fmt.PHP(total) & " – " & items.Count.ToString() & " item(s) ready for processing" &
                             ". Quote the J&T shipping fee to confirm it.",
                             "ORDER", "merchant-orders")

            Return ServiceResult.Ok("Order " & orderNumber & " placed!", orderNumber)
        End Function

        ''' <summary>
        ''' Records the channel the customer picked at payment time. Checkout parks the
        ''' method as "PENDING" because the studio quotes the order first, so this is
        ''' where the real channel is chosen.
        ''' </summary>
        Public Function ChoosePaymentMethod(orderId As Integer, method As String) As ServiceResult
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.UserId <> Session.CurrentUser.Id AndAlso Not Session.CanManageStore Then
                Return ServiceResult.Fail("You don't have access to this order.")
            End If
            If order.Status = "CANCELLED" Then Return ServiceResult.Fail("This order was cancelled.")
            If order.ShippingFeeConfirmed = False Then
                Return ServiceResult.Fail("Your order is still being priced. You can choose a payment method " &
                                          "once the studio returns the final total and shipping fee.")
            End If
            Dim pick As String = If(method, "").Trim().ToUpperInvariant()
            If pick <> "GCASH" AndAlso pick <> "GOTYME" Then
                Return ServiceResult.Fail("Choose GCash or GOtyme.")
            End If
            If Not _paySettings.IsChannelEnabled(pick) Then
                Return ServiceResult.Fail("That payment method is not available right now.")
            End If
            _orders.SetPaymentMethod(orderId, pick)
            Return ServiceResult.Ok("Payment method saved.")
        End Function

        ''' <summary>
        ''' Clears the chosen channel back to PENDING so the customer can pick the
        ''' other wallet. Only legal while nothing has been submitted against it —
        ''' once a reference is in the studio's hands there is nothing to re-choose.
        ''' </summary>
        Public Function ResetPaymentMethod(orderId As Integer) As ServiceResult
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.UserId <> Session.CurrentUser.Id AndAlso Not Session.CanManageStore Then
                Return ServiceResult.Fail("You don't have access to this order.")
            End If
            If order.Status = "CANCELLED" Then Return ServiceResult.Fail("This order was cancelled.")
            If order.PaymentStatus = "PAID" OrElse order.PaymentStatus = "SUBMITTED" Then
                Return ServiceResult.Fail("This payment can no longer be changed here.")
            End If
            _orders.SetPaymentMethod(orderId, "PENDING")
            Return ServiceResult.Ok("Choose a payment method to continue.")
        End Function

        ''' <summary>
        ''' Verifies a re-entered password against a fresh hash read, not the copy in
        ''' session, so a password changed mid-session is honoured immediately. Used by
        ''' every step that touches money: submitting, confirming and declining.
        ''' </summary>
        Private Function PasswordOk(password As String) As Boolean
            If String.IsNullOrEmpty(password) Then Return False
            Dim freshUser As User = New UserRepository().GetById(Session.CurrentUser.Id)
            Return freshUser IsNot Nothing AndAlso PasswordHasher.Verify(password, freshUser.PasswordHash)
        End Function

        ''' <summary>
        ''' Step 3 of the payment flow - the customer's side. The buyer pays GCash or
        ''' GOtyme outside the app and hands over the reference number from the receipt
        ''' plus their password. This does NOT make the order paid: it parks
        ''' PaymentStatus at SUBMITTED so the studio can check the transfer against its
        ''' own records first. No receipt is issued, PROCESSING cannot start, and J&amp;T
        ''' stays shut until the studio confirms below.
        ''' </summary>
        Public Function SubmitPayment(orderNumber As String, reference As String, password As String) As ServiceResult
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
            If order.PaymentStatus = "SUBMITTED" Then
                Return ServiceResult.Ok("Payment already submitted - waiting for the studio to confirm it.")
            End If

            ' Payment is only possible after the studio has returned the order with
            ' the final price and the shipping fee. Until ConfirmWithShippingFee
            ' runs, ShippingFee is 0 and TotalAmount is not what the customer owes,
            ' so taking money now would collect the wrong figure.
            If Not order.ShippingFeeConfirmed Then
                Return ServiceResult.Fail("Your order is still being priced. You can pay once the studio " &
                                          "returns it with the final total and shipping fee.")
            End If

            ' A declined payment (FAILED) falls through on purpose: the customer is
            ' handed the support line first, then gets to resubmit a corrected reference.

            ' Password re-entry gate. Every rejection past this point is a failed
            ' payment, so it carries the contact line: the customer needs a human,
            ' not a silent red flash that disappears on the next page load.
            If String.IsNullOrEmpty(password) Then
                Return ServiceResult.Fail(PaymentFailedMessage("Enter your password to confirm the payment."))
            End If
            If Not PasswordOk(password) Then
                Return ServiceResult.Fail(PaymentFailedMessage("Password incorrect - payment was not confirmed."))
            End If

            Dim method As String = If(order.PaymentMethod, "").Trim().ToUpperInvariant()
            Dim ref As String = If(reference, "").Trim()
            ' GCash and GOtyme are the only channels, and both hand out a transaction
            ' number, so the reference is required rather than auto-generated: a payment
            ' we cannot trace back to a receipt is not one we should ever confirm.
            If ref = "" Then
                Return ServiceResult.Fail(PaymentFailedMessage(
                    "Enter the " & PaymentSetting.DisplayName(method) & " reference number from your payment receipt."))
            End If
            Try
                Db.InTransaction(Of Boolean)(Function() As Boolean
                    Db.Exec(
                        "UPDATE Payments SET Status = 'SUBMITTED', ReferenceNumber = @r, PaymentMethod = @m, " &
                        "GatewayResponse = @g WHERE OrderId = @o",
                        Db.P("@r", ref), Db.P("@m", method), Db.P("@g", "CUSTOMER_SUBMITTED::" & Date.Now.ToString("yyyyMMddHHmmss")),
                        Db.P("@o", order.Id))
                    Db.Exec(
                        "UPDATE Orders SET PaymentStatus = 'SUBMITTED', UpdatedAt = NOW() WHERE Id = @o",
                        Db.P("@o", order.Id))
                    Return True
                End Function)
            Catch ex As Exception
                Db.LogError("SubmitPayment", ex)
                Return ServiceResult.Fail(PaymentFailedMessage("Payment update failed: " & ex.Message))
            End Try

            Dim notif As New NotificationService()
            notif.NotifyRole("ADMIN", "Payment submitted - " & orderNumber,
                             "Reference " & ref & " (" & PaymentSetting.DisplayName(method) & ") of " &
                             Fmt.PHP(order.TotalAmount) & " is waiting for verification. Confirm or decline it " &
                             "on Orders & Payments before the order moves on.",
                             "ORDER", "merchant-orders")
            notif.Notify(order.UserId, "Payment submitted - " & orderNumber,
                         "We've got your " & PaymentSetting.DisplayName(method) & " reference " & ref &
                         ". The studio will confirm it before the order ships.",
                         "ORDER", "my-orders")
            Return ServiceResult.Ok("Payment submitted (ref " & ref & "). The studio will confirm it before the order ships.")
        End Function

        ''' <summary>
        ''' Step 4 (confirm side) - the studio checks the submitted reference against its
        ''' own records, then and only then marks the money as received. This is the ONLY
        ''' path to PAID: the customer's submission stops at SUBMITTED, so an unverified
        ''' transfer can never book J&amp;T, start PROCESSING or produce a receipt.
        ''' </summary>
        Public Function ConfirmPayment(orderId As Integer, password As String) As ServiceResult
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Only the store team can confirm a payment.")
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.Status = "CANCELLED" Then Return ServiceResult.Fail("This order was cancelled.")
            If order.PaymentStatus = "PAID" Then Return ServiceResult.Ok("Payment already confirmed.")
            If order.PaymentStatus = "REFUNDED" Then Return ServiceResult.Fail("This payment was already refunded.")
            If order.PaymentStatus <> "SUBMITTED" Then
                Return ServiceResult.Fail("There is no submitted payment waiting to be confirmed.")
            End If
            If String.IsNullOrEmpty(password) Then
                Return ServiceResult.Fail("Enter your password to confirm this payment.")
            End If
            If Not PasswordOk(password) Then
                Return ServiceResult.Fail("Password incorrect - payment was not confirmed.")
            End If

            ' The reference comes from what the customer submitted, not from a box on
            ' this form: confirming means checking THAT number, and there is nothing
            ' to confirm if no reference was submitted at all.
            Dim submitted As Payment = _orders.ListPaymentsByOrder(orderId).FirstOrDefault(
                Function(p) p.Status = "SUBMITTED")
            If submitted Is Nothing OrElse String.IsNullOrWhiteSpace(submitted.ReferenceNumber) Then
                Return ServiceResult.Fail(
                    "No reference number was submitted, so this payment cannot be verified.")
            End If
            Dim ref As String = submitted.ReferenceNumber.Trim()

            Try
                Db.InTransaction(Of Boolean)(Function() As Boolean
                    _orders.UpdatePaymentStatusById(submitted.Id, "PAID", ref)
                    ' Money confirmed means fulfilment starts: a PENDING/CONFIRMED order
                    ' moves straight to PROCESSING, so the studio never has to press
                    ' Advance after verifying a payment. Anything further along stays put.
                    Db.Exec(
                        "UPDATE Orders SET PaymentStatus = 'PAID', " &
                        "Status = CASE WHEN Status IN ('PENDING','CONFIRMED') THEN 'PROCESSING' ELSE Status END, UpdatedAt = NOW() WHERE Id = @o",
                        Db.P("@o", orderId))
                    Return True
                End Function)
            Catch ex As Exception
                Db.LogError("ConfirmPayment", ex)
                Return ServiceResult.Fail("Payment update failed: " & ex.Message)
            End Try

            IssueReceiptFor(order)

            Dim notif As New NotificationService()
            notif.Notify(order.UserId, "Payment confirmed - " & order.OrderNumber,
                         "Your " & PaymentSetting.DisplayName(order.PaymentMethod) & " payment of " &
                         Fmt.PHP(order.TotalAmount) & " (ref " & ref & ") was confirmed. " &
                         "The order can now be prepared for shipping.",
                         "ORDER", "my-orders")
            Return ServiceResult.Ok("Payment confirmed (ref " & ref & ").")
        End Function

        ''' <summary>
        ''' The studio declines a submitted reference - the amount on the e-wallet
        ''' receipt does not match, or the transfer cannot be traced. The order falls
        ''' back to FAILED (never PAID), the customer is handed the support contact
        ''' immediately, and may resubmit a corrected reference afterwards.
        ''' </summary>
        Public Function DenyPayment(orderId As Integer, password As String) As ServiceResult
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Only the store team can decline a payment.")
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.Status = "CANCELLED" Then Return ServiceResult.Fail("This order was cancelled.")
            If order.PaymentStatus = "PAID" Then Return ServiceResult.Ok("Payment already confirmed.")
            If order.PaymentStatus = "REFUNDED" Then Return ServiceResult.Fail("This payment was already refunded.")
            If order.PaymentStatus <> "SUBMITTED" Then
                Return ServiceResult.Fail("There is no submitted payment waiting to be declined.")
            End If
            If String.IsNullOrEmpty(password) Then
                Return ServiceResult.Fail("Enter your password to decline this payment.")
            End If
            If Not PasswordOk(password) Then
                Return ServiceResult.Fail("Password incorrect - nothing was declined.")
            End If
            Try
                Db.InTransaction(Of Boolean)(Function() As Boolean
                    Db.Exec(
                        "UPDATE Payments SET Status = 'FAILED', GatewayResponse = @g " &
                        "WHERE OrderId = @o AND Status <> 'PAID'",
                        Db.P("@g", "DECLINED_BY_SELLER::" & Date.Now.ToString("yyyyMMddHHmmss")), Db.P("@o", orderId))
                    Db.Exec("UPDATE Orders SET PaymentStatus = 'FAILED', UpdatedAt = NOW() WHERE Id = @o",
                            Db.P("@o", orderId))
                    Return True
                End Function)
            Catch ex As Exception
                Db.LogError("DenyPayment", ex)
                Return ServiceResult.Fail("Payment update failed: " & ex.Message)
            End Try
            Dim notif As New NotificationService()
            notif.Notify(order.UserId, "Payment declined - " & order.OrderNumber,
                         PaymentFailedMessage("The seller could not confirm your payment for this order. " &
                                              "Please check your reference number and try again."),
                         "ORDER", "my-orders")
            Return ServiceResult.Ok("Payment declined. The customer has been notified.")
        End Function

        ''' <summary>
        ''' Customer confirms the parcel actually arrived. Deliberately gated on
        ''' DELIVERED: the store marks an order delivered when it hands it to the
        ''' courier, and until that has happened the buyer has nothing to confirm.
        ''' Setting RECEIVED also stamps ReceivedAt, so the row records that the
        ''' customer saw it land rather than the store merely asserting it did.
        ''' </summary>
        Public Function MarkReceived(orderId As Integer) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.UserId <> Session.CurrentUser.Id Then
                Return ServiceResult.Fail("You don't have access to this order.")
            End If
            If order.Status = "RECEIVED" Then Return ServiceResult.Ok("You already confirmed this order.")
            If order.Status <> "DELIVERED" Then
                Return ServiceResult.Fail("You can mark this received once it shows as delivered.")
            End If

            _orders.MarkReceived(orderId)
            Dim notif As New NotificationService()
            notif.NotifyRole("ADMIN", "Order received – " & order.OrderNumber,
                             "The customer confirmed this order arrived.",
                             "ORDER", "merchant-orders")
            Return ServiceResult.Ok("Thanks for confirming! Enjoy your art.")
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
            If order.Status = "CANCELLED" Then Return ServiceResult.Fail("This order was cancelled.")
            ' PROCESSING is refused alongside SHIPPED/DELIVERED on purpose. Confirming
            ' writes Status = CONFIRMED, so letting an in-flight order back in would
            ' drag it a step backwards in the pipeline. Re-quoting a CONFIRMED order is
            ' still allowed — that is how a mis-typed fee gets corrected.
            If order.Status = "PROCESSING" OrElse order.Status = "SHIPPED" OrElse
               order.Status = "DELIVERED" OrElse order.Status = "RECEIVED" Then
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
            If pwd = "" Then Return ServiceResult.Fail(PaymentFailedMessage("Enter your password to confirm the order."))
            Dim freshUser As User = New UserRepository().GetById(Session.CurrentUser.Id)
            If freshUser Is Nothing OrElse Not PasswordHasher.Verify(pwd, freshUser.PasswordHash) Then
                Return ServiceResult.Fail(PaymentFailedMessage("Password incorrect — the order was not confirmed."))
            End If

            Try
                Db.InTransaction(Of Boolean)(Function() As Boolean
                    _orders.ConfirmWithShippingFee(orderId, Math.Round(fee, 2), Session.CurrentUser.Id)
                    Return True
                End Function)
            Catch ex As Exception
                Db.LogError("ConfirmWithShippingFee", ex)
                Return ServiceResult.Fail(PaymentFailedMessage("Could not save the shipping fee: " & ex.Message))
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

            ' A delivery order cannot be confirmed on a bare status change: the courier
            ' fee is what the customer is quoted, so it has to be entered through
            ' ConfirmWithShippingFee. A zero fee is still a decision worth recording, so
            ' this gate is on ShippingFeeConfirmed rather than on the fee being non-zero.
            If newStatus = "CONFIRMED" AndAlso Not order.ShippingFeeConfirmed Then
                Return ServiceResult.Fail("Enter the J&T shipping fee to confirm this order — the customer is quoted the final total at that point.")
            End If

            ' Fulfilment only starts once the money is in: PROCESSING is the studio
            ' committing to build the order, so it waits on a confirmed payment just
            ' like the J&T hand-off below. Nothing unpaid can move forward at all.
            If newStatus = "PROCESSING" AndAlso order.PaymentStatus <> "PAID" Then
                Return ServiceResult.Fail(
                    "Payment is not confirmed for this order yet, so it cannot move into processing.")
            End If

            ' No parcel leaves the studio on an unpaid order. SHIPPED and DELIVERED are
            ' both physical hand-offs to J&T, so both wait on a confirmed payment. This
            ' used to be enforced nowhere, and marking an order DELIVERED also stamped
            ' PaymentStatus = PAID — so an order nobody paid for could walk the whole
            ' pipeline and come out the other end looking settled, receipt included.
            If (newStatus = "SHIPPED" OrElse newStatus = "DELIVERED") AndAlso order.PaymentStatus <> "PAID" Then
                Return ServiceResult.Fail(PaymentFailedMessage(
                    "Payment is not confirmed for this order, so it cannot be booked with J&T yet."))
            End If

            ' Tracking must be entered manually — never auto-generated. Booking (SHIPPED)
            ' is where the waybill number is typed in; DELIVERED is a later status change
            ' on a parcel that was already booked, so it checks the STORED waybill rather
            ' than the blank parameter the advance link passes in.
            If newStatus = "SHIPPED" AndAlso String.IsNullOrWhiteSpace(tracking) Then
                Return ServiceResult.Fail("Enter the J&T tracking number before marking this order as SHIPPED.")
            End If
            If newStatus = "DELIVERED" AndAlso String.IsNullOrWhiteSpace(order.TrackingNumber) Then
                Return ServiceResult.Fail("Book this order with J&T (enter its tracking number) before marking it as DELIVERED.")
            End If

            Dim wasCancelled As Boolean = order.Status = "CANCELLED"

            ' Validate state machine loosely (same-state is allowed for re-saves)
            _orders.UpdateStatus(orderId, newStatus)

            If newStatus = "SHIPPED" OrElse newStatus = "DELIVERED" Then
                _orders.UpdateShipping(orderId, "J&T Express", tracking, newStatus)
            End If
            If newStatus = "CANCELLED" Then
                _orders.UpdatePaymentStatus(orderId, "REFUNDED")
                If Not wasCancelled Then _orders.RestoreOrderStock(orderId)
            End If

            Dim shownTracking As String = If(String.IsNullOrWhiteSpace(tracking), order.TrackingNumber, tracking)
            Dim notif As New NotificationService()
            notif.Notify(order.UserId, "Order " & newStatus.Replace("_", " ") & " – " & order.OrderNumber,
                         "Your order is now: " & newStatus.Replace("_", " ") & ". " &
                         If(shownTracking.Length > 0, "Tracking: " & shownTracking, ""), "ORDER", "my-orders")
            Return ServiceResult.Ok("Order updated to " & newStatus & ".")
        End Function

        ' ----- Reviews ----------------------------------------------------------

        ''' <summary>
        ''' A review is only honest if the parcel actually reached the buyer, so it is
        ''' gated on the order being RECEIVED — the state only the customer can set,
        ''' and only once they have seen the piece land. Merely having bought something
        ''' is not enough.
        ''' </summary>
        Public Function SubmitReview(productId As Integer, orderId As Integer?, rating As Integer, comment As String) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If rating < 1 OrElse rating > 5 Then Return ServiceResult.Fail("Rating must be 1–5 stars.")
            If _orders.UserReviewedProduct(Session.CurrentUser.Id, productId) Then
                Return ServiceResult.Fail("You already reviewed this product.")
            End If
            ' Reviews are only allowed for a delivered-and-received purchase of this
            ' exact product, not just any order the shopper has ever placed.
            Dim receivedId As Integer? = ReceivedPurchaseOrderId(Session.CurrentUser.Id, productId)
            If Not receivedId.HasValue Then
                Return ServiceResult.Fail("You can review this item once you confirm you received it " &
                                          "from your order.")
            End If
            _orders.AddReview(Session.CurrentUser.Id, productId, receivedId.Value, rating, comment)
            Return ServiceResult.Ok("Thank you for your review!")
        End Function

        ''' <summary>
        ''' The most recent order for this product that reached RECEIVED, or Nothing.
        ''' RECEIVED is the gate: the customer marks it themselves, so a row carrying
        ''' it is proof the goods landed rather than a claim that they did.
        ''' </summary>
        Public Function ReceivedPurchaseOrderId(userId As Integer, productId As Integer) As Integer?
            Dim rows As List(Of Integer) = _orders.ReceivedPurchaseOrderIds(userId, productId, 1)
            If rows.Count = 0 Then Return Nothing
            Return rows(0)
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

        ''' <summary>Whether the current user may review this product right now — i.e. they confirmed it arrived.</summary>
        Public Function CanReview(productId As Integer) As Boolean
            If Not Session.IsAuthenticated Then Return False
            Return ReceivedPurchaseOrderId(Session.CurrentUser.Id, productId).HasValue
        End Function

        ''' <summary>The RECEIVED order this product sits in, or Nothing when none has arrived yet.</summary>
        Public Function PurchaseOrderId(productId As Integer) As Integer?
            If Not Session.IsAuthenticated Then Return Nothing
            Return ReceivedPurchaseOrderId(Session.CurrentUser.Id, productId)
        End Function

        ''' <summary>
        ''' Every product on a RECEIVED order this shopper has not reviewed yet — the
        ''' "write a review" queue on My Orders.
        ''' </summary>
        Public Function PendingReviewItems(orderId As Integer) As List(Of OrderItem)
            Dim all As List(Of OrderItem) = _orders.GetItems(orderId)
            Return all.Where(Function(i) Not _orders.UserReviewedProduct(Session.CurrentUser.Id, i.ProductId)).ToList()
        End Function

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

        ''' <summary>Merchant-side: force-issue a receipt for a payment. Every channel is prepaid, so it needs PAID.</summary>
        Public Function IssueReceiptByPaymentId(paymentId As Integer) As Receipt
            Dim p As Payment = _orders.GetPaymentById(paymentId)
            If p Is Nothing Then Return Nothing
            If p.Status <> "PAID" Then Return Nothing
            Dim o As Order = _orders.GetById(p.OrderId)
            If o Is Nothing Then Return Nothing
            Dim items As List(Of OrderItem) = _orders.GetItems(o.Id)
            Dim paidAt As Date? = If(p.Status = "PAID", p.PaidAt, Nothing)
            Return _receipts.Issue(o, items, p.Id, p.PaymentMethod, paidAt)
        End Function

        ''' <summary>Issue an official receipt whenever a payment becomes PAID.</summary>
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