Namespace STAR_DOM.Models

    Public Class Cart
        Public Property Id As Integer
        Public Property UserId As Integer
        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date
    End Class

    Public Class CartItem
        Public Property Id As Integer
        Public Property CartId As Integer
        Public Property ProductId As Integer
        Public Property VariantId As Integer?
        Public Property Quantity As Integer
        Public Property AddedAt As Date

        ' Joined display fields
        Public Property ProductName As String
        Public Property ProductSku As String
        Public Property UnitPrice As Decimal
        Public Property ImageFile As String
        Public Property StockQuantity As Integer
        Public Property IsActive As Boolean

        Public ReadOnly Property LineTotal As Decimal
            Get
                Return UnitPrice * Quantity
            End Get
        End Property
    End Class

    Public Class WishlistItem
        Public Property Id As Integer
        Public Property UserId As Integer
        Public Property ProductId As Integer
        Public Property CreatedAt As Date
        Public Property ProductName As String
        Public Property UnitPrice As Decimal
        Public Property ImageFile As String
        Public Property InStock As Boolean
    End Class

    Public Class Order
        Public Property Id As Integer
        Public Property OrderNumber As String
        Public Property UserId As Integer
        Public Property EventId As Integer?
        Public Property Status As String          ' PENDING / CONFIRMED / PROCESSING / SHIPPED / DELIVERED / CANCELLED
        Public Property Subtotal As Decimal
        Public Property DiscountAmount As Decimal
        Public Property ShippingFee As Decimal
        Public Property TotalAmount As Decimal
        Public Property PaymentMethod As String
        Public Property PaymentStatus As String
        Public Property ShippingAddress As String
        Public Property ContactPhone As String
        Public Property Notes As String
        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date

        Public Property CustomerName As String
        Public Property CustomerEmail As String
        Public Property ItemCount As Integer

        Public ReadOnly Property StatusTimeline As String()()
            Get
                Return OrderStatusTimeline(Status)
            End Get
        End Property

        Public Shared Function OrderStatusTimeline(status As String) As String()()
            Dim s As String = If(status, "").ToUpperInvariant()
            Dim steps As New List(Of String()) From {
                New String() {"PENDING", "Order placed"},
                New String() {"CONFIRMED", "Order confirmed"},
                New String() {"PROCESSING", "Preparing at the studio"},
                New String() {"SHIPPED", "Handed to courier"},
                New String() {"DELIVERED", "Delivered"}
            }
            Dim reached As Integer
            Select Case s
                Case "PENDING" : reached = 0
                Case "CONFIRMED" : reached = 1
                Case "PROCESSING" : reached = 2
                Case "SHIPPED" : reached = 3
                Case "DELIVERED" : reached = 4
                Case Else : reached = 0
            End Select
            Dim result As New List(Of String())()
            For i As Integer = 0 To steps.Count - 1
                result.Add({steps(i)(0), steps(i)(1), If(i <= reached, "DONE", "TODO")})
            Next
            Return result.ToArray()
        End Function
    End Class

    Public Class OrderItem
        Public Property Id As Integer
        Public Property OrderId As Integer
        Public Property ProductId As Integer
        Public Property VariantId As Integer?
        Public Property Quantity As Integer
        Public Property UnitPrice As Decimal
        Public Property LineTotal As Decimal

        Public Property ProductName As String
        Public Property ProductSku As String
        Public Property ImageFile As String
    End Class

    Public Class Payment
        Public Property Id As Integer
        Public Property OrderId As Integer
        Public Property PaymentMethod As String    ' GCASH / MAYA / CARD / COD
        Public Property Amount As Decimal
        Public Property ReferenceNumber As String
        Public Property Status As String          ' PENDING / PAID / FAILED / REFUNDED
        Public Property PaidAt As Date?
        Public Property GatewayResponse As String
        Public Property CreatedAt As Date

        Public ReadOnly Property DisplayMethod As String
            Get
                Select Case PaymentMethod.ToUpperInvariant()
                    Case "GCASH" : Return "GCash"
                    Case "MAYA" : Return "Maya"
                    Case "CARD" : Return "Card"
                    Case "COD" : Return "Cash on Delivery"
                    Case Else : Return PaymentMethod
                End Select
            End Get
        End Property
    End Class

    Public Class Receipt
        Public Property Id As Integer
        Public Property PaymentId As Integer
        Public Property OrderId As Integer
        Public Property ReceiptNumber As String
        Public Property ReceiptType As String            ' OR
        Public Property IssuerName As String
        Public Property IssuerTin As String
        Public Property IssuerAddress As String
        Public Property IssuerAccreditation As String
        Public Property SoldToName As String
        Public Property SoldToAddress As String
        Public Property Subtotal As Decimal
        Public Property DiscountAmount As Decimal
        Public Property ShippingFee As Decimal
        Public Property VatableAmount As Decimal
        Public Property VatAmount As Decimal
        Public Property VatExemptAmount As Decimal
        Public Property TotalAmount As Decimal
        Public Property ItemsSnapshot As String
        Public Property IssuedAt As Date
        Public Property PaymentMethod As String
        Public Property PaidAt As Date?
    End Class

    Public Class Shipping
        Public Property Id As Integer
        Public Property OrderId As Integer
        Public Property Courier As String
        Public Property TrackingNumber As String
        Public Property Status As String
        Public Property Address As String
        Public Property ShippedAt As Date?
        Public Property DeliveredAt As Date?
    End Class

    Public Class Review
        Public Property Id As Integer
        Public Property ProductId As Integer
        Public Property OrderId As Integer?
        Public Property UserId As Integer
        Public Property Rating As Integer
        Public Property Comment As String
        Public Property IsApproved As Boolean
        Public Property CreatedAt As Date

        Public Property ProductName As String
        Public Property CustomerName As String
    End Class

End Namespace