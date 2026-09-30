Namespace STAR_DOM.Models

    Public Class StoreLocation
        Public Property Id As Integer
        Public Property Name As String
        Public Property Venue As String
        Public Property Address As String
        Public Property City As String
        Public Property Region As String
        Public Property Latitude As Decimal?
        Public Property Longitude As Decimal?
        Public Property Contact As String
        Public Property IsActive As Boolean
    End Class

    Public Class PopUpEvent
        Public Property Id As Integer
        Public Property LocationId As Integer
        Public Property Name As String
        Public Property Description As String
        Public Property StartDate As Date
        Public Property EndDate As Date
        Public Property OpenTime As String      ' "10:00 AM"
        Public Property CloseTime As String    ' "9:00 PM"
        Public Property BoothNumber As String
        Public Property VenueDetail As String
        Public Property Status As String       ' UPCOMING / NOW OPEN / ENDED / CANCELLED (CANCELLED is manual; the rest are clock-derived)
        Public Property TimeZone As String = "Asia/Manila"
        Public Property FeaturedGuest As String
        Public Property IsCurrent As Boolean
        Public Property ImageFile As String
        Public Property LineupText As String
        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date

        ' Joined display fields
        Public Property LocationName As String
        Public Property LocationAddress As String
        Public Property RegionLabel As String
        Public Property CityLabel As String

        Public ReadOnly Property HoursText As String
            Get
                Return OpenTime & " – " & CloseTime
            End Get
        End Property

        Public ReadOnly Property WindowText As String
            Get
                Return Helpers.Fmt.EventWindow(StartDate, EndDate)
            End Get
        End Property
    End Class

    Public Class EventInventory
        Public Property Id As Integer
        Public Property EventId As Integer
        Public Property ProductId As Integer
        Public Property StartingStock As Integer
        Public Property SoldQuantity As Integer
        Public Property RemainingStock As Integer
        Public Property IsEventExclusive As Boolean
        Public Property IsActive As Boolean

        Public Property ProductName As String
        Public Property ProductSku As String
        Public Property UnitPrice As Decimal
        Public Property ImageFile As String
    End Class

    Public Class EventSale
        Public Property Id As Integer
        Public Property EventId As Integer
        Public Property OrderId As Integer?
        Public Property ProductId As Integer
        Public Property Quantity As Integer
        Public Property UnitPrice As Decimal
        Public Property TotalAmount As Decimal
        Public Property SaleType As String      ' IN_PERSON / QR / PREORDER / ONLINE
        Public Property PaymentMethod As String
        Public Property SaleDate As Date
        Public Property Notes As String

        Public Property ProductName As String
    End Class

End Namespace