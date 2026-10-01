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
            ' The "@v IS NULL" form is refused by PostgreSQL with 42P08 ("could not
            ' determine data type of parameter") when @v binds DBNull — it cannot infer
            ' the type from context. Branch in VB instead, so each statement has a
            ' concrete shape and no nullable placeholder inside the SQL text.
            Dim existing As Integer
            If variantId.HasValue Then
                existing = Db.ScalarInt(
                    "SELECT Id FROM CartItems WHERE CartId = @c AND ProductId = @p AND VariantId = @v LIMIT 1",
                    Db.P("@c", cartId), Db.P("@p", productId), Db.P("@v", variantId.Value))
            Else
                existing = Db.ScalarInt(
                    "SELECT Id FROM CartItems WHERE CartId = @c AND ProductId = @p AND VariantId IS NULL LIMIT 1",
                    Db.P("@c", cartId), Db.P("@p", productId))
            End If
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
            ' PostgreSQL has no MySQL-style "DELETE alias FROM ... JOIN"; scope by
            ' subquery instead.
            Db.Exec("DELETE FROM CartItems WHERE Id = @i AND CartId IN (SELECT Id FROM Cart WHERE UserId = @u)",
                    Db.P("@i", cartItemId), Db.P("@u", userId))
        End Sub

        Public Sub ClearCart(userId As Integer)
            Db.Exec("DELETE FROM CartItems WHERE CartId IN (SELECT Id FROM Cart WHERE UserId = @u)", Db.P("@u", userId))
        End Sub

        ' ----- Bundles ----------------------------------------------------------

        ''' <summary>
        ''' Active "N for ₱M" bundle rules for cart pricing. The deal is parsed from the
        ''' bundle name — "(4 for 100)" — so merchandising can reprice a bundle by
        ''' renaming it; bundles without that pattern (plain percentage promos) are
        ''' skipped by the cart pricing engine.
        ''' </summary>
        Public Function ListBundleGroups() As List(Of BundleGroup)
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT bi.BundleId, b.Name, bi.ProductId FROM BundleItems bi " &
                "JOIN Bundles b ON b.Id = bi.BundleId WHERE b.IsActive = TRUE ORDER BY bi.BundleId, bi.Id")
            Dim map As New Dictionary(Of Integer, BundleGroup)()
            For Each r As DataRow In rows
                Dim id As Integer = RowReader.AsInt(r, "BundleId")
                Dim g As BundleGroup = Nothing
                If Not map.TryGetValue(id, g) Then
                    Dim size As Integer = 0
                    Dim price As Decimal = 0D
                    If Not ParseBundleDeal(RowReader.AsStr(r, "Name"), size, price) Then Continue For
                    g = New BundleGroup With {
                        .BundleId = id, .Name = RowReader.AsStr(r, "Name"),
                        .GroupSize = size, .GroupPrice = price, .ProductIds = New List(Of Integer)()}
                    map(id) = g
                End If
                g.ProductIds.Add(RowReader.AsInt(r, "ProductId"))
            Next
            Return map.Values.ToList()
        End Function

        ''' <summary>"Stickers Bundle (4 for 100)" -> size 4, group price 100.</summary>
        Private Shared Function ParseBundleDeal(name As String, ByRef size As Integer, ByRef price As Decimal) As Boolean
            size = 0
            price = 0D
            If String.IsNullOrEmpty(name) Then Return False
            Dim m As System.Text.RegularExpressions.Match =
                System.Text.RegularExpressions.Regex.Match(name, "\((\d+)\s*for\s*([0-9.]+)\)",
                                                           System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            If Not m.Success Then Return False
            If Not Integer.TryParse(m.Groups(1).Value, size) Then Return False
            If Not Decimal.TryParse(m.Groups(2).Value, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, price) Then Return False
            Return size > 0 AndAlso price > 0D
        End Function

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