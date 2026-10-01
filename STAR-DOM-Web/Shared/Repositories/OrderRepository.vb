Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class OrderRepository

        Public Function CreateOrderRow(o As Order) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO Orders (OrderNumber, UserId, EventId, Status, Subtotal, DiscountAmount, ShippingFee, " &
                "TotalAmount, PaymentMethod, PaymentStatus, ShippingAddress, ContactPhone, Notes, Fulfillment, PickupEventId, CreatedAt, UpdatedAt) " &
                "VALUES (@num, @u, @e, @s, @sub, @d, @sf, @tot, @pm, 'PENDING', @addr, @ph, @notes, @ful, @pk, NOW(), NOW())",
                Db.P("@num", o.OrderNumber), Db.P("@u", o.UserId),
                Db.P("@e", If(o.EventId.HasValue, CObj(o.EventId.Value), DBNull.Value)),
                Db.P("@s", o.Status), Db.P("@sub", o.Subtotal), Db.P("@d", o.DiscountAmount),
                Db.P("@sf", o.ShippingFee), Db.P("@tot", o.TotalAmount), Db.P("@pm", o.PaymentMethod),
                Db.P("@addr", o.ShippingAddress), Db.P("@ph", o.ContactPhone), Db.P("@notes", o.Notes),
                Db.P("@ful", If(String.IsNullOrEmpty(o.Fulfillment), "DELIVERY", o.Fulfillment)),
                Db.P("@pk", If(o.PickupEventId.HasValue, CObj(o.PickupEventId.Value), DBNull.Value)))
        End Function

        Public Sub AddOrderItem(orderId As Integer, item As OrderItem)
            Db.Exec(
                "INSERT INTO OrderItems (OrderId, ProductId, VariantId, Quantity, UnitPrice, LineTotal) " &
                "VALUES (@o, @p, @v, @q, @u, @t)",
                Db.P("@o", orderId), Db.P("@p", item.ProductId),
                Db.P("@v", If(item.VariantId.HasValue, CObj(item.VariantId.Value), DBNull.Value)),
                Db.P("@q", item.Quantity), Db.P("@u", item.UnitPrice), Db.P("@t", item.LineTotal))
        End Sub

        Private Const OrderSelect As String =
            "SELECT o.*, u.FullName AS CustomerName, u.Email AS CustomerEmail, " &
            "s.TrackingNumber AS TrackingNumber, pe.Name AS PickupEventName, " &
            "(pe.OpenTime || ' - ' || pe.CloseTime) AS PickupHoursText, " &
            "(SELECT COALESCE(SUM(oi.Quantity),0) FROM OrderItems oi WHERE oi.OrderId = o.Id) AS ItemCount " &
            "FROM Orders o LEFT JOIN Users u ON u.Id = o.UserId " &
            "LEFT JOIN Shipping s ON s.OrderId = o.Id " &
            "LEFT JOIN PopUpEvents pe ON pe.Id = o.PickupEventId "

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

        ''' <summary>
        ''' Record one side of the pick-up claim. A pick-up order only completes when
        ''' BOTH flags are set — the service layer turns the pair into DELIVERED.
        ''' </summary>
        Public Sub SetPickupConfirm(orderId As Integer, customerSide As Boolean, confirmed As Boolean)
            If customerSide Then
                Db.Exec("UPDATE Orders SET PickupCustomerConfirmed = @c, UpdatedAt = NOW() WHERE Id = @id",
                        Db.P("@c", confirmed), Db.P("@id", orderId))
            Else
                Db.Exec("UPDATE Orders SET PickupMerchantConfirmed = @c, UpdatedAt = NOW() WHERE Id = @id",
                        Db.P("@c", confirmed), Db.P("@id", orderId))
            End If
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
            Db.Exec("UPDATE Shipping SET Courier = @c, " &
                    "TrackingNumber = CASE WHEN @t = '' THEN TrackingNumber ELSE @t END, Status = @s, " &
                    "ShippedAt = CASE WHEN @s = 'SHIPPED' AND ShippedAt IS NULL THEN NOW() ELSE ShippedAt END, " &
                    "DeliveredAt = CASE WHEN @s = 'DELIVERED' THEN NOW() ELSE DeliveredAt END WHERE OrderId = @o",
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

        Public Sub AddReview(userId As Integer, productId As Integer, orderId As Integer?, rating As Integer, comment As String)
            Db.Exec("INSERT INTO Reviews (ProductId, OrderId, UserId, Rating, Comment, IsApproved, CreatedAt) " &
                    "VALUES (@p, @o, @u, @r, @c, 1, NOW())",
                    Db.P("@p", productId), Db.P("@o", If(orderId.HasValue, CObj(orderId.Value), DBNull.Value)),
                    Db.P("@u", userId), Db.P("@r", rating), Db.P("@c", comment))
            RecalcRating(productId)
        End Sub

        Public Sub RecalcRating(productId As Integer)
            Db.Exec("UPDATE Products SET RatingAvg = (SELECT COALESCE(AVG(Rating),0) FROM Reviews WHERE ProductId = @p), " &
                    "RatingCount = (SELECT COUNT(*) FROM Reviews WHERE ProductId = @p) WHERE Id = @p", Db.P("@p", productId))
        End Sub

        Public Function ListForProduct(productId As Integer) As List(Of Review)
            Return Db.Rows(
                "SELECT r.*, u.FullName AS CustomerName FROM Reviews r " &
                "JOIN Users u ON u.Id = r.UserId WHERE r.ProductId = @p AND r.IsApproved = TRUE ORDER BY r.CreatedAt DESC",
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

        Public Sub SetReviewApproved(reviewId As Integer, approved As Boolean)
            Db.Exec("UPDATE Reviews SET IsApproved = @a WHERE Id = @id",
                    Db.P("@a", approved), Db.P("@id", reviewId))
        End Sub

        Public Sub DeleteReview(reviewId As Integer)
            Dim productId As Integer = Db.ScalarInt("SELECT ProductId FROM Reviews WHERE Id = @id", Db.P("@id", reviewId))
            Db.Exec("DELETE FROM Reviews WHERE Id = @id", Db.P("@id", reviewId))
            If productId > 0 Then RecalcRating(productId)
        End Sub

        Private Function MapReview(r As DataRow) As Review
            Return New Review With {
                .Id = RowReader.AsInt(r, "Id"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .OrderId = RowReader.AsNullableInt(r, "OrderId"), .UserId = RowReader.AsInt(r, "UserId"),
                .Rating = RowReader.AsInt(r, "Rating"), .Comment = RowReader.AsStr(r, "Comment"),
                .IsApproved = RowReader.AsBool(r, "IsApproved"), .CreatedAt = RowReader.AsDate(r, "CreatedAt"),
                .ProductName = RowReader.AsStr(r, "ProductName"), .CustomerName = RowReader.AsStr(r, "CustomerName")
            }
        End Function

        Private Function Map(r As DataRow) As Order
            Return New Order With {
                .Id = RowReader.AsInt(r, "Id"), .OrderNumber = RowReader.AsStr(r, "OrderNumber"),
                .UserId = RowReader.AsInt(r, "UserId"), .EventId = RowReader.AsNullableInt(r, "EventId"),
                .Status = RowReader.AsStr(r, "Status"), .Subtotal = RowReader.AsDec(r, "Subtotal"),
                .DiscountAmount = RowReader.AsDec(r, "DiscountAmount"), .ShippingFee = RowReader.AsDec(r, "ShippingFee"),
                .TotalAmount = RowReader.AsDec(r, "TotalAmount"), .PaymentMethod = RowReader.AsStr(r, "PaymentMethod"),
                .PaymentStatus = RowReader.AsStr(r, "PaymentStatus"), .ShippingAddress = RowReader.AsStr(r, "ShippingAddress"),
                .ContactPhone = RowReader.AsStr(r, "ContactPhone"), .Notes = RowReader.AsStr(r, "Notes"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt"), .UpdatedAt = RowReader.AsDate(r, "UpdatedAt"),
                .CustomerName = RowReader.AsStr(r, "CustomerName"), .CustomerEmail = RowReader.AsStr(r, "CustomerEmail"),
                .ItemCount = RowReader.AsInt(r, "ItemCount"),
                .Fulfillment = RowReader.AsStr(r, "Fulfillment", "DELIVERY"),
                .PickupEventId = RowReader.AsNullableInt(r, "PickupEventId"),
                .PickupCustomerConfirmed = RowReader.AsBool(r, "PickupCustomerConfirmed"),
                .PickupMerchantConfirmed = RowReader.AsBool(r, "PickupMerchantConfirmed"),
                .TrackingNumber = RowReader.AsStr(r, "TrackingNumber"),
                .PickupEventName = RowReader.AsStr(r, "PickupEventName"),
                .PickupHoursText = RowReader.AsStr(r, "PickupHoursText")
            }
        End Function

    End Class

End Namespace