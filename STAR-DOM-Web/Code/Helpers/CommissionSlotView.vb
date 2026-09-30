Namespace STAR_DOM.Helpers

    ''' <summary>
    ''' Commission slot card data (web copy of the class the shared CatalogService returns).
    ''' </summary>
    Public Class CommissionSlotView
        Public Property MerchantId As Integer
        Public Property MerchantName As String
        Public Property MerchantTagline As String
        Public Property MerchantAvatar As String
        Public Property SampleImage As String
        Public Property StartingPrice As Decimal
        Public Property Turnaround As String
        Public Property Formats As String
        Public Property Deposit As Decimal
        Public Property SlotsOpen As Integer
        Public Property SlotsTotal As Integer
        Public Property CtaText As String

        Public ReadOnly Property SlotsText As String
            Get
                Return SlotsOpen.ToString() & "/" & SlotsTotal.ToString() & " SLOTS OPEN"
            End Get
        End Property

        Public ReadOnly Property Seed As Integer
            Get
                Return MerchantId * 7 + SlotsOpen
            End Get
        End Property
    End Class

End Namespace
