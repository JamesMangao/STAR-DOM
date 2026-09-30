Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class ProductRepository

        Private Const BaseSelect As String =
            "SELECT p.*, c.Name AS CategoryName, u.FullName AS MerchantName, " &
            "(SELECT pi.ImageFile FROM ProductImages pi WHERE pi.ProductId = p.Id AND pi.IsPrimary = TRUE LIMIT 1) AS PrimaryImageFile " &
            "FROM Products p " &
            "LEFT JOIN Categories c ON c.Id = p.CategoryId " &
            "LEFT JOIN Users u ON u.Id = p.MerchantId "

        Public Function ListActive(Optional categoryId As Integer = 0, Optional search As String = "") As List(Of Product)
            Dim sql As String = BaseSelect & "WHERE p.IsActive = TRUE "
            Dim ps As New List(Of NpgsqlParameter)()
            If categoryId > 0 Then
                sql &= "AND p.CategoryId = @cat "
                ps.Add(Db.P("@cat", categoryId))
            End If
            If search.Length > 0 Then
                sql &= "AND (p.Name LIKE @s OR p.BrandName LIKE @s OR p.Sku LIKE @s OR p.Description LIKE @s) "
                ps.Add(Db.P("@s", "%" & search & "%"))
            End If
            sql &= "ORDER BY p.IsFeatured DESC, p.SoldCount DESC, p.Name"
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function ListFeatured(Optional limit As Integer = 8) As List(Of Product)
            Return Db.Rows(BaseSelect & "WHERE p.IsActive = TRUE AND p.IsFeatured = TRUE ORDER BY p.SoldCount DESC LIMIT @l",
                           Db.P("@l", limit)).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function ListByMerchant(merchantId As Integer, Optional search As String = "") As List(Of Product)
            Dim sql As String = BaseSelect & "WHERE p.MerchantId = @m "
            Dim ps As New List(Of NpgsqlParameter)() From {Db.P("@m", merchantId)}
            If search.Length > 0 Then
                sql &= "AND (p.Name LIKE @s OR p.Sku LIKE @s) "
                ps.Add(Db.P("@s", "%" & search & "%"))
            End If
            sql &= "ORDER BY p.CreatedAt DESC"
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function GetById(id As Integer) As Product
            Dim rows As List(Of DataRow) = Db.Rows(BaseSelect & "WHERE p.Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function Create(p As Product) As Integer
Return Db.ExecIdentity(
                "INSERT INTO Products (CategoryId, MerchantId, Name, Description, BasePrice, SalePrice, StockQuantity, " &
                "LowStockThreshold, Sku, BrandName, IsActive, IsFeatured, IsBoothExclusive, IsEventExclusive, BadgeLabel, " &
                "MaterialDetails, CreatedAt, UpdatedAt) " &
                "VALUES (@cat, @m, @n, @d, @bp, @sp, @q, @lt, @sku, @b, @a, @f, @be, @ee, @bl, @md, NOW(), NOW())",
                Db.P("@cat", p.CategoryId), Db.P("@m", p.MerchantId), Db.P("@n", p.Name),
                Db.P("@d", p.Description), Db.P("@bp", p.BasePrice),
                Db.P("@sp", If(p.SalePrice.HasValue, CObj(p.SalePrice.Value), DBNull.Value)),
                Db.P("@q", p.StockQuantity), Db.P("@lt", p.LowStockThreshold), Db.P("@sku", p.Sku),
                Db.P("@b", p.BrandName), Db.P("@a", p.IsActive), Db.P("@f", p.IsFeatured),
                Db.P("@be", p.IsBoothExclusive), Db.P("@ee", p.IsEventExclusive),
                Db.P("@bl", p.BadgeLabel), Db.P("@md", p.MaterialDetails))
        End Function

        Public Sub Update(p As Product)
            Db.Exec(
                "UPDATE Products SET CategoryId = @c, Name = @n, Description = @d, BasePrice = @bp, SalePrice = @sp, " &
                "StockQuantity = @q, LowStockThreshold = @lt, Sku = @sku, BrandName = @b, IsActive = @a, " &
                "IsFeatured = @f, IsBoothExclusive = @be, IsEventExclusive = @ee, BadgeLabel = @bl, MaterialDetails = @md, " &
                "UpdatedAt = NOW() WHERE Id = @id",
                Db.P("@c", p.CategoryId), Db.P("@n", p.Name), Db.P("@d", p.Description), Db.P("@bp", p.BasePrice),
                Db.P("@sp", If(p.SalePrice.HasValue, CObj(p.SalePrice.Value), DBNull.Value)),
                Db.P("@q", p.StockQuantity), Db.P("@lt", p.LowStockThreshold), Db.P("@sku", p.Sku),
                Db.P("@b", p.BrandName), Db.P("@a", p.IsActive), Db.P("@f", p.IsFeatured),
                Db.P("@be", p.IsBoothExclusive), Db.P("@ee", p.IsEventExclusive),
                Db.P("@bl", p.BadgeLabel), Db.P("@md", p.MaterialDetails), Db.P("@id", p.Id))
        End Sub

        Public Function Delete(id As Integer) As String
            Dim inOrders As Integer = Db.ScalarInt("SELECT COUNT(*) FROM OrderItems WHERE ProductId = @id", Db.P("@id", id))
            If inOrders > 0 Then
                ' Keep history: soft-delete instead of destroying order references.
                Db.Exec("UPDATE Products SET IsActive = FALSE WHERE Id = @id", Db.P("@id", id))
                Return Nothing
            End If
            Db.Exec("DELETE FROM ProductImages WHERE ProductId = @id", Db.P("@id", id))
            Db.Exec("DELETE FROM ProductVariants WHERE ProductId = @id", Db.P("@id", id))
            Db.Exec("DELETE FROM CartItems WHERE ProductId = @id", Db.P("@id", id))
            Db.Exec("DELETE FROM WishlistItems WHERE ProductId = @id", Db.P("@id", id))
            Db.Exec("DELETE FROM Products WHERE Id = @id", Db.P("@id", id))
            Return Nothing
        End Function

        Public Sub SetStock(productId As Integer, quantity As Integer)
            Db.Exec("UPDATE Products SET StockQuantity = @q, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@q", quantity), Db.P("@id", productId))
        End Sub

        Public Sub BumpSold(productId As Integer, quantity As Integer)
            Db.Exec("UPDATE Products SET SoldCount = SoldCount + @q, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@q", quantity), Db.P("@id", productId))
        End Sub

        Public Function LowStock(merchantId As Integer) As List(Of Product)
            Return Db.Rows(BaseSelect & "WHERE p.MerchantId = @m AND p.IsActive = TRUE AND p.StockQuantity <= p.LowStockThreshold " &
                           "ORDER BY p.StockQuantity ASC", Db.P("@m", merchantId)).Select(Function(r) Map(r)).ToList()
        End Function

        ' ----- Images -----------------------------------------------------------

        Public Function ListImages(productId As Integer) As List(Of ProductImage)
            Return Db.Rows("SELECT * FROM ProductImages WHERE ProductId = @id ORDER BY SortOrder, Id",
                           Db.P("@id", productId)).Select(Function(r) New ProductImage With {
                .Id = RowReader.AsInt(r, "Id"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .ImageFile = RowReader.AsStr(r, "ImageFile"), .IsPrimary = RowReader.AsBool(r, "IsPrimary"),
                .SortOrder = RowReader.AsInt(r, "SortOrder")}).ToList()
        End Function

        Public Sub AddImage(productId As Integer, imageFile As String, isPrimary As Boolean, sortOrder As Integer)
            If isPrimary Then Db.Exec("UPDATE ProductImages SET IsPrimary = FALSE WHERE ProductId = @id", Db.P("@id", productId))
            Db.Exec("INSERT INTO ProductImages (ProductId, ImageFile, IsPrimary, SortOrder) VALUES (@p, @f, @pr, @s)",
                    Db.P("@p", productId), Db.P("@f", imageFile), Db.P("@pr", isPrimary), Db.P("@s", sortOrder))
        End Sub

        Public Sub DeleteImage(imageId As Integer)
            Db.Exec("DELETE FROM ProductImages WHERE Id = @id", Db.P("@id", imageId))
        End Sub

        Public Sub DeleteAllImages(productId As Integer)
            Db.Exec("DELETE FROM ProductImages WHERE ProductId = @id", Db.P("@id", productId))
        End Sub

        ' ----- Variants ---------------------------------------------------------

        Public Function ListVariants(productId As Integer) As List(Of ProductVariant)
            Return Db.Rows("SELECT * FROM ProductVariants WHERE ProductId = @id ORDER BY Id",
                           Db.P("@id", productId)).Select(Function(r) New ProductVariant With {
                .Id = RowReader.AsInt(r, "Id"), .ProductId = RowReader.AsInt(r, "ProductId"), .Name = RowReader.AsStr(r, "Name"),
                .Sku = RowReader.AsStr(r, "Sku"), .PriceAdjustment = RowReader.AsDec(r, "PriceAdjustment"),
                .StockQuantity = RowReader.AsInt(r, "StockQuantity"), .IsActive = RowReader.AsBool(r, "IsActive")}).ToList()
        End Function

        Public Sub AddVariant(productId As Integer, name As String, sku As String, priceAdjustment As Decimal, stock As Integer)
            Db.Exec("INSERT INTO ProductVariants (ProductId, Name, Sku, PriceAdjustment, StockQuantity, IsActive) " &
                    "VALUES (@p, @n, @s, @pa, @q, 1)",
                    Db.P("@p", productId), Db.P("@n", name), Db.P("@s", sku),
                    Db.P("@pa", priceAdjustment), Db.P("@q", stock))
        End Sub

        Public Sub UpdateVariant(id As Integer, name As String, sku As String, priceAdjustment As Decimal, stock As Integer)
            Db.Exec("UPDATE ProductVariants SET Name = @n, Sku = @s, PriceAdjustment = @pa, StockQuantity = @q WHERE Id = @id",
                    Db.P("@n", name), Db.P("@s", sku), Db.P("@pa", priceAdjustment), Db.P("@q", stock), Db.P("@id", id))
        End Sub

        Public Sub DeleteVariant(id As Integer)
            Db.Exec("DELETE FROM ProductVariants WHERE Id = @id", Db.P("@id", id))
        End Sub

        ' ----- Bundles ----------------------------------------------------------

        Public Function ListBundles() As List(Of Bundle)
            Return Db.Rows("SELECT * FROM Bundles ORDER BY CreatedAt DESC").Select(Function(r) MapBundle(r)).ToList()
        End Function

        Public Function GetBundle(id As Integer) As Bundle
            Dim rows As List(Of DataRow) = Db.Rows("SELECT * FROM Bundles WHERE Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Dim b As Bundle = MapBundle(rows(0))
            b.Items = Db.Rows(
                "SELECT bi.*, p.Name AS ProductName FROM BundleItems bi " &
                "LEFT JOIN Products p ON p.Id = bi.ProductId WHERE bi.BundleId = @id ORDER BY bi.Id",
                Db.P("@id", id)).Select(Function(r) New BundleItem With {
                .Id = RowReader.AsInt(r, "Id"), .BundleId = RowReader.AsInt(r, "BundleId"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .Quantity = RowReader.AsInt(r, "Quantity"), .ProductName = RowReader.AsStr(r, "ProductName")}).ToList()
            Return b
        End Function

        Public Function CreateBundle(b As Bundle) As Integer
            Dim id As Integer = Db.ExecIdentity("INSERT INTO Bundles (Name, Description, DiscountPercent, IsActive, CreatedAt) VALUES (@n, @d, @dp, @a, NOW())",
                    Db.P("@n", b.Name), Db.P("@d", b.Description), Db.P("@dp", b.DiscountPercent),
                    Db.P("@a", b.IsActive))
            For Each item As BundleItem In b.Items
                Db.Exec("INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES (@b, @p, @q)",
                        Db.P("@b", id), Db.P("@p", item.ProductId), Db.P("@q", item.Quantity))
            Next
            Return id
        End Function

        Public Sub UpdateBundle(b As Bundle)
            Db.Exec("UPDATE Bundles SET Name = @n, Description = @d, DiscountPercent = @dp, IsActive = @a WHERE Id = @id",
                    Db.P("@n", b.Name), Db.P("@d", b.Description), Db.P("@dp", b.DiscountPercent),
                    Db.P("@a", b.IsActive), Db.P("@id", b.Id))
            Db.Exec("DELETE FROM BundleItems WHERE BundleId = @id", Db.P("@id", b.Id))
            For Each item As BundleItem In b.Items
                Db.Exec("INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES (@b, @p, @q)",
                        Db.P("@b", b.Id), Db.P("@p", item.ProductId), Db.P("@q", item.Quantity))
            Next
        End Sub

        Public Function DeleteBundle(id As Integer) As String
            Db.Exec("DELETE FROM BundleItems WHERE BundleId = @id", Db.P("@id", id))
            Db.Exec("DELETE FROM Bundles WHERE Id = @id", Db.P("@id", id))
            Return Nothing
        End Function

        ' ----- Promotions -------------------------------------------------------

        Public Function ListPromotions() As List(Of Promotion)
            Return Db.Rows("SELECT * FROM Promotions ORDER BY EndsAt DESC").Select(Function(r) New Promotion With {
                .Id = RowReader.AsInt(r, "Id"), .Name = RowReader.AsStr(r, "Name"), .Description = RowReader.AsStr(r, "Description"),
                .DiscountType = RowReader.AsStr(r, "DiscountType"), .DiscountValue = RowReader.AsDec(r, "DiscountValue"),
                .StartsAt = RowReader.AsDate(r, "StartsAt"), .EndsAt = RowReader.AsDate(r, "EndsAt"),
                .IsActive = RowReader.AsBool(r, "IsActive")}).ToList()
        End Function

        Public Function GetPromotion(id As Integer) As Promotion
            Dim rows As List(Of DataRow) = Db.Rows("SELECT * FROM Promotions WHERE Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return Db.Rows("SELECT * FROM Promotions WHERE Id = @id", Db.P("@id", id)).Select(Function(r) New Promotion With {
                .Id = RowReader.AsInt(r, "Id"), .Name = RowReader.AsStr(r, "Name"), .Description = RowReader.AsStr(r, "Description"),
                .DiscountType = RowReader.AsStr(r, "DiscountType"), .DiscountValue = RowReader.AsDec(r, "DiscountValue"),
                .StartsAt = RowReader.AsDate(r, "StartsAt"), .EndsAt = RowReader.AsDate(r, "EndsAt"),
                .IsActive = RowReader.AsBool(r, "IsActive")}).FirstOrDefault()
        End Function

        Public Function CreatePromotion(p As Promotion) As Integer
            Return Db.ExecIdentity("INSERT INTO Promotions (Name, Description, DiscountType, DiscountValue, StartsAt, EndsAt, IsActive) " &
                    "VALUES (@n, @d, @t, @v, @s, @e, @a)",
                    Db.P("@n", p.Name), Db.P("@d", p.Description), Db.P("@t", p.DiscountType),
                    Db.P("@v", p.DiscountValue), Db.P("@s", p.StartsAt), Db.P("@e", p.EndsAt),
                    Db.P("@a", p.IsActive))
        End Function

        Public Sub UpdatePromotion(p As Promotion)
            Db.Exec("UPDATE Promotions SET Name = @n, Description = @d, DiscountType = @t, DiscountValue = @v, " &
                    "StartsAt = @s, EndsAt = @e, IsActive = @a WHERE Id = @id",
                    Db.P("@n", p.Name), Db.P("@d", p.Description), Db.P("@t", p.DiscountType),
                    Db.P("@v", p.DiscountValue), Db.P("@s", p.StartsAt), Db.P("@e", p.EndsAt),
                    Db.P("@a", p.IsActive), Db.P("@id", p.Id))
        End Sub

        Public Function DeletePromotion(id As Integer) As String
            Db.Exec("DELETE FROM Promotions WHERE Id = @id", Db.P("@id", id))
            Return Nothing
        End Function

        Private Function MapBundle(r As DataRow) As Bundle
            Return New Bundle With {
                .Id = RowReader.AsInt(r, "Id"), .Name = RowReader.AsStr(r, "Name"), .Description = RowReader.AsStr(r, "Description"),
                .DiscountPercent = RowReader.AsDec(r, "DiscountPercent"), .IsActive = RowReader.AsBool(r, "IsActive"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt")
            }
        End Function

        Private Function Slug(name As String) As String
            Dim s As String = name.ToLowerInvariant().Trim().Replace(" ", "-").Replace("_", "-")
            Dim out As String = ""
            For Each ch As Char In s
                If Char.IsLetterOrDigit(ch) OrElse ch = "-"c Then out &= ch
            Next
            Return out.Trim("-"c)
        End Function

        Private Function Map(r As DataRow) As Product
            Return New Product With {
                .Id = RowReader.AsInt(r, "Id"), .MerchantId = RowReader.AsInt(r, "MerchantId"),
                .CategoryId = RowReader.AsInt(r, "CategoryId"), .Name = RowReader.AsStr(r, "Name"),
                .Slug = RowReader.AsStr(r, "Slug"), .Description = RowReader.AsStr(r, "Description"),
                .BasePrice = RowReader.AsDec(r, "BasePrice"), .SalePrice = RowReader.AsNullableDec(r, "SalePrice"),
                .StockQuantity = RowReader.AsInt(r, "StockQuantity"), .LowStockThreshold = RowReader.AsInt(r, "LowStockThreshold"),
                .Sku = RowReader.AsStr(r, "Sku"), .BrandName = RowReader.AsStr(r, "BrandName"),
                .IsActive = RowReader.AsBool(r, "IsActive"), .IsFeatured = RowReader.AsBool(r, "IsFeatured"),
                .IsBoothExclusive = RowReader.AsBool(r, "IsBoothExclusive"), .IsEventExclusive = RowReader.AsBool(r, "IsEventExclusive"),
                .BadgeLabel = RowReader.AsStr(r, "BadgeLabel"), .MaterialDetails = RowReader.AsStr(r, "MaterialDetails"),
                .RatingAvg = RowReader.AsDec(r, "RatingAvg"), .RatingCount = RowReader.AsInt(r, "RatingCount"),
                .SoldCount = RowReader.AsInt(r, "SoldCount"), .CreatedAt = RowReader.AsDate(r, "CreatedAt"),
                .UpdatedAt = RowReader.AsDate(r, "UpdatedAt"), .CategoryName = RowReader.AsStr(r, "CategoryName"),
                .MerchantName = RowReader.AsStr(r, "MerchantName"), .PrimaryImageFile = RowReader.AsStr(r, "PrimaryImageFile")
            }
        End Function

    End Class

End Namespace