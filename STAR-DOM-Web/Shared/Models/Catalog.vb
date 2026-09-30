Namespace STAR_DOM.Models

    Public Class Category
        Public Property Id As Integer
        Public Property Name As String
        Public Property Slug As String
        Public Property Description As String
        Public Property DisplayOrder As Integer
        Public Property IsActive As Boolean
        Public Property ParentId As Integer?
    End Class

    Public Class Product
        Public Property Id As Integer
        Public Property MerchantId As Integer
        Public Property CategoryId As Integer
        Public Property Name As String
        Public Property Slug As String
        Public Property Description As String
        Public Property BasePrice As Decimal
        Public Property SalePrice As Decimal?
        Public Property StockQuantity As Integer
        Public Property LowStockThreshold As Integer
        Public Property Sku As String
        Public Property BrandName As String
        Public Property IsActive As Boolean
        Public Property IsFeatured As Boolean
        Public Property IsBoothExclusive As Boolean
        Public Property IsEventExclusive As Boolean
        Public Property BadgeLabel As String
        Public Property MaterialDetails As String
        Public Property RatingAvg As Decimal
        Public Property RatingCount As Integer
        Public Property SoldCount As Integer
        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date

        ' Joined display fields
        Public Property CategoryName As String
        Public Property MerchantName As String
        Public Property PrimaryImageFile As String

        Public ReadOnly Property EffectivePrice As Decimal
            Get
                If SalePrice.HasValue AndAlso SalePrice.Value > 0 Then Return SalePrice.Value
                Return BasePrice
            End Get
        End Property

        Public ReadOnly Property HasDiscount As Boolean
            Get
                Return SalePrice.HasValue AndAlso SalePrice.Value > 0 AndAlso SalePrice.Value < BasePrice
            End Get
        End Property

        Public ReadOnly Property DiscountPercent As Integer
            Get
                If Not HasDiscount OrElse BasePrice <= 0 Then Return 0
                Return CInt(Math.Round((BasePrice - SalePrice.Value) / BasePrice * 100D))
            End Get
        End Property

        Public ReadOnly Property InStock As Boolean
            Get
                Return StockQuantity > 0
            End Get
        End Property
    End Class

    Public Class ProductImage
        Public Property Id As Integer
        Public Property ProductId As Integer
        Public Property ImageFile As String
        Public Property IsPrimary As Boolean
        Public Property SortOrder As Integer
    End Class

    Public Class ProductVariant
        Public Property Id As Integer
        Public Property ProductId As Integer
        Public Property Name As String
        Public Property Sku As String
        Public Property PriceAdjustment As Decimal
        Public Property StockQuantity As Integer
        Public Property IsActive As Boolean
    End Class

    Public Class Bundle
        Public Property Id As Integer
        Public Property Name As String
        Public Property Description As String
        Public Property DiscountPercent As Decimal
        Public Property IsActive As Boolean
        Public Property CreatedAt As Date
        Public Property Items As New List(Of BundleItem)()
    End Class

    Public Class BundleItem
        Public Property Id As Integer
        Public Property BundleId As Integer
        Public Property ProductId As Integer
        Public Property Quantity As Integer
        Public Property ProductName As String
    End Class

    Public Class Promotion
        Public Property Id As Integer
        Public Property Name As String
        Public Property Description As String
        Public Property DiscountType As String    ' PERCENT / FIXED
        Public Property DiscountValue As Decimal
        Public Property StartsAt As Date
        Public Property EndsAt As Date
        Public Property IsActive As Boolean
        Public ReadOnly Property IsLive As Boolean
            Get
                Return IsActive AndAlso Date.Now >= StartsAt AndAlso Date.Now <= EndsAt
            End Get
        End Property
    End Class

End Namespace