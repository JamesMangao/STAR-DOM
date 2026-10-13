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
        ''' The studio's earnings ledger. Every path that establishes RECEIVED
        ''' releases it, and every path that refunds voids it, so the two can never
        ''' drift apart from the order's own status.
        ''' </summary>
        Private ReadOnly _ledger As New CommissionLedgerService()

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
            ' Checkout parks the method as "PENDING" for the quote-first flow; the
            ' customer picks a real channel on the order page once the studio returns
            ' the final total and shipping fee.
            Dim method As String = If(paymentMethod, "").Trim().ToUpperInvariant()
            If method = PaymentSettingRepository.LegacyMaya Then method = PaymentSettingRepository.Gotyme
            If method = "" Then method = "PENDING"
            If method <> "PENDING" Then
                Dim enabled As PaymentSettingRepository = New PaymentSettingRepository()
                If Not enabled.IsChannelEnabled(method) Then
                    Return ServiceResult.Fail("That payment method is not available right now.")
                End If
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

            ' A method the order already carries is a commitment the customer made,
            ' not a fresh choice. The owner can disable or delete the channel
            ' afterwards -- an admin tidying up a retired bank, say -- and that must
            ' not strand an order that is already on its way. Re-confirming the same
            ' method changes nothing, so it is allowed even when the channel no
            ' longer resolves. Choosing something DIFFERENT still goes through the
            ' availability check below, because that is a new selection and has to
            ' point at an account that exists.
            Dim alreadyChosen As Boolean =
                String.Equals(order.PaymentMethod, pick, StringComparison.OrdinalIgnoreCase)
            If Not alreadyChosen AndAlso Not _paySettings.IsChannelEnabled(pick) Then
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
        ''' Step 3 of the payment flow - the customer's side. The buyer pays by the
        ''' channel shown on the order page (any enabled channel from Payment Settings),
        ''' hands over the reference number from the receipt plus their password. This
        ''' does NOT make the order paid: it parks PaymentStatus at SUBMITTED so the
        ''' studio can check the transfer against its own records first. No receipt is
        ''' issued, PROCESSING cannot start, and J&amp;T stays shut until the studio
        ''' confirms below.
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
            _orders.SetReceiptSource(orderId, ReceiptSources.Customer)
            _orders.AppendHistory(orderId, order.Status, OrderStatuses.Received,
                                  ReceiptSources.Customer, "", Session.CurrentUser.Username)
            ' The parcel is in the buyer's hands, so the studio has genuinely earned
            ' the sale. Released here rather than by a scheduled job so the ledger row
            ' is never out of step with the order it belongs to.
            _ledger.ReleaseForOrder(order, "Released: the customer confirmed the parcel arrived")
            Dim notif As New NotificationService()
            notif.NotifyRole("ADMIN", "Order received – " & order.OrderNumber,
                             "The customer confirmed this order arrived.",
                             "ORDER", "merchant-orders")
            Return ServiceResult.Ok("Thanks for confirming! Enjoy your art.")
        End Function

''' <summary>
        ''' Open a delivery dispute. Only a customer may open one, only on their own
        ''' order, and only once the studio has marked it DELIVERED: a dispute means
        ''' "the studio says this landed and I say it did not", so there has to be a
        ''' delivery claim to disagree with. Anything earlier is the studio simply not
        ''' having updated the order yet, which is not a dispute.
        ''' </summary>
        ''' <remarks>
        ''' The status becomes HELD rather than CANCELLED or RECEIVED. The parcel may
        ''' well have arrived and nobody has decided yet, and an order in that state
        ''' must not read as either finished or refunded. Recording the complaint
        ''' without changing the status would be worse than useless: the customer
        ''' would still be swept up by the delivery close and their complaint would
        ''' arrive after the fact.
        ''' </remarks>
        Public Function OpenDispute(orderId As Integer, reason As String) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.UserId <> Session.CurrentUser.Id Then
                Return ServiceResult.Fail("You don't have access to this order.")
            End If
            If order.Status = OrderStatuses.Held Then
                Return ServiceResult.Fail("This order is already under investigation.")
            End If
            If order.Status <> OrderStatuses.Delivered Then
                Return ServiceResult.Fail("You can raise a delivery issue once the order shows as delivered.")
            End If
            Dim text As String = If(reason, "").Trim()
            If text.Length < 10 Then
                Return ServiceResult.Fail("Please describe the problem in a little more detail so the studio can look into it.")
            End If

            _orders.OpenHold(orderId, text, Session.CurrentUser.Username)
            _orders.AppendHistory(orderId, order.Status, OrderStatuses.Held,
                                  ReceiptSources.Customer, text, Session.CurrentUser.Username)
            ' A dispute stops the sweep from closing the order, so the earning must
            ' stop moving with it. It is already HELD (the order reached DELIVERED to
            ' be disputable at all); this is the idempotent safety net for any row
            ' whose accrual was missed, e.g. an order delivered before the ledger
            ' existed.
            _ledger.HoldForOrder(order, "Held: delivery is under dispute")
            Dim notif As New NotificationService()
            notif.NotifyRole("ADMIN", "Delivery dispute " & order.OrderNumber,
                             "The customer reports a problem with this delivery: " & text,
                             "ORDER", "merchant-orders")
            Return ServiceResult.Ok("Thank you. The studio has been notified and will look into this delivery.")
        End Function

        ''' <summary>
        ''' Close a dispute as RECEIVED, REFUNDED or RESHIP. Admin only.
        ''' </summary>
        ''' <remarks>
        ''' The three outcomes are deliberately blunt. There is no reviewer queue and
        ''' no partial state: someone has to look at the parcel and say what happened
        ''' to it, and each of those three is a real answer. REFUNDED records that
        ''' decision only -- the studio hands the money back over the phone or in
        ''' person, because there is no gateway here that could move it by itself.
        ''' </remarks>
        Public Function ResolveDispute(orderId As Integer, resolution As String,
                                       note As String, igRef As String,
                                       Optional tracking As String = "") As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If Not Session.IsAdmin Then Return ServiceResult.Fail("Only an administrator can resolve a dispute.")

            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.Status <> OrderStatuses.Held Then
                Return ServiceResult.Fail("This order is not under investigation.")
            End If

            Dim res As String = If(resolution, "").Trim().ToUpperInvariant()
            Dim targetStatus As String
            Dim waybill As String = If(tracking, "").Trim()
            Select Case res
                Case "RECEIVED"
                    targetStatus = OrderStatuses.Received
                Case "REFUNDED"
                    targetStatus = OrderStatuses.Refunded
                Case "RESHIP"
                    ' A reship goes back out through the same door as the first parcel:
                    ' SHIPPED, not straight to DELIVERED. It is a new courier hand-off,
                    ' so it needs a new J&T waybill -- the old one is the parcel the
                    ' customer never got, and reusing it would leave them tracking a
                    ' shipment that already ended. Without a number there is nothing
                    ' for the customer to follow, so a missing one is refused outright.
                    If waybill = "" Then
                        Return ServiceResult.Fail("Enter the new J&T tracking number for the reshipped parcel.")
                    End If
                    targetStatus = OrderStatuses.Shipped
                Case Else
                    Return ServiceResult.Fail("Choose whether the parcel was received, refunded, or sent again.")
            End Select

            Dim ig As String = If(igRef, "").Trim()
            _orders.ResolveHold(orderId, res, targetStatus, Session.CurrentUser.Username)
            If res = "RECEIVED" Then
                _orders.MarkReceived(orderId)
                _orders.SetReceiptSource(orderId, ReceiptSources.Studio)
            End If
            If res = "REFUNDED" Then
                _orders.SetRefundIgRef(orderId, ig)
            ElseIf res = "RESHIP" Then
                ' No refund, so no money moves: the order simply goes back out. The
                ' delivery cycle is reset with it, so the new parcel gets its own
                ' delivered notification and its own reminder before the sweep may
                ' close it again -- and the earning stays HELD throughout, because
                ' nothing has been confirmed received yet.
                _orders.UpdateShipping(orderId, "J&T Express", waybill, OrderStatuses.Shipped)
                _orders.ResetDeliveryCycle(orderId)
                _orders.SetHoldIgRef(orderId, ig)
            Else
                _orders.SetHoldIgRef(orderId, ig)
            End If
            _orders.AppendHistory(orderId, OrderStatuses.Held, targetStatus,
                                  ReceiptSources.Studio, TimelineReason(ig, note), Session.CurrentUser.Username)

            If res = "RECEIVED" Then
                _ledger.ReleaseForOrder(order, "Released: the studio resolved the dispute as received")
            ElseIf res = "REFUNDED" Then
                ' Same as RecordRefund: the payment moves with the order, so a refunded
                ' sale is not left inside gross revenue.
                _orders.UpdatePaymentStatus(orderId, OrderStatuses.Refunded)
                _orders.SetPaymentsRefunded(orderId)
                _ledger.VoidForOrder(order, If(Trim(If(note, "")) = "", "Dispute resolved as refunded", note))
                _receipts.MarkRefunded(orderId)
            ElseIf res = "RESHIP" Then
                ' Still not confirmed received, so the earning stays exactly where it
                ' is: held.
                _ledger.HoldForOrder(order, "Held: the parcel is being sent again")
            End If

            Dim outcomeLine As String
            Select Case res
                Case "RECEIVED" : outcomeLine = "the parcel did arrive"
                Case "REFUNDED" : outcomeLine = "the order was refunded"
                Case "RESHIP" : outcomeLine = "the parcel is being sent again"
                Case Else : outcomeLine = res.ToLowerInvariant()
            End Select
            Dim notif As New NotificationService()
            notif.Notify(order.UserId, "Your order " & order.OrderNumber & " has been looked into",
                         "The studio has finished investigating your delivery: " & outcomeLine & "." &
                         If(res = "RESHIP", " Nothing further is needed from you — the new parcel will " &
                                            "arrive with tracking number " & waybill & ".", "") &
                         If(ig <> "", " (Instagram reference " & ig & ")", ""),
                         "ORDER", "order-detail")
            Return ServiceResult.Ok("Dispute resolved.")
        End Function

        ''' <summary>
        ''' The stored reason for a studio-sourced transition, with the Instagram
        ''' reference in front of it. The history row is what the timeline panel
        ''' renders, so putting the IG ref here -- rather than only on the order row --
        ''' is what makes "resolved by studio &middot; IG ref &middot; reason" one honest line
        ''' instead of two facts kept in different places.
        ''' </summary>
        Private Shared Function TimelineReason(igRef As String, note As String) As String
            Dim ig As String = Trim(If(igRef, ""))
            Dim text As String = Trim(If(note, ""))
            If ig = "" Then Return text
            If text = "" Then Return "IG " & ig
            Return "IG " & ig & " · " & text
        End Function

        ''' <summary>
        ''' Mark an order received on the customer's behalf. Admin only, and requires
        ''' a reason, a proof of delivery already on file, and the admin's password.
        ''' </summary>
        ''' <remarks>
        ''' This exists for the real case: the customer tells the studio over the phone
        ''' that the parcel arrived and never presses the button. Refusing would only
        ''' push people back to arguing with the courier. What it must not be is
        ''' silent -- so ReceiptSource records STUDIO and the reason is mandatory,
        ''' leaving a plain answer to "why does this order say received when nobody
        ''' clicked anything".
        '''
        ''' The locked spec requires POD + password for this path. An order without a
        ''' proof of delivery cannot be closed by the studio on the customer's behalf.
        ''' </remarks>
        Public Function StudioMarkReceived(orderId As Integer, reason As String,
                                          igRef As String, password As String) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If Not Session.IsAdmin Then Return ServiceResult.Fail("Only an administrator can do that.")

            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.Status = OrderStatuses.Received Then
                Return ServiceResult.Fail("This order is already marked received.")
            End If
            If order.Status <> OrderStatuses.Delivered AndAlso order.Status <> OrderStatuses.Held Then
                Return ServiceResult.Fail("This order has not been delivered yet.")
            End If
            Dim text As String = If(reason, "").Trim()
            If text.Length = 0 Then
                Return ServiceResult.Fail("Say how you know the parcel arrived - the reason is kept on the order.")
            End If
            Dim pwd As String = If(password, "").Trim()
            If pwd = "" Then Return ServiceResult.Fail(PaymentFailedMessage("Enter your password to mark this order as received."))
            Dim freshUser As User = New UserRepository().GetById(Session.CurrentUser.Id)
            If freshUser Is Nothing OrElse Not PasswordHasher.Verify(pwd, freshUser.PasswordHash) Then
                Return ServiceResult.Fail(PaymentFailedMessage("Password incorrect — the order was not marked."))
            End If

            ' The locked spec requires a proof of delivery on file for studio override.
            If order.PodImageData Is Nothing OrElse order.PodImageData.Length = 0 Then
                Return ServiceResult.Fail("This order has no proof of delivery on file. Upload a POD first " &
                                          "before marking it as received on the customer's behalf.")
            End If

            Dim ig As String = If(igRef, "").Trim()
            _orders.MarkReceived(orderId)
            _orders.SetReceiptSource(orderId, ReceiptSources.Studio)
            _orders.SetHoldIgRef(orderId, ig)  ' record IG ref alongside studio override
            _orders.AppendHistory(orderId, order.Status, OrderStatuses.Received,
                                  ReceiptSources.Studio, TimelineReason(ig, text), Session.CurrentUser.Username)
            ' The studio asserted the parcel arrived and carries a POD to back it, so
            ' the earning is released on the same grounds the customer's own
            ' confirmation would have released it.
            _ledger.ReleaseForOrder(order, "Released: the studio marked the order received on the customer's behalf")
            Return ServiceResult.Ok("Order marked as received.")
        End Function

        ''' <summary>
        ''' Record the courier outcome and proof of delivery for an order. Admin only.
        ''' </summary>
        ''' <remarks>
        ''' Saving a POD does NOT close the order. It is evidence, and evidence is not
        ''' a decision: the customer normally confirms receipt, and the dispute path
        ''' still exists for when the evidence is wrong. Coupling the two would mean
        ''' the studio could end an order unilaterally by uploading a screenshot,
        ''' which is exactly the power this design is meant to take away.
        ''' </remarks>
        Public Function SaveProofOfDelivery(orderId As Integer, image As Byte(), mime As String,
                                            reference As String, courierStatus As String) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If Not Session.IsAdmin Then Return ServiceResult.Fail("Only an administrator can do that.")

            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.Status = OrderStatuses.Cancelled Then
                Return ServiceResult.Fail("This order was cancelled.")
            End If

            Dim err As String = _orders.SavePod(orderId, image, mime, reference, courierStatus,
                                                 Session.CurrentUser.Username)
            If Not String.IsNullOrEmpty(err) Then Return ServiceResult.Fail(err)

            _orders.AppendHistory(orderId, order.Status, order.Status, ReceiptSources.CourierPod,
                                  "Proof of delivery on file: " & reference.Trim(),
                                  Session.CurrentUser.Username)
            Return ServiceResult.Ok("Proof of delivery saved.")
        End Function

        ''' <summary>
        ''' Record a refund for an order. Admin only. The order status becomes REFUNDED
        ''' (not CANCELLED), the IG reference is recorded, and the audit trail captures
        ''' the reason. The money itself moves outside the system -- this records the
        ''' decision, it does not perform a refund.
        ''' </summary>
        ''' <remarks>
        ''' The locked spec: "admin 'Record a refund' + reason + IG ref -> REFUNDED
        ''' (not CANCELLED). receipt: existing OR marked refunded. commission: clawback.
        ''' void commission row, flag reversal. No money moves."
        '''
        ''' Each of those three is honoured literally:
        '''   - REFUNDED, never CANCELLED. Cancelling an order and refunding one are
        '''     different facts, and writing CANCELLED over a paid order loses the
        '''     record that money actually changed hands.
        '''   - The official receipt is KEPT and stamped refunded. It cannot be
        '''     re-issued (UNIQUE(PaymentId)) and it must not be deleted: the sale
        '''     genuinely happened and the BIR record of it survives the refund.
        '''   - The studio's earning is voided. If it had already been released when
        '''     the customer confirmed receipt, the row is voided AND flagged as a
        '''     reversal -- a clawback rather than a row that never existed.
        '''
        ''' No money moves: there is no gateway here that could send it, so this
        ''' records the decision exactly as Orders.REFUNDED does.
        ''' </remarks>
        Public Function RecordRefund(orderId As Integer, reason As String,
                                     igRef As String, password As String) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If Not Session.IsAdmin Then Return ServiceResult.Fail("Only an administrator can do that.")

            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")
            If order.Status = OrderStatuses.Cancelled Then
                Return ServiceResult.Fail("This order was cancelled.")
            End If
            If order.Status = OrderStatuses.Refunded Then
                Return ServiceResult.Fail("This order is already refunded.")
            End If
            ' A refund is money going back, so there has to be money to give back.
            ' A PENDING or FAILED payment is not a refund, it is a cancellation, and
            ' offering it here would let REFUNDED be written over an unpaid order.
            If order.PaymentStatus <> "PAID" Then
                Return ServiceResult.Fail("This order has no confirmed payment, so there is nothing to refund. " &
                                          "Cancel it instead.")
            End If
            Dim text As String = If(reason, "").Trim()
            If text.Length = 0 Then
                Return ServiceResult.Fail("Say why this order is being refunded - the reason is kept on the order.")
            End If
            Dim pwd As String = If(password, "").Trim()
            If pwd = "" Then Return ServiceResult.Fail(PaymentFailedMessage("Enter your password to record this refund."))
            Dim freshUser As User = New UserRepository().GetById(Session.CurrentUser.Id)
            If freshUser Is Nothing OrElse Not PasswordHasher.Verify(pwd, freshUser.PasswordHash) Then
                Return ServiceResult.Fail(PaymentFailedMessage("Password incorrect -- the refund was not recorded."))
            End If

            Dim ig As String = If(igRef, "").Trim()
            _orders.UpdateStatus(orderId, OrderStatuses.Refunded)
            ' The order is REFUNDED, and so is what was paid against it. Leaving the
            ' payment at PAID would keep a refunded sale inside gross revenue, which is
            ' summed over PaymentStatus = 'PAID'.
            _orders.UpdatePaymentStatus(orderId, OrderStatuses.Refunded)
            _orders.SetPaymentsRefunded(orderId)
            _orders.SetRefundIgRef(orderId, ig)
            _orders.AppendHistory(orderId, order.Status, OrderStatuses.Refunded,
                                  ReceiptSources.Studio, TimelineReason(ig, text), Session.CurrentUser.Username)
            ' The receipt is marked, not re-issued. Best-effort on purpose: a receipt
            ' failure must not be able to make a recorded refund look like it never
            ' happened.
            _receipts.MarkRefunded(orderId)
            ' Void the earning, flagging a reversal when it had already been released.
            _ledger.VoidForOrder(order, text)

            Dim notif As New NotificationService()
            notif.Notify(order.UserId, "Your order " & order.OrderNumber & " has been refunded",
                         "The studio has recorded a refund for this order. Reason: " & text &
                         If(ig <> "", " (Instagram reference " & ig & ")", ""),
                         "ORDER", "order-detail")
            Return ServiceResult.Ok("Refund recorded. " & PaymentSetting.SupportContact)
        End Function


        ' ====================================================================
        ' The delivery sweep
        ' ====================================================================
        ' A parcel that lands and is never confirmed is the normal case, not the
        ' exception: most buyers simply never press the button. Without a scheduled
        ' close, every one of those orders sits in DELIVERED for ever, the studio's
        ' earning never releases, and the order can never be reviewed. The sweep is
        ' the answer, and its whole design is about not being able to do damage:
        '
        '   - The window is counted from the courier hand-off, not from now.
        '   - Day 5-6 sends ONE reminder (ReminderSentAt is set-once).
        '   - Day 7 closes the order ONLY when every one of these holds:
        '       * the customer was told the parcel landed (DeliveredNotifiedAt), and
        '       * the single reminder actually went out (ReminderSentAt), and
        '       * a proof of delivery is on file, and
        '       * the studio recorded the courier as having delivered it.
        '     Anything else stays DELIVERED and is flagged to the studio instead.
        '   - HELD orders are not in the candidate set at all, so a dispute stops the
        '     close completely rather than only being written down.
        '
        ' Every write is guarded (set-once stamps, ON CONFLICT DO NOTHING on the
        ' ledger), so a re-run, an overlapping run and a manual retry are all safe.

        ''' <summary>Days after the courier hand-off at which the reminder goes out.</summary>
        Public Const SweepReminderAfterDays As Integer = 5
        ''' <summary>Days after the courier hand-off at which the order may be closed.</summary>
        Public Const SweepAutoCloseAfterDays As Integer = 7
        ''' <summary>Recorded as the actor on a row the scheduled job wrote.</summary>
        Public Const SweepActor As String = "delivery-sweep"

        ''' <summary>
        ''' True when the studio's courier status says the parcel was delivered.
        ''' </summary>
        ''' <remarks>
        ''' This is a text match on what a person typed off J&amp;T's tracking page, and
        ''' that is deliberate: there is no courier API, so an automatic status claim
        ''' would be a guess. "deliver" covers J&amp;T's own wordings (Delivered,
        ''' DELIVERED, Delivered - left with neighbour), which is the only vocabulary
        ''' the studio is told to type.
        ''' </remarks>
        Public Shared Function CourierConfirmsDelivery(courierStatus As String) As Boolean
            Dim s As String = If(courierStatus, "").Trim().ToLowerInvariant()
            If s = "" Then Return False
            Return s.Contains("deliver")
        End Function

        ''' <summary>What one sweep run did. Counted so the run can be logged and read.</summary>
        Public Class DeliverySweepReport
            Public Property Examined As Integer
            Public Property Notified As Integer
            Public Property Reminded As Integer
            Public Property Closed As Integer
            Public Property Flagged As Integer

            Public ReadOnly Property Summary As String
                Get
                    Return "delivery sweep: " & Examined.ToString() & " delivered, " &
                           Notified.ToString() & " notified, " & Reminded.ToString() & " reminded, " &
                           Closed.ToString() & " closed, " & Flagged.ToString() & " flagged"
                End Get
            End Property
        End Class

        ''' <summary>
        ''' Run the delivery sweep. Safe to call twice; safe to call late.
        ''' </summary>
        ''' <remarks>
        ''' Idempotency comes from the data, not from a lock: the two stamps are
        ''' set-once, the ledger accrual is ON CONFLICT DO NOTHING, and both the close
        ''' and the flag are guarded on the order still being in the state they expect.
        ''' Two runs racing can therefore both do their job without a second
        ''' notification, a second history row or a double commission. A dry run reads
        ''' and reports without writing anything.
        ''' </remarks>
        Public Function RunDeliverySweep(Optional dryRun As Boolean = False) As DeliverySweepReport
            Dim report As New DeliverySweepReport()
            Dim notif As New NotificationService()
            Dim today As Date = Clock.Now

            ' A failure on one order must not abandon the ones behind it: the sweep's
            ' job is to make progress on a batch, and one unreadable row would
            ' otherwise stall every delivery in the system for good.
            For Each c As DeliverySweepCandidate In _orders.ListDeliveredForSweep()
                Try
                    report.Examined += 1
                    Dim days As Integer = If(c.DeliveredAt.HasValue,
                                             CInt(Math.Floor((today - c.DeliveredAt.Value).TotalDays)), 0)

                    ' 1. The same-day announcement. Normally stamped the moment the order
                    '    became DELIVERED; this is the catch-up for rows delivered before
                    '    that stamp existed, and it is a precondition of the close below.
                    If Not c.DeliveredNotifiedAt.HasValue Then
                        c.DeliveredNotifiedAt = today
                        If Not dryRun Then
                            _orders.StampDeliveredNotified(c.OrderId)
                            notif.Notify(c.UserId, "Order delivered – " & c.OrderNumber,
                                         "Your parcel has been delivered. Please confirm you received it in " &
                                         "My Orders — and if anything is wrong, report a delivery problem " &
                                         "from the same page within 7 days.", "ORDER", "order-detail")
                        End If
                        report.Notified += 1
                    End If

                    ' 2. ONE reminder, once, in the 5-6 day window.
                    If days >= SweepReminderAfterDays AndAlso Not c.ReminderSentAt.HasValue Then
                        c.ReminderSentAt = today
                        If Not dryRun Then
                            _orders.StampReminderSent(c.OrderId)
                            notif.Notify(c.UserId, "Did your order arrive? – " & c.OrderNumber,
                                         "Your order was marked delivered and we have not heard back. " &
                                         "If it arrived, confirm it in My Orders so it can be closed. If it " &
                                         "did not, report a delivery problem there and the studio will look " &
                                         "into it with J&amp;T Express — nothing closes automatically while a " &
                                         "dispute is open.", "ORDER", "order-detail")
                        End If
                        report.Reminded += 1
                    End If

                    ' 3. Day 7: close, or flag and leave it alone. The two stamps are a
                    '    hard precondition, so an order whose customer was never
                    '    notified can never be closed against them.
                    If days >= SweepAutoCloseAfterDays AndAlso
                       c.DeliveredNotifiedAt.HasValue AndAlso c.ReminderSentAt.HasValue Then
                        If c.HasPod AndAlso CourierConfirmsDelivery(c.CourierStatus) Then
                            If Not dryRun Then
                                ' The customer is now RECEIVED by the courier's evidence and
                                ' the absence of any dispute, and the audit trail says so in
                                ' words rather than leaving a bare status flip.
                                _orders.MarkReceived(c.OrderId)
                                _orders.SetReceiptSource(c.OrderId, ReceiptSources.CourierPod)
                                _orders.AppendHistory(c.OrderId, OrderStatuses.Delivered, OrderStatuses.Received,
                                                      ReceiptSources.CourierPod,
                                                      "Closed automatically: courier proof of delivery on file and no dispute in the delivery window",
                                                      SweepActor)
                                Dim closed As Order = _orders.GetById(c.OrderId)
                                _ledger.ReleaseForOrder(closed, "Released: closed automatically from the courier proof of delivery")
                                notif.Notify(c.UserId, "Order closed – " & c.OrderNumber,
                                             "Your order has been recorded as received: the studio has the " &
                                             "courier's proof of delivery on file and no problem was reported. " &
                                             "If that is wrong, message the studio and they will sort it out.",
                                             "ORDER", "order-detail")
                            End If
                            report.Closed += 1
                        ElseIf Not _orders.HasSweepFlag(c.OrderId) Then
                            ' Nothing to close it on. Stays DELIVERED and the studio is
                            ' told once: the flag is read back out of the audit trail, so a
                            ' daily run does not repeat the same note every morning.
                            If Not dryRun Then
                                _orders.AppendHistory(c.OrderId, OrderStatuses.Delivered, OrderStatuses.Delivered,
                                                      ReceiptSources.AutoSweep,
                                                      "Past the delivery window with no courier proof of delivery on file — the studio needs to check with J&T and record it",
                                                      SweepActor)
                                notif.NotifyRole("ADMIN", "Order needs attention – " & c.OrderNumber,
                                                 "This order has been sitting in DELIVERED past the delivery window. " &
                                                 "There is no courier proof of delivery on file, so it cannot be closed " &
                                                 "automatically. Check it with J&T Express and record the outcome on " &
                                                 "the Delivery page.",
                                                 "ORDER", "merchant-orders")
                            End If
                            report.Flagged += 1
                        End If
                    End If
                Catch ex As Exception
                    ' One bad order must not take the run down; the next run picks it up.
                    Db.LogError("DeliverySweep", ex)
                End Try
            Next

            ' The run itself is logged. It goes to AppErrors because that is the only
            ' table this application already keeps for machine-run auditable events,
            ' and each order the sweep touched also left a row in its own history -- so
            ' "what did the 3am job do" is answerable from both ends.
            If Not dryRun Then
                Try
                    Db.LogError("DeliverySweep", New InvalidOperationException(report.Summary))
                Catch
                    ' never let logging break the run
                End Try
            End If
            Return report
        End Function

        ''' <summary>The delivery audit trail for an order, newest first.</summary>
        Public Function OrderHistory(orderId As Integer) As List(Of OrderStatusEntry)
            Return _orders.ListHistory(orderId)
        End Function

        ''' <summary>
        ''' The delivery panel's timeline for one order: the recorded transitions,
        ''' each rendered in the fixed wording the locked spec lays down.
        ''' </summary>
        ''' <remarks>
        ''' Built from the history rows rather than from the current status, because a
        ''' status is only ever one moment and the questions people actually ask
        ''' ("who closed this, and why", "was it the courier's proof or my own click")
        ''' are answered by the sequence. The provenance words -- "confirmed by
        ''' customer", "courier POD on file", "marked by studio" -- ARE the answer, so
        ''' no username is appended: the line already says who acted.
        ''' </remarks>
        Public Function DeliveryTimeline(order As Order) As List(Of String)
            Dim lines As New List(Of String)()
            If order Is Nothing OrElse order.Id <= 0 Then Return lines
            For Each e As OrderStatusEntry In _orders.ListHistory(order.Id)
                Dim line As String = TimelineLine(e, order)
                If line <> "" Then lines.Add(line)
            Next
            Return lines
        End Function

        ''' <summary>One timeline line, in the wording the locked spec fixes.</summary>
        Public Shared Function TimelineLine(entry As OrderStatusEntry, order As Order) As String
            If entry Is Nothing Then Return ""
            ' Named toStatus/fromStatus, not to/from: both are VB keywords (For..To,
            ' and the From in a query expression), so the short names do not compile.
            Dim toStatus As String = If(entry.ToStatus, "").Trim().ToUpperInvariant()
            Dim fromStatus As String = If(entry.FromStatus, "").Trim().ToUpperInvariant()
            Dim source As String = If(entry.Source, "").Trim().ToUpperInvariant()
            Dim reason As String = Trim(If(entry.Reason, ""))
            Dim podOnFile As Boolean = order IsNot Nothing AndAlso order.HasPod

            Select Case toStatus
                Case OrderStatuses.Delivered
                    ' Two different DELIVERED rows exist: the delivery claim itself, and
                    ' the sweep's own "still not closed" flag. Only the first is a
                    ' delivery event, so the flag is skipped -- it is the studio's to-do,
                    ' not a step the customer took.
                    If source = ReceiptSources.AutoSweep Then Return ""
                    If podOnFile Then Return "DELIVERED · courier POD on file"
                    Return "DELIVERED · " & If(reason <> "", reason, "handed to J&T Express")

                Case OrderStatuses.Held
                    Return "HELD · under investigation · contact studio via IG"

                Case OrderStatuses.Received
                    Select Case source
                        Case ReceiptSources.Customer
                            Return "RECEIVED · confirmed by customer"
                        Case ReceiptSources.CourierPod
                            Return "RECEIVED · courier POD on file"
                        Case ReceiptSources.AutoSweep
                            Return "RECEIVED · courier POD on file"
                        Case Else
                            ' A studio-sourced RECEIVED is either the studio asserting it
                            ' on the customer's behalf, or a dispute the studio resolved.
                            ' The two are told apart by where the row came FROM, which is
                            ' exactly why the from-status is stored on the row.
                            If fromStatus = OrderStatuses.Held Then
                                Return "RECEIVED · resolved by studio · " & If(reason <> "", reason, "no reason given")
                            End If
                            Return "RECEIVED · marked by studio · " & If(reason <> "", reason, "no reason given")
                    End Select

                Case OrderStatuses.Refunded
                    Return "REFUNDED · recorded by studio · " & If(reason <> "", reason, "no reason given")

                Case Else
                    ' Anything else the order went through (PENDING, CONFIRMED, ...) is
                    ' not part of the delivery story this panel tells.
                    Return ""
            End Select
        End Function


        ''' <summary>The proof-of-delivery image for an order, or Nothing.</summary>
        Public Function PodImage(orderId As Integer) As Byte()
            Return _orders.GetPodImage(orderId)
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
               order.Status = "DELIVERED" OrElse order.Status = "RECEIVED" OrElse
               order.Status = "REFUNDED" OrElse order.Status = "HELD" Then
                Return ServiceResult.Fail("This order is already on its way — the shipping fee can no longer be changed.")
            End If
            ' Once the customer has paid against a quoted total the total is settled:
            ' re-quoting now would change what they were charged after the fact. A
            ' still-unpaid CONFIRMED order may be corrected; a SUBMITTED or PAID one
            ' may not.
            If order.PaymentStatus = "SUBMITTED" OrElse order.PaymentStatus = "PAID" Then
                Return ServiceResult.Fail("The customer has already paid against this total, so the shipping fee can no longer be changed.")
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

        ''' <summary>
        ''' Merchant updates fulfilment state, keeping the payment status consistent.
        ''' A customer may also call this to cancel their own order, but only while it
        ''' is still PENDING and nothing has been paid. A reason is mandatory for any
        ''' cancellation, from either side.
        ''' </summary>
        Public Function UpdateOrderState(orderId As Integer, newStatus As String, Optional tracking As String = "", Optional reason As String = "") As ServiceResult
            Dim order As Order = _orders.GetById(orderId)
            If order Is Nothing Then Return ServiceResult.Fail("Order not found.")

            ' Authorization. Fulfilment transitions are store-only; a customer may
            ' only cancel their own order. Without this check any signed-in user could
            ' POST UpdateOrderState and cancel (or advance) somebody else's order.
            Dim isStore As Boolean = Session.CanManageStore
            Dim isOwnOrder As Boolean = Session.IsAuthenticated AndAlso order.UserId = Session.CurrentUser.Id
            If newStatus = "CANCELLED" AndAlso Not isStore Then
                If Not isOwnOrder Then Return ServiceResult.Fail("Not authorized.")
                ' The cutoff is the PAYMENT, not the courier quote. The rule table has
                ' PENDING payment cancellable from both PENDING and CONFIRMED order
                ' status, and only blocks once a reference is SUBMITTED or the money is
                ' PAID/REFUNDED. Gating on the order status alone was wrong twice over:
                ' it refused a CONFIRMED-but-unpaid order the customer was told they
                ' could still cancel, and it left FAILED open (which is correct -- a
                ' rejected reference means nothing was paid, so the order may still be
                ' pulled back).
                If order.PaymentStatus = "SUBMITTED" OrElse order.PaymentStatus = "PAID" OrElse
                   order.PaymentStatus = "REFUNDED" Then
                    Return ServiceResult.Fail("A payment has already been submitted for this order, so it can no " &
                                              "longer be cancelled here. " & PaymentSetting.SupportContact)
                End If
                If order.Status <> "PENDING" AndAlso order.Status <> "CONFIRMED" Then
                    Return ServiceResult.Fail("This order is already being prepared, so it can no " &
                                              "longer be cancelled here. " & PaymentSetting.SupportContact)
                End If
            ElseIf Not isStore Then
                Return ServiceResult.Fail("Not authorized.")
            End If

            ' A settled order does not go back into the pipeline. This was missing
            ' before HELD and REFUNDED existed, but it was already a way to take a
            ' RECEIVED order and walk it back to PROCESSING. CANCELLED stays reachable
            ' from anywhere because the store is deliberately allowed to cancel an
            ' order outright; every other settled state is final.
            If order.Status <> "CANCELLED" AndAlso newStatus <> "CANCELLED" AndAlso
               OrderStatuses.IsTerminal(order.Status) Then
                Return ServiceResult.Fail("This order is already settled and cannot be changed to " &
                                          newStatus.Replace("_", " ").ToLowerInvariant() & ".")
            End If

            ' A delivery dispute is the studio's to resolve, not to advance around.
            ' Letting a plain status change move a HELD order on would sidestep
            ' ResolveDispute entirely, and the dispute would be resolved by accident.
            If order.Status = "HELD" Then
                Return ServiceResult.Fail("This order is under investigation. Resolve the dispute " &
                                          "before changing its status.")
            End If

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

            ' DELIVERED requires proof: either a tracking number (the J&T waybill that
            ' was entered at SHIPPED) OR a proof of delivery already on file. Without
            ' either there is nothing to show the customer and nothing the sweep can
            ' use to auto-close. This is the "tracking# o POD" gate from the locked spec.
            If newStatus = "DELIVERED" Then
                Dim hasTracking As Boolean = Not String.IsNullOrWhiteSpace(order.TrackingNumber)
                Dim hasPod As Boolean = order.PodImageData IsNot Nothing AndAlso order.PodImageData.Length > 0
                If Not hasTracking AndAlso Not hasPod Then
                    Return ServiceResult.Fail("This order cannot be marked DELIVERED yet — enter the J&T tracking number " &
                                              "or upload a proof of delivery first.")
                End If
            End If

            ' Every cancellation carries a reason: an order that simply disappears
            ' gives the other side nothing to act on. Required for the customer and
            ' the studio alike.
            Dim why As String = If(reason, "").Trim()
            If newStatus = "CANCELLED" AndAlso why = "" Then
                Return ServiceResult.Fail("Please tell us why you're cancelling this order.")
            End If

            Dim wasCancelled As Boolean = order.Status = "CANCELLED"

            ' One transaction: the status change, the payment reconciliation and the
            ' stock restore either all land or none do. A cancelled order left with a
            ' PENDING payment (the old REFUNDED-vs-PENDING mismatch) is what this fixes.
            Try
                Db.InTransaction(Function() As Boolean
                    _orders.UpdateStatus(orderId, newStatus)
                    ' Written inside the transaction, so the trail can never record a
                    ' move that then failed to commit.
                    ' The actor is recorded from who actually pressed the button. It used
                    ' to say "STUDIO" unconditionally, so a customer cancelling their own
                    ' order left a row claiming the studio had done it -- the one
                    ' distinction this column exists to make.
                    _orders.AppendHistory(orderId, order.Status, newStatus,
                                          If(isStore, ReceiptSources.Studio, ReceiptSources.Customer),
                                          why, Session.CurrentUser.Username)
                    If newStatus = "SHIPPED" OrElse newStatus = "DELIVERED" Then
                        _orders.UpdateShipping(orderId, "J&T Express", tracking, newStatus)
                    End If
                    If newStatus = "CANCELLED" Then
                        ' CANCELLED, never REFUNDED: cancelling an order is not a refund.
                        ' REFUNDED is reserved for an actual refund (none exist today).
                        ' A PAID status is likewise never rewritten here: money was
                        ' actually taken, so that record has to survive the cancellation.
                        ' The store keeps its ability to cancel the order itself.
                        If order.PaymentStatus <> "PAID" Then
                            _orders.UpdatePaymentStatus(orderId, "CANCELLED")
                        End If
                        _orders.SetCancelReason(orderId, why)
                        _orders.CancelPayments(orderId)
                        If Not wasCancelled Then _orders.RestoreOrderStock(orderId)
                    End If
                    Return True
                End Function)
            Catch ex As Exception
                Db.LogError("OrderService.UpdateOrderState", ex)
                Return ServiceResult.Fail("Could not update the order. Please try again.")
            End Try

            Dim shownTracking As String = If(String.IsNullOrWhiteSpace(tracking), order.TrackingNumber, tracking)
            Dim notif As New NotificationService()
            If newStatus = "CANCELLED" Then
                notif.Notify(order.UserId, "Order cancelled – " & order.OrderNumber,
                             "Your order has been cancelled. Reason: " & why, "ORDER", "my-orders")
            ElseIf newStatus = "DELIVERED" Then
                ' The parcel is with the customer and the 7-day window starts now, so
                ' they are told the same day rather than discovering it on a page poll.
                ' StampDeliveredNotified is set-once, so a re-read cannot restart the
                ' clock -- and the sweep refuses to auto-close an order that was never
                ' announced, which is what makes this stamp load-bearing rather than
                ' cosmetic.
                _orders.StampDeliveredNotified(orderId)
                ' Delivery is the point the studio's earning starts accruing: the parcel
                ' is out of the studio's hands, and it is not really earned until the
                ' customer confirms receipt. Idempotent, so re-marking changes nothing.
                _ledger.HoldForOrder(order, "Held: delivered and awaiting confirmation")
                notif.Notify(order.UserId, "Order delivered – " & order.OrderNumber,
                             "Your parcel has been delivered. Please confirm you received it in My Orders — " &
                             "and if anything is wrong, report a delivery problem from the same page within 7 days." &
                             If(shownTracking.Length > 0, " Tracking: " & shownTracking, ""),
                             "ORDER", "order-detail")
            Else
                notif.Notify(order.UserId, "Order " & newStatus.Replace("_", " ") & " – " & order.OrderNumber,
                             "Your order is now: " & newStatus.Replace("_", " ") & ". " &
                             If(shownTracking.Length > 0, "Tracking: " & shownTracking, ""), "ORDER", "my-orders")
            End If
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