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

    ''' <summary>
    ''' A resolved "N for ₱M" bundle rule used by cart/checkout pricing. Membership
    ''' comes from the BundleItems table; the group size and group price are parsed
    ''' from the bundle name itself, e.g. "Stickers Bundle (4 for 100)" -> any 4
    ''' member units ring up at ₱100 instead of list price.
    ''' </summary>
    Public Class BundleGroup
        Public Property BundleId As Integer
        Public Property Name As String
        Public Property GroupSize As Integer
        Public Property GroupPrice As Decimal
        Public Property ProductIds As List(Of Integer)
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
        Public Property Status As String          ' PENDING / CONFIRMED / PROCESSING / SHIPPED / DELIVERED / RECEIVED / CANCELLED
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

        ' Delivery shipping is merchant-quoted, not computed. ShippingFeeConfirmed flips
        ' to True the moment the fee is entered, which is also the moment the order
        ' becomes CONFIRMED and TotalAmount becomes final.
        Public Property ShippingFeeConfirmed As Boolean
        Public Property ShippingFeeConfirmedBy As Integer?
        Public Property ShippingFeeConfirmedAt As Date?

        ' When the customer confirmed the parcel arrived. Only ever set from
        ' DELIVERED, so its presence is proof the buyer actually saw it land
        ' rather than the store simply asserting it did.
        Public Property ReceivedAt As Date?

        ''' <summary>True once the order has a final, customer-facing total.</summary>
        Public ReadOnly Property HasFinalTotal As Boolean
            Get
                Return ShippingFeeConfirmed
            End Get
        End Property

        ' Joined display fields
        Public Property CustomerName As String
        Public Property CustomerEmail As String
        Public Property ItemCount As Integer

        ''' <summary>
        ''' Customer-facing courier sentence, J&amp;T only. Plain text: the page layer adds
        ''' the J&amp;T tracking hyperlink when a tracking number exists. The website never
        ''' plays courier - it just states where the parcel is in the courier journey.
        ''' </summary>
        Public Property TrackingNumber As String

        Public ReadOnly Property TrackingUrl As String
            Get
                If String.IsNullOrWhiteSpace(TrackingNumber) Then Return ""
                Return "https://www.jtexpress.ph/trajectoryQuery?billcode=" & Uri.EscapeDataString(TrackingNumber)
            End Get
        End Property

        Public ReadOnly Property DeliveryStatusLine As String
            Get
                Dim s As String = If(Status, "").ToUpperInvariant()
                Select Case s
                    Case "SHIPPED"
                        If Not String.IsNullOrWhiteSpace(TrackingNumber) Then
                            Return "Order booked with J&T Express — tracking number " & TrackingNumber & ". Track it on the J&T website."
                        End If
                        Return "Order is being scheduled for booking with J&T Express."
                    Case "DELIVERED"
                        Return "Order delivered — waiting for you to confirm you received it."
                    Case "RECEIVED"
                        Return "Order received. Enjoy your art!"
                    Case "CANCELLED"
                        Return "Order cancelled."
                    Case "PENDING"
                        Return "Order is currently scheduled for booking."
                    Case Else
                        Return "Order is currently scheduled for booking."
                End Select
            End Get
        End Property

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
                New String() {"SHIPPED", "Handed to J&T Express"},
                New String() {"DELIVERED", "Delivered"},
                New String() {"RECEIVED", "Received by customer"}
            }
            Dim reached As Integer
            Select Case s
                Case "PENDING" : reached = 0
                Case "CONFIRMED" : reached = 1
                Case "PROCESSING" : reached = 2
                Case "SHIPPED" : reached = 3
                Case "DELIVERED" : reached = 4
                Case "RECEIVED" : reached = 5
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
        Public Property PaymentMethod As String    ' GCASH / GOTYME / CARD / COD
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
                    Case "GOTYME", "MAYA" : Return "GOtyme"
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