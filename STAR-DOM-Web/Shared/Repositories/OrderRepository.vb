Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class OrderRepository

        Public Function CreateOrderRow(o As Order) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO Orders (OrderNumber, UserId, EventId, Status, Subtotal, DiscountAmount, ShippingFee, " &
                "TotalAmount, PaymentMethod, PaymentStatus, ShippingAddress, ContactPhone, Notes, CreatedAt, UpdatedAt) " &
                "VALUES (@num, @u, @e, @s, @sub, @d, @sf, @tot, @pm, 'PENDING', @addr, @ph, @notes, NOW(), NOW())",
                Db.P("@num", o.OrderNumber), Db.P("@u", o.UserId),
                Db.P("@e", If(o.EventId.HasValue, CObj(o.EventId.Value), DBNull.Value)),
                Db.P("@s", o.Status), Db.P("@sub", o.Subtotal), Db.P("@d", o.DiscountAmount),
                Db.P("@sf", o.ShippingFee), Db.P("@tot", o.TotalAmount), Db.P("@pm", o.PaymentMethod),
                Db.P("@addr", o.ShippingAddress), Db.P("@ph", o.ContactPhone), Db.P("@notes", o.Notes))
        End Function

        Public Sub AddOrderItem(orderId As Integer, item As OrderItem)
            Db.Exec(
                "INSERT INTO OrderItems (OrderId, ProductId, VariantId, Quantity, UnitPrice, LineTotal) " &
                "VALUES (@o, @p, @v, @q, @u, @t)",
                Db.P("@o", orderId), Db.P("@p", item.ProductId),
                Db.P("@v", If(item.VariantId.HasValue, CObj(item.VariantId.Value), DBNull.Value)),
                Db.P("@q", item.Quantity), Db.P("@u", item.UnitPrice), Db.P("@t", item.LineTotal))
        End Sub

        ''' <summary>
        ''' The order read used to be SELECT o.*, which silently started pulling the
        ''' proof-of-delivery BYTEA down with every list page the moment that column
        ''' existed -- the same trap the payment-channel QR images fell into. The
        ''' column list is spelled out here so that adding a blob column to Orders can
        ''' never again cost every order page its payload, and so an accidental
        ''' PodImageData in the list is a visible omission rather than a hidden cost.
        ''' </summary>
        ''' <remarks>
        ''' PodImageData is deliberately absent. GetPodImage fetches it on demand for the
        ''' one request that actually shows the image. PodMime is kept because it is a
        ''' short string and the page needs it to decide whether the POD exists at all.
        ''' </remarks>
        Private Const OrderSelect As String =
            "SELECT o.Id, " &
            "o.OrderNumber, " &
            "o.UserId, " &
            "o.EventId, " &
            "o.Status, " &
            "o.Subtotal, " &
            "o.DiscountAmount, " &
            "o.ShippingFee, " &
            "o.TotalAmount, " &
            "o.PaymentMethod, " &
            "o.PaymentStatus, " &
            "o.ShippingAddress, " &
            "o.ContactPhone, " &
            "o.Notes, " &
            "o.ShippingFeeConfirmed, " &
            "o.ShippingFeeConfirmedBy, " &
            "o.ShippingFeeConfirmedAt, " &
            "o.ReceivedAt, " &
            "o.CreatedAt, " &
            "o.UpdatedAt, " &
            "o.CancelReason, " &
            "o.CourierStatus, " &
            "o.PodReference, " &
            "o.PodMime, " &
            "o.PodCapturedAt, " &
            "o.PodCapturedBy, " &
            "o.DeliveredNotifiedAt, " &
            "o.ReminderSentAt, " &
            "o.ReceiptSource, " &
            "o.HoldReason, " &
            "o.HoldResolvedAt, " &
            "o.HoldResolution, " &
            "o.HoldIgRef, " &
            "o.RefundIgRef, " &
            "u.FullName AS CustomerName, u.Email AS CustomerEmail, " &
            "s.TrackingNumber AS TrackingNumber, " &
            "(SELECT COALESCE(SUM(oi.Quantity),0) FROM OrderItems oi WHERE oi.OrderId = o.Id) AS ItemCount " &
            "FROM Orders o LEFT JOIN Users u ON u.Id = o.UserId " &
            "LEFT JOIN Shipping s ON s.OrderId = o.Id "

        Public Function GetById(id As Integer) As Order
            Dim rows As List(Of DataRow) = Db.Rows(OrderSelect & "WHERE o.Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function GetByNumber(orderNumber As String) As Order
            Dim rows As List(Of DataRow) = Db.Rows(OrderSelect & "WHERE o.OrderNumber = @n", Db.P("@n", orderNumber))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function ListByUser(userId As Integer, Optional status As String = "", Optional page As Integer = 1, Optional pageSize As Integer = 0) As List(Of Order)
            Dim sql As String = OrderSelect & "WHERE o.UserId = @u "
            Dim ps As New List(Of NpgsqlParameter)() From {Db.P("@u", userId)}
            If Not String.IsNullOrEmpty(status) Then
                sql &= "AND o.Status = @s "
                ps.Add(Db.P("@s", status))
            End If
            sql &= "ORDER BY o.CreatedAt DESC"
            If pageSize > 0 Then
                sql &= " LIMIT @sz OFFSET @of"
                ps.Add(Db.P("@sz", pageSize))
                ps.Add(Db.P("@of", (Math.Max(page, 1) - 1) * pageSize))
            End If
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function ListAll(Optional search As String = "", Optional status As String = "", Optional page As Integer = 1, Optional pageSize As Integer = 0) As List(Of Order)
            Dim sql As String = OrderSelect & "WHERE 1=1 "
            Dim ps As New List(Of NpgsqlParameter)()
            If Not String.IsNullOrEmpty(search) Then
                sql &= "AND (o.OrderNumber LIKE @s OR u.FullName LIKE @s OR u.Email LIKE @s) "
                ps.Add(Db.P("@s", "%" & search & "%"))
            End If
            If Not String.IsNullOrEmpty(status) Then
                sql &= "AND o.Status = @st "
                ps.Add(Db.P("@st", status))
            End If
            sql &= "ORDER BY o.CreatedAt DESC"
            If pageSize > 0 Then
                sql &= " LIMIT @sz OFFSET @of"
                ps.Add(Db.P("@sz", pageSize))
                ps.Add(Db.P("@of", (Math.Max(page, 1) - 1) * pageSize))
            End If
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) Map(r)).ToList()
        End Function

        ''' <summary>Latest orders, bounded in SQL (used for dashboards instead of trimming full tables in memory).</summary>
        Public Function ListRecent(Optional limit As Integer = 8) As List(Of Order)
            Dim rows As List(Of DataRow) = Db.Rows(OrderSelect & "ORDER BY o.CreatedAt DESC LIMIT @l", Db.P("@l", limit))
            Return rows.Select(Function(r) Map(r)).ToList()
        End Function

        Public Function CountAll(Optional search As String = "", Optional status As String = "") As Integer
            Dim sql As String =
                "SELECT COUNT(*) FROM Orders o LEFT JOIN Users u ON u.Id = o.UserId WHERE 1=1 "
            Dim ps As New List(Of NpgsqlParameter)()
            If Not String.IsNullOrEmpty(search) Then
                sql &= "AND (o.OrderNumber LIKE @s OR u.FullName LIKE @s OR u.Email LIKE @s) "
                ps.Add(Db.P("@s", "%" & search & "%"))
            End If
            If Not String.IsNullOrEmpty(status) Then
                sql &= "AND o.Status = @st "
                ps.Add(Db.P("@st", status))
            End If
            Return Db.ScalarInt(sql, ps.ToArray())
        End Function

        Public Function CountByUser(userId As Integer, Optional status As String = "") As Integer
            Dim sql As String = "SELECT COUNT(*) FROM Orders WHERE UserId = @u "
            Dim ps As New List(Of NpgsqlParameter)() From {Db.P("@u", userId)}
            If Not String.IsNullOrEmpty(status) Then
                sql &= "AND Status = @s "
                ps.Add(Db.P("@s", status))
            End If
            Return Db.ScalarInt(sql, ps.ToArray())
        End Function

        Public Function GetItems(orderId As Integer) As List(Of OrderItem)
            Return Db.Rows(
                "SELECT oi.*, p.Name AS ProductName, p.Sku AS ProductSku, " &
                "(SELECT pi.ImageFile FROM ProductImages pi WHERE pi.ProductId = p.Id AND pi.IsPrimary = TRUE LIMIT 1) AS ImageFile " &
                "FROM OrderItems oi LEFT JOIN Products p ON p.Id = oi.ProductId WHERE oi.OrderId = @o ORDER BY oi.Id",
                Db.P("@o", orderId)).Select(Function(r) New OrderItem With {
                .Id = RowReader.AsInt(r, "Id"), .OrderId = RowReader.AsInt(r, "OrderId"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .VariantId = RowReader.AsNullableInt(r, "VariantId"), .Quantity = RowReader.AsInt(r, "Quantity"),
                .UnitPrice = RowReader.AsDec(r, "UnitPrice"), .LineTotal = RowReader.AsDec(r, "LineTotal"),
                .ProductName = RowReader.AsStr(r, "ProductName"), .ProductSku = RowReader.AsStr(r, "ProductSku"),
                .ImageFile = RowReader.AsStr(r, "ImageFile")}).ToList()
        End Function

        Public Sub UpdateStatus(orderId As Integer, status As String)
            Db.Exec("UPDATE Orders SET Status = @s, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@s", status), Db.P("@id", orderId))
        End Sub

        ''' <summary>
        ''' Customer-side confirmation that the parcel arrived. This is the only
        ''' writer of ReceivedAt, and it stamps the same NOW() it stores, so the
        ''' date on the row is the moment the customer clicked rather than a
        ''' separately-entered value that could disagree with it.
        ''' </summary>
        Public Sub MarkReceived(orderId As Integer)
            Db.Exec("UPDATE Orders SET Status = 'RECEIVED', ReceivedAt = NOW(), UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@id", orderId))
        End Sub

        ''' <summary>
        ''' Confirm a delivery order in the same statement that records the courier fee
        ''' it was confirmed against, so the two can never disagree: there is no window
        ''' in which Status = CONFIRMED but no fee was quoted.
        ''' </summary>
        Public Sub ConfirmWithShippingFee(orderId As Integer, fee As Decimal, userId As Integer)
            Db.Exec(
                "UPDATE Orders SET ShippingFee = @f, TotalAmount = GREATEST(Subtotal - DiscountAmount + @f, 0), " &
                "ShippingFeeConfirmed = TRUE, ShippingFeeConfirmedBy = @u, ShippingFeeConfirmedAt = NOW(), " &
                "Status = 'CONFIRMED', UpdatedAt = NOW() WHERE Id = @id",
                Db.P("@f", fee), Db.P("@u", userId), Db.P("@id", orderId))
        End Sub

        ''' <summary>
        ''' Keep Payments.Amount in step with a re-quoted total. A payment row is created
        ''' at checkout against the pre-fee total, so without this the ledger and the
        ''' order header would disagree about what was owed.
        ''' </summary>
        Public Sub UpdatePaymentAmountForOrder(orderId As Integer, amount As Decimal)
            Db.Exec("UPDATE Payments SET Amount = @a WHERE OrderId = @o AND Status <> 'PAID'",
                    Db.P("@a", amount), Db.P("@o", orderId))
        End Sub

        ''' <summary>Put sold units back on the shelf when an order is cancelled.</summary>
        Public Sub RestoreOrderStock(orderId As Integer)
            ' MySQL wrote this as a multi-table UPDATE (UPDATE ... JOIN ... SET).
            ' PostgreSQL has no such form; the joined relation moves into a FROM
            ' clause and the SET expressions are read against the old row.
            Db.Exec("UPDATE Products p " &
                    "SET StockQuantity = p.StockQuantity + oi.Quantity, " &
                    "SoldCount = GREATEST(p.SoldCount - oi.Quantity, 0) " &
                    "FROM OrderItems oi WHERE oi.ProductId = p.Id AND oi.OrderId = @o",
                    Db.P("@o", orderId))
        End Sub

        Public Sub UpdatePaymentStatus(orderId As Integer, paymentStatus As String)
            Db.Exec("UPDATE Orders SET PaymentStatus = @s, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@s", paymentStatus), Db.P("@id", orderId))
        End Sub

        ''' <summary>Records why an order was cancelled (customer or studio).</summary>
        Public Sub SetCancelReason(orderId As Integer, reason As String)
            Db.Exec("UPDATE Orders SET CancelReason = @r, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@r", If(reason, "")), Db.P("@id", orderId))
        End Sub

        ''' <summary>
        ''' Mark this order's paid Payment rows as refunded.
        ''' </summary>
        ''' <remarks>
        ''' REFUNDED was deliberately reserved for an actual refund when CANCELLED
        ''' stopped writing it ("nothing was ever refunded"); now that refunds really
        ''' are recorded, the payment rows have to move with the order. This is what
        ''' keeps an order from reading REFUNDED while its payment still says PAID --
        ''' and, because gross revenue is summed over `PaymentStatus = 'PAID'`, it is
        ''' also what takes a refunded sale OUT of the revenue figures instead of
        ''' leaving the studio's top line overstated by money it gave back.
        '''
        ''' Only PAID rows are touched: a refund is money going back, and there is
        ''' nothing to give back on a payment that never landed.
        ''' </remarks>
        Public Sub SetPaymentsRefunded(orderId As Integer)
            Db.Exec("UPDATE Payments SET Status = 'REFUNDED' WHERE OrderId = @o AND Status = 'PAID'",
                    Db.P("@o", orderId))
        End Sub

        ''' <summary>
        ''' Cancels this order's still-open Payment rows. When an order is cancelled
        ''' the payment that was never completed must not stay PENDING, or the order
        ''' reads as both cancelled and awaiting payment. Paid/refunded rows are left
        ''' alone because real money movement is not ours to rewrite here.
        ''' </summary>
        Public Sub CancelPayments(orderId As Integer)
            Db.Exec("UPDATE Payments SET Status = 'CANCELLED' WHERE OrderId = @o AND Status IN ('PENDING', 'SUBMITTED')",
                    Db.P("@o", orderId))
        End Sub

        ''' <summary>
        ''' Records which channel the customer chose. Checkout parks the method as
        ''' "PENDING" because the studio quotes the order first, so the real channel
        ''' is only known at payment time.
        ''' </summary>
        Public Sub SetPaymentMethod(orderId As Integer, method As String)
            Db.Exec("UPDATE Orders SET PaymentMethod = @m, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@m", method), Db.P("@id", orderId))
        End Sub

        ' ----- Payments ---------------------------------------------------------

        Public Function CreatePayment(p As Payment) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO Payments (OrderId, PaymentMethod, Amount, ReferenceNumber, Status, PaidAt, GatewayResponse, CreatedAt) " &
                "VALUES (@o, @m, @a, @r, @s, @pa, @g, NOW())",
                Db.P("@o", p.OrderId), Db.P("@m", p.PaymentMethod), Db.P("@a", p.Amount),
                Db.P("@r", p.ReferenceNumber), Db.P("@s", p.Status),
                Db.P("@pa", If(p.PaidAt.HasValue, CObj(p.PaidAt.Value), DBNull.Value)),
                Db.P("@g", p.GatewayResponse))
        End Function

        Public Function ListPayments(Optional search As String = "", Optional limit As Integer = 0) As List(Of Payment)
            Dim sql As String =
                "SELECT p.*, o.OrderNumber FROM Payments p JOIN Orders o ON o.Id = p.OrderId "
            Dim ps As New List(Of NpgsqlParameter)()
            If Not String.IsNullOrEmpty(search) Then
                sql &= "WHERE o.OrderNumber LIKE @s OR p.ReferenceNumber LIKE @s "
                ps.Add(Db.P("@s", "%" & search & "%"))
            End If
            sql &= "ORDER BY p.CreatedAt DESC"
            If limit > 0 Then
                sql &= " LIMIT @l"
                ps.Add(Db.P("@l", limit))
            End If
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) MapPayment(r)).ToList()
        End Function

        Public Function GetPaymentById(paymentId As Integer) As Payment
            Dim rows As List(Of DataRow) = Db.Rows("SELECT p.*, o.OrderNumber FROM Payments p JOIN Orders o ON o.Id = p.OrderId WHERE p.Id = @id",
                                                   Db.P("@id", paymentId))
            If rows.Count = 0 Then Return Nothing
            Return MapPayment(rows(0))
        End Function

        Public Function ListPaymentsByOrder(orderId As Integer) As List(Of Payment)
            Return Db.Rows("SELECT p.*, o.OrderNumber FROM Payments p JOIN Orders o ON o.Id = p.OrderId WHERE p.OrderId = @o ORDER BY p.CreatedAt DESC",
                           Db.P("@o", orderId)).Select(Function(r) MapPayment(r)).ToList()
        End Function

        Private Function MapPayment(r As DataRow) As Payment
            Return New Payment With {
                .Id = RowReader.AsInt(r, "Id"), .OrderId = RowReader.AsInt(r, "OrderId"),
                .PaymentMethod = RowReader.AsStr(r, "PaymentMethod"), .Amount = RowReader.AsDec(r, "Amount"),
                .ReferenceNumber = RowReader.AsStr(r, "ReferenceNumber"), .Status = RowReader.AsStr(r, "Status"),
                .PaidAt = RowReader.AsNullableDate(r, "PaidAt"), .GatewayResponse = RowReader.AsStr(r, "GatewayResponse"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt")}
        End Function

        Public Sub UpdatePaymentStatusById(paymentId As Integer, status As String, reference As String)
            Db.Exec("UPDATE Payments SET Status = @s, ReferenceNumber = @r, PaidAt = NOW() WHERE Id = @id",
                    Db.P("@s", status), Db.P("@r", reference), Db.P("@id", paymentId))
        End Sub

        ' ----- Shipping ---------------------------------------------------------

        Public Function CreateShipping(s As Shipping) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO Shipping (OrderId, Courier, TrackingNumber, Status, Address, ShippedAt, DeliveredAt) " &
                "VALUES (@o, @c, @t, @s, @a, @sh, @d)",
                Db.P("@o", s.OrderId), Db.P("@c", s.Courier), Db.P("@t", s.TrackingNumber),
                Db.P("@s", s.Status), Db.P("@a", s.Address),
                Db.P("@sh", If(s.ShippedAt.HasValue, CObj(s.ShippedAt.Value), DBNull.Value)),
                Db.P("@d", If(s.DeliveredAt.HasValue, CObj(s.DeliveredAt.Value), DBNull.Value)))
        End Function

        Public Function GetShipping(orderId As Integer) As Shipping
            Dim rows As List(Of DataRow) = Db.Rows("SELECT * FROM Shipping WHERE OrderId = @o LIMIT 1", Db.P("@o", orderId))
            If rows.Count = 0 Then Return Nothing
            Dim r As DataRow = rows(0)
            Return New Shipping With {
                .Id = RowReader.AsInt(r, "Id"), .OrderId = RowReader.AsInt(r, "OrderId"), .Courier = RowReader.AsStr(r, "Courier"),
                .TrackingNumber = RowReader.AsStr(r, "TrackingNumber"), .Status = RowReader.AsStr(r, "Status"),
                .Address = RowReader.AsStr(r, "Address"), .ShippedAt = RowReader.AsNullableDate(r, "ShippedAt"),
                .DeliveredAt = RowReader.AsNullableDate(r, "DeliveredAt")
            }
        End Function

        Public Sub UpdateShipping(orderId As Integer, courier As String, tracking As String, status As String)
            ' A later transition (e.g. SHIPPED -> DELIVERED) passes no tracking number;
            ' the empty value must never clobber the number booked at ship time.
            '
            ' DeliveredAt is CLEARED on a fresh SHIPPED. That single line is what makes
            ' a reship honest: DeliveredAt is the date the delivery sweep counts the
            ' 5/7-day window from, so leaving the old parcel's date on the row would
            ' let the sweep close the NEW parcel days before the customer could ever
            ' have received it.
            Db.Exec("UPDATE Shipping SET Courier = @c, " &
                    "TrackingNumber = CASE WHEN @t = '' THEN TrackingNumber ELSE @t END, Status = @s, " &
                    "ShippedAt = CASE WHEN @s = 'SHIPPED' THEN NOW() ELSE ShippedAt END, " &
                    "DeliveredAt = CASE WHEN @s = 'SHIPPED' THEN NULL " &
                    "WHEN @s = 'DELIVERED' THEN NOW() ELSE DeliveredAt END WHERE OrderId = @o",
                    Db.P("@c", courier), Db.P("@t", If(tracking, "")), Db.P("@s", status), Db.P("@o", orderId))
        End Sub

        ' ----- Reviews ----------------------------------------------------------

        Public Function UserReviewedProduct(userId As Integer, productId As Integer) As Boolean
            Return Db.ScalarInt("SELECT COUNT(*) FROM Reviews WHERE UserId = @u AND ProductId = @p",
                                Db.P("@u", userId), Db.P("@p", productId)) > 0
        End Function

        ''' <summary>
        ''' Id of the user's most recent non-cancelled order containing the product.
        ''' Reviews are only allowed for verified purchases, so this gates SubmitReview.
        ''' </summary>
        Public Function PurchaseOrderId(userId As Integer, productId As Integer) As Integer?
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT o.Id FROM OrderItems oi JOIN Orders o ON o.Id = oi.OrderId " &
                "WHERE o.UserId = @u AND oi.ProductId = @p AND o.Status <> 'CANCELLED' " &
                "ORDER BY o.CreatedAt DESC LIMIT 1",
                Db.P("@u", userId), Db.P("@p", productId))
            If rows.Count = 0 Then Return Nothing
            Return RowReader.AsInt(rows(0), "Id")
        End Function

        ''' <summary>
        ''' Ids of this user's orders containing the product that reached RECEIVED,
        ''' newest first. RECEIVED is written only by MarkReceived, i.e. by the customer,
        ''' so an id coming back from here is evidence the goods actually landed —
        ''' which is what a review is meant to attest to. Bounded by `limit` so the
        ''' caller cannot pull an unbounded order history into memory.
        ''' </summary>
        Public Function ReceivedPurchaseOrderIds(userId As Integer, productId As Integer,
                                                 Optional limit As Integer = 1) As List(Of Integer)
            Dim capped As Integer = If(limit < 1, 1, Math.Min(limit, 50))
            Return Db.Rows(
                "SELECT o.Id FROM OrderItems oi JOIN Orders o ON o.Id = oi.OrderId " &
                "WHERE o.UserId = @u AND oi.ProductId = @p AND o.Status = 'RECEIVED' " &
                "ORDER BY o.ReceivedAt DESC, o.CreatedAt DESC LIMIT @n",
                Db.P("@u", userId), Db.P("@p", productId), Db.P("@n", capped)
            ).Select(Function(r) RowReader.AsInt(r, "Id")).ToList()
        End Function

        Public Sub AddReview(userId As Integer, productId As Integer, orderId As Integer?, rating As Integer, comment As String)
            ' IsApproved stays TRUE on insert. The column is kept only so older rows
            ' keep their shape; nothing reads it any more.
            Db.Exec("INSERT INTO Reviews (ProductId, OrderId, UserId, Rating, Comment, IsApproved, CreatedAt) " &
                    "VALUES (@p, @o, @u, @r, @c, TRUE, NOW())",
                    Db.P("@p", productId), Db.P("@o", If(orderId.HasValue, CObj(orderId.Value), DBNull.Value)),
                    Db.P("@u", userId), Db.P("@r", rating), Db.P("@c", comment))
            RecalcRating(productId)
        End Sub

        Public Sub RecalcRating(productId As Integer)
            Db.Exec("UPDATE Products SET RatingAvg = (SELECT COALESCE(AVG(Rating),0) FROM Reviews WHERE ProductId = @p), " &
                    "RatingCount = (SELECT COUNT(*) FROM Reviews WHERE ProductId = @p) WHERE Id = @p", Db.P("@p", productId))
        End Sub

        ''' <summary>
        ''' Reviews shown on a product page. There is no approval gate any more: a
        ''' review can only be written by a customer who confirmed the parcel arrived,
        ''' which is the verification worth having, so every review is public on submit.
        ''' </summary>
        Public Function ListForProduct(productId As Integer) As List(Of Review)
            Return Db.Rows(
                "SELECT r.*, u.FullName AS CustomerName FROM Reviews r " &
                "JOIN Users u ON u.Id = r.UserId WHERE r.ProductId = @p ORDER BY r.CreatedAt DESC",
                Db.P("@p", productId)).Select(Function(r) MapReview(r)).ToList()
        End Function

        Public Function ListAllReviews(Optional search As String = "") As List(Of Review)
            Dim sql As String =
                "SELECT r.*, u.FullName AS CustomerName, p.Name AS ProductName FROM Reviews r " &
                "JOIN Users u ON u.Id = r.UserId LEFT JOIN Products p ON p.Id = r.ProductId "
            Dim ps As New List(Of NpgsqlParameter)()
            If Not String.IsNullOrEmpty(search) Then
                sql &= "WHERE p.Name LIKE @s OR u.FullName LIKE @s "
                ps.Add(Db.P("@s", "%" & search & "%"))
            End If
            sql &= "ORDER BY r.CreatedAt DESC"
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) MapReview(r)).ToList()
        End Function

        Private Function MapReview(r As DataRow) As Review
            Return New Review With {
                .Id = RowReader.AsInt(r, "Id"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .OrderId = RowReader.AsNullableInt(r, "OrderId"), .UserId = RowReader.AsInt(r, "UserId"),
                .Rating = RowReader.AsInt(r, "Rating"), .Comment = RowReader.AsStr(r, "Comment"),
                .IsApproved = RowReader.AsBool(r, "IsApproved"), .CreatedAt = RowReader.AsDate(r, "CreatedAt"),
                .ProductName = RowReader.AsStr(r, "ProductName"), .CustomerName = RowReader.AsStr(r, "CustomerName")
            }
        End Function

        ' ====================================================================
        ' Delivery evidence and the order audit trail
        ' ====================================================================
        ' Every method here writes to OrderStatusHistory as well as to Orders.
        ' That is not bookkeeping for its own sake: ReceiptSource on Orders says
        ' how an order closed, but only the history rows say who closed it, when,
        ' from what, and why. If the two ever diverge, the history is the record
        ' that was written at the time rather than a summary assembled later.

        ''' <summary>
        ''' Append one line to an order's delivery audit trail. Callers pass the
        ''' status the order was in beforehand; passing "" for a first entry is
        ''' normal.
        ''' </summary>
        Public Sub AppendHistory(orderId As Integer, fromStatus As String, toStatus As String,
                                 source As String, reason As String, changedBy As String)
            Db.Exec("INSERT INTO OrderStatusHistory (OrderId, FromStatus, ToStatus, Source, Reason, ChangedBy) " &
                    "VALUES (@o, @f, @t, @s, @r, @b)",
                    Db.P("@o", orderId), Db.P("@f", If(fromStatus, "")), Db.P("@t", If(toStatus, "")),
                    Db.P("@s", If(source, "")), Db.P("@r", If(reason, "")),
                    Db.P("@b", If(changedBy, "")))
        End Sub

        ''' <summary>Audit trail for one order, newest first.</summary>
        Public Function ListHistory(orderId As Integer) As List(Of OrderStatusEntry)
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT OrderId, FromStatus, ToStatus, Source, Reason, ChangedBy, CreatedAt " &
                "FROM OrderStatusHistory WHERE OrderId = @o ORDER BY CreatedAt DESC, Id DESC", Db.P("@o", orderId))
            Dim out As New List(Of OrderStatusEntry)()
            For Each r As DataRow In rows
                out.Add(New OrderStatusEntry With {
                    .OrderId = RowReader.AsInt(r, "OrderId"),
                    .FromStatus = RowReader.AsStr(r, "FromStatus"),
                    .ToStatus = RowReader.AsStr(r, "ToStatus"),
                    .Source = RowReader.AsStr(r, "Source"),
                    .Reason = RowReader.AsStr(r, "Reason"),
                    .ChangedBy = RowReader.AsStr(r, "ChangedBy"),
                    .CreatedAt = RowReader.AsDate(r, "CreatedAt")
                })
            Next
            Return out
        End Function

        ''' <summary>
        ''' The proof-of-delivery bytes for one order, or Nothing. This is the ONLY
        ''' query that reads PodImageData: it is called when a POD is actually being
        ''' displayed, never as part of a list. See the note on OrderSelect.
        ''' </summary>
        Public Function GetPodImage(orderId As Integer) As Byte()
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT PodImageData FROM Orders WHERE Id = @o AND PodImageData IS NOT NULL", Db.P("@o", orderId))
            If rows.Count = 0 Then Return Nothing
            Return RowReader.AsBytes(rows(0), "PodImageData")
        End Function

        ''' <summary>Record the IG reference for a dispute resolution.</summary>
        Public Sub SetHoldIgRef(orderId As Integer, igRef As String)
            Db.Exec("UPDATE Orders SET HoldIgRef = @r WHERE Id = @o", Db.P("@r", igRef), Db.P("@o", orderId))
        End Sub

        ''' <summary>Record the IG reference for a refund.</summary>
        Public Sub SetRefundIgRef(orderId As Integer, igRef As String)
            Db.Exec("UPDATE Orders SET RefundIgRef = @r WHERE Id = @o", Db.P("@r", igRef), Db.P("@o", orderId))
        End Sub

        ''' <summary>
        ''' Record the courier outcome and proof of delivery for an order, as
        ''' transcribed by the studio from the courier's own tracking page.
        ''' </summary>
        ''' <remarks>
        ''' The image is required. A courier status typed in without the screenshot
        ''' that supports it is an assertion, and an assertion is exactly what this
        ''' whole feature exists to stop being the only evidence for. An empty
        ''' reference is likewise rejected rather than stored as a blank, because a
        ''' blank POD reference is indistinguishable from a missing one later.
        ''' </remarks>
        Public Function SavePod(orderId As Integer, image As Byte(), mime As String,
                                reference As String, courierStatus As String,
                                capturedBy As String) As String
            If image Is Nothing OrElse image.Length = 0 Then
                Return "Attach the proof-of-delivery image before saving."
            End If
            If String.IsNullOrWhiteSpace(reference) Then
                Return "Enter the courier's reference or tracking number for this delivery."
            End If
            Db.Exec("UPDATE Orders SET CourierStatus = @cs, PodReference = @ref, PodImageData = @img, " &
                    "PodMime = @mime, PodCapturedAt = NOW(), PodCapturedBy = @by WHERE Id = @o",
                    Db.P("@cs", If(courierStatus, "")), Db.P("@ref", reference.Trim()),
                    Db.P("@img", image), Db.P("@mime", If(mime, "image/png")),
                    Db.P("@by", If(capturedBy, "")), Db.P("@o", orderId))
            Return ""
        End Function

        ''' <summary>Park an order in HELD with the customer's stated reason.</summary>
        Public Sub OpenHold(orderId As Integer, reason As String, changedBy As String)
            Db.Exec("UPDATE Orders SET Status = 'HELD', HoldReason = @r, " &
                    "HoldResolvedAt = NULL, HoldResolution = '' WHERE Id = @o",
                    Db.P("@r", If(reason, "")), Db.P("@o", orderId))
        End Sub

        ''' <summary>
        ''' Close out a dispute. Resolution is one of RECEIVED / REFUNDED / RESHIP
        ''' and is recorded verbatim: the sweep and the order page both read it, and
        ''' they must not have to guess what the studio meant.
        ''' </summary>
        Public Sub ResolveHold(orderId As Integer, resolution As String, newStatus As String,
                               changedBy As String)
            Db.Exec("UPDATE Orders SET Status = @ns, HoldResolvedAt = NOW(), HoldResolution = @res WHERE Id = @o",
                    Db.P("@ns", newStatus), Db.P("@res", resolution), Db.P("@o", orderId))
        End Sub

        ''' <summary>
        ''' Note that the customer has been told the parcel landed, and that the
        ''' single follow-up nudge has gone out. Both are set-once: the COALESCE
        ''' guard means a retried page render cannot push the reminder window
        ''' forward and let an order sit unclosed for ever.
        ''' </summary>
        Public Sub StampDeliveredNotified(orderId As Integer)
            Db.Exec("UPDATE Orders SET DeliveredNotifiedAt = COALESCE(DeliveredNotifiedAt, NOW()) WHERE Id = @o",
                    Db.P("@o", orderId))
        End Sub

        Public Sub StampReminderSent(orderId As Integer)
            Db.Exec("UPDATE Orders SET ReminderSentAt = COALESCE(ReminderSentAt, NOW()) WHERE Id = @o",
                    Db.P("@o", orderId))
        End Sub

        ''' <summary>
        ''' Start a fresh delivery cycle: clear the two notification stamps so the
        ''' next DELIVERED announces itself again and earns its own reminder before the
        ''' sweep may close it. Used when a disputed parcel is reshipped.
        ''' </summary>
        ''' <remarks>
        ''' Both stamps are written with COALESCE (set-once), which is what stops a
        ''' re-render from restarting the clock. That same guard is exactly why they
        ''' have to be cleared explicitly here: the second parcel is a new delivery and
        ''' deserves its own notification, not the first parcel's.
        ''' </remarks>
        Public Sub ResetDeliveryCycle(orderId As Integer)
            Db.Exec("UPDATE Orders SET DeliveredNotifiedAt = NULL, ReminderSentAt = NULL, " &
                    "UpdatedAt = NOW() WHERE Id = @o", Db.P("@o", orderId))
        End Sub

        ''' <summary>
        ''' True when the sweep has already flagged this order as past the delivery
        ''' window with nothing to close it on. Read from the audit trail rather than a
        ''' dedicated column, because the flag IS an audit fact -- and it is what keeps
        ''' a daily job from sending the studio the same "order needs attention" note
        ''' every morning until somebody acts on it.
        ''' </summary>
        Public Function HasSweepFlag(orderId As Integer) As Boolean
            Return Db.ScalarInt(
                "SELECT COUNT(*) FROM OrderStatusHistory WHERE OrderId = @o AND Source = @s AND ToStatus = @t",
                Db.P("@o", orderId), Db.P("@s", ReceiptSources.AutoSweep),
                Db.P("@t", OrderStatuses.Delivered)) > 0
        End Function

        ''' <summary>
        ''' Every order sitting in DELIVERED, with the inputs the sweep decides on:
        ''' when the courier hand-off was recorded, whether the customer was told,
        ''' whether the nudge went out, and whether any proof of delivery exists.
        ''' </summary>
        ''' <remarks>
        ''' Deliberately not OrderSelect: the list must not drag Orders.PodImageData
        ''' (the POD blob) into memory, so it asks only whether the column has bytes --
        ''' `octet_length` answers that on the server, as a boolean, for any size of
        ''' image. HELD orders are excluded by the WHERE clause, which is what makes a
        ''' dispute actually stop the automatic close rather than only recording a
        ''' complaint.
        '''
        ''' The age is taken from Shipping.DeliveredAt with Orders.UpdatedAt as the
        ''' fallback: Shipping is where the courier hand-off lives, but an order
        ''' delivered before the Shipping row existed still has to be sweepable
        ''' rather than stranded in DELIVERED for ever.
        ''' </remarks>
        Public Function ListDeliveredForSweep() As List(Of DeliverySweepCandidate)
            Return Db.Rows(
                "SELECT o.Id AS OrderId, o.OrderNumber, o.UserId, o.CourierStatus, " &
                "o.DeliveredNotifiedAt, o.ReminderSentAt, " &
                "COALESCE(s.DeliveredAt, o.UpdatedAt) AS HandedOverAt, " &
                "(o.PodImageData IS NOT NULL AND octet_length(o.PodImageData) > 0) AS HasPod " &
                "FROM Orders o LEFT JOIN Shipping s ON s.OrderId = o.Id " &
                "WHERE o.Status = 'DELIVERED' ORDER BY HandedOverAt"
            ).Select(Function(r) New DeliverySweepCandidate With {
                .OrderId = RowReader.AsInt(r, "OrderId"),
                .OrderNumber = RowReader.AsStr(r, "OrderNumber"),
                .UserId = RowReader.AsInt(r, "UserId"),
                .CourierStatus = RowReader.AsStr(r, "CourierStatus"),
                .DeliveredNotifiedAt = RowReader.AsNullableDate(r, "DeliveredNotifiedAt"),
                .ReminderSentAt = RowReader.AsNullableDate(r, "ReminderSentAt"),
                .DeliveredAt = RowReader.AsNullableDate(r, "HandedOverAt"),
                .HasPod = RowReader.AsBool(r, "HasPod")
            }).ToList()
        End Function

        ''' <summary>Attach how this order reached RECEIVED, without moving it there.</summary>
        Public Sub SetReceiptSource(orderId As Integer, source As String)
            Db.Exec("UPDATE Orders SET ReceiptSource = @s WHERE Id = @o", Db.P("@s", source), Db.P("@o", orderId))
        End Sub

        Private Function Map(r As DataRow) As Order
            ' PodImageData is deliberately absent here. OrderSelect no longer fetches
            ' the column, so it could not be mapped even if this tried; GetPodImage is
            ' the only path that reads the bytes.
            Return New Order With {
                .Id = RowReader.AsInt(r, "Id"), .OrderNumber = RowReader.AsStr(r, "OrderNumber"),
                .UserId = RowReader.AsInt(r, "UserId"), .EventId = RowReader.AsNullableInt(r, "EventId"),
                .Status = RowReader.AsStr(r, "Status"), .Subtotal = RowReader.AsDec(r, "Subtotal"),
                .DiscountAmount = RowReader.AsDec(r, "DiscountAmount"), .ShippingFee = RowReader.AsDec(r, "ShippingFee"),
                .TotalAmount = RowReader.AsDec(r, "TotalAmount"), .PaymentMethod = RowReader.AsStr(r, "PaymentMethod"),
                .PaymentStatus = RowReader.AsStr(r, "PaymentStatus"), .ShippingAddress = RowReader.AsStr(r, "ShippingAddress"),
                .ContactPhone = RowReader.AsStr(r, "ContactPhone"), .Notes = RowReader.AsStr(r, "Notes"),
                .CancelReason = RowReader.AsStr(r, "CancelReason"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt"), .UpdatedAt = RowReader.AsDate(r, "UpdatedAt"),
                .CustomerName = RowReader.AsStr(r, "CustomerName"), .CustomerEmail = RowReader.AsStr(r, "CustomerEmail"),
                .ItemCount = RowReader.AsInt(r, "ItemCount"),
                .ShippingFeeConfirmed = RowReader.AsBool(r, "ShippingFeeConfirmed"),
                .ShippingFeeConfirmedBy = RowReader.AsNullableInt(r, "ShippingFeeConfirmedBy"),
                .ShippingFeeConfirmedAt = RowReader.AsNullableDate(r, "ShippingFeeConfirmedAt"),
                .ReceivedAt = RowReader.AsNullableDate(r, "ReceivedAt"),
                .TrackingNumber = RowReader.AsStr(r, "TrackingNumber"),
                .CourierStatus = RowReader.AsStr(r, "CourierStatus"),
                .PodReference = RowReader.AsStr(r, "PodReference"),
                .PodMime = RowReader.AsStr(r, "PodMime"),
                .PodCapturedAt = RowReader.AsNullableDate(r, "PodCapturedAt"),
                .PodCapturedBy = RowReader.AsStr(r, "PodCapturedBy"),
                .DeliveredNotifiedAt = RowReader.AsNullableDate(r, "DeliveredNotifiedAt"),
                .ReminderSentAt = RowReader.AsNullableDate(r, "ReminderSentAt"),
                .ReceiptSource = RowReader.AsStr(r, "ReceiptSource"),
                .HoldReason = RowReader.AsStr(r, "HoldReason"),
                .HoldResolvedAt = RowReader.AsNullableDate(r, "HoldResolvedAt"),
                .HoldResolution = RowReader.AsStr(r, "HoldResolution"),
                .HoldIgRef = RowReader.AsStr(r, "HoldIgRef"),
                .RefundIgRef = RowReader.AsStr(r, "RefundIgRef")
            }
        End Function

    End Class

End Namespace