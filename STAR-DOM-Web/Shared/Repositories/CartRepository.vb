Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class CartRepository

        Public Function GetOrCreateCart(userId As Integer) As Integer
            Dim existing As Integer = Db.ScalarInt("SELECT Id FROM Cart WHERE UserId = @u LIMIT 1", Db.P("@u", userId))
            If existing > 0 Then Return existing
            Return Db.ExecIdentity("INSERT INTO Cart (UserId, CreatedAt, UpdatedAt) VALUES (@u, NOW(), NOW())", Db.P("@u", userId))
        End Function

        Public Function ListItems(userId As Integer) As List(Of CartItem)
            Return Db.Rows(
                "SELECT ci.*, p.Name AS ProductName, p.Sku AS ProductSku, p.IsActive, " &
                "p.StockQuantity, p.BasePrice, p.SalePrice, " &
                "(SELECT pi.ImageFile FROM ProductImages pi WHERE pi.ProductId = p.Id AND pi.IsPrimary = TRUE LIMIT 1) AS ImageFile " &
                "FROM CartItems ci " &
                "JOIN Cart c ON c.Id = ci.CartId " &
                "JOIN Products p ON p.Id = ci.ProductId " &
                "WHERE c.UserId = @u ORDER BY ci.AddedAt DESC",
                Db.P("@u", userId)).Select(Function(r) New CartItem With {
                .Id = RowReader.AsInt(r, "Id"), .CartId = RowReader.AsInt(r, "CartId"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .VariantId = RowReader.AsNullableInt(r, "VariantId"), .Quantity = RowReader.AsInt(r, "Quantity"),
                .AddedAt = RowReader.AsDate(r, "AddedAt"), .ProductName = RowReader.AsStr(r, "ProductName"),
                .ProductSku = RowReader.AsStr(r, "ProductSku"), .IsActive = RowReader.AsBool(r, "IsActive"),
                .StockQuantity = RowReader.AsInt(r, "StockQuantity"),
                .UnitPrice = Effective(RowReader.AsDec(r, "BasePrice"), RowReader.AsNullableDec(r, "SalePrice")),
                .ImageFile = RowReader.AsStr(r, "ImageFile")}).ToList()
        End Function

        Private Function Effective(baseP As Decimal, saleP As Decimal?) As Decimal
            If saleP.HasValue AndAlso saleP.Value > 0 Then Return saleP.Value
            Return baseP
        End Function

        Public Function GetCartCount(userId As Integer) As Integer
            Return Db.ScalarInt(
                "SELECT COALESCE(SUM(ci.Quantity), 0) FROM CartItems ci JOIN Cart c ON c.Id = ci.CartId WHERE c.UserId = @u",
                Db.P("@u", userId))
        End Function

        Public Function AddItem(userId As Integer, productId As Integer, variantId As Integer?, quantity As Integer) As String
            Dim product As DataRow = Db.Rows("SELECT IsActive, StockQuantity FROM Products WHERE Id = @id", Db.P("@id", productId)).FirstOrDefault()
            If product Is Nothing Then Return "Product no longer exists."
            If Not RowReader.AsBool(product, "IsActive") Then Return "This product is no longer available."
            If RowReader.AsInt(product, "StockQuantity") < quantity Then Return "Not enough stock for that quantity."

            Dim cartId As Integer = GetOrCreateCart(userId)
            Dim existing As Integer = Db.ScalarInt(
                "SELECT Id FROM CartItems WHERE CartId = @c AND ProductId = @p " &
                "AND ((@v IS NULL AND VariantId IS NULL) OR VariantId = @v) LIMIT 1",
                Db.P("@c", cartId), Db.P("@p", productId),
                Db.P("@v", If(variantId.HasValue, CObj(variantId.Value), DBNull.Value)))
            If existing > 0 Then
                Db.Exec("UPDATE CartItems SET Quantity = Quantity + @q WHERE Id = @id",
                        Db.P("@q", quantity), Db.P("@id", existing))
            Else
                Db.Exec("INSERT INTO CartItems (CartId, ProductId, VariantId, Quantity, AddedAt) VALUES (@c, @p, @v, @q, NOW())",
                        Db.P("@c", cartId), Db.P("@p", productId),
                        Db.P("@v", If(variantId.HasValue, CObj(variantId.Value), DBNull.Value)), Db.P("@q", quantity))
            End If
            Db.Exec("UPDATE Cart SET UpdatedAt = NOW() WHERE Id = @id", Db.P("@id", cartId))
            Return Nothing
        End Function

        Public Function UpdateQuantity(userId As Integer, cartItemId As Integer, quantity As Integer) As String
            If quantity < 1 Then
                RemoveItem(userId, cartItemId)
                Return Nothing
            End If
            Dim stock As Integer = Db.ScalarInt(
                "SELECT p.StockQuantity FROM CartItems ci JOIN Products p ON p.Id = ci.ProductId " &
                "JOIN Cart c ON c.Id = ci.CartId WHERE ci.Id = @i AND c.UserId = @u",
                Db.P("@i", cartItemId), Db.P("@u", userId))
            If stock < quantity Then Return "Not enough stock (max " & stock.ToString() & ")."
            Db.Exec("UPDATE CartItems SET Quantity = @q WHERE Id = @i", Db.P("@q", quantity), Db.P("@i", cartItemId))
            Return Nothing
        End Function

        Public Sub RemoveItem(userId As Integer, cartItemId As Integer)
            Db.Exec("DELETE ci FROM CartItems ci JOIN Cart c ON c.Id = ci.CartId WHERE ci.Id = @i AND c.UserId = @u",
                    Db.P("@i", cartItemId), Db.P("@u", userId))
        End Sub

        Public Sub ClearCart(userId As Integer)
            Db.Exec("DELETE ci FROM CartItems ci JOIN Cart c ON c.Id = ci.CartId WHERE c.UserId = @u", Db.P("@u", userId))
        End Sub

        ' ----- Wishlist ---------------------------------------------------------

        Public Function ListWishlist(userId As Integer) As List(Of WishlistItem)
            Return Db.Rows(
                "SELECT w.*, p.Name AS ProductName, p.StockQuantity, p.BasePrice, p.SalePrice, " &
                "(SELECT pi.ImageFile FROM ProductImages pi WHERE pi.ProductId = p.Id AND pi.IsPrimary = TRUE LIMIT 1) AS ImageFile " &
                "FROM WishlistItems w JOIN Products p ON p.Id = w.ProductId WHERE w.UserId = @u ORDER BY w.CreatedAt DESC",
                Db.P("@u", userId)).Select(Function(r) New WishlistItem With {
                .Id = RowReader.AsInt(r, "Id"), .UserId = RowReader.AsInt(r, "UserId"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt"), .ProductName = RowReader.AsStr(r, "ProductName"),
                .UnitPrice = Effective(RowReader.AsDec(r, "BasePrice"), RowReader.AsNullableDec(r, "SalePrice")),
                .ImageFile = RowReader.AsStr(r, "ImageFile"), .InStock = RowReader.AsInt(r, "StockQuantity") > 0}).ToList()
        End Function

        Public Function WishlistHas(userId As Integer, productId As Integer) As Boolean
            Return Db.ScalarInt("SELECT COUNT(*) FROM WishlistItems WHERE UserId = @u AND ProductId = @p",
                                Db.P("@u", userId), Db.P("@p", productId)) > 0
        End Function

        Public Sub AddWishlist(userId As Integer, productId As Integer)
            If Not WishlistHas(userId, productId) Then
                Db.Exec("INSERT INTO WishlistItems (UserId, ProductId, CreatedAt) VALUES (@u, @p, NOW())",
                        Db.P("@u", userId), Db.P("@p", productId))
            End If
        End Sub

        Public Sub RemoveWishlist(userId As Integer, productId As Integer)
            Db.Exec("DELETE FROM WishlistItems WHERE UserId = @u AND ProductId = @p",
                    Db.P("@u", userId), Db.P("@p", productId))
        End Sub

        Public Sub MoveWishlistToCart(userId As Integer, productId As Integer)
            AddItem(userId, productId, Nothing, 1)
            RemoveWishlist(userId, productId)
        End Sub

    End Class

End Namespace