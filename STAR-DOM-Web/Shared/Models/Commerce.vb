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
        Public Property PaymentStatus As String   ' PENDING / SUBMITTED / PAID / FAILED / REFUNDED / CANCELLED
        Public Property ShippingAddress As String
        Public Property ContactPhone As String
        Public Property Notes As String
        Public Property CancelReason As String
        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date

        ' Delivery shipping is merchant-quoted, not computed. ShippingFeeConfirmed flips
        ' to True the moment the fee is entered, which is also the moment the order
        ' becomes CONFIRMED and TotalAmount becomes final.
        Public Property ShippingFeeConfirmed As Boolean
        Public Property ShippingFeeConfirmedBy As Integer?
        Public Property ShippingFeeConfirmedAt As Date?

        ' When the order reached RECEIVED. Only ever set on that transition, so it is
        ' the date the order closed rather than the date the studio handed the parcel
        ' to J&T. ReceiptSource is what distinguishes who established it: a customer
        ' pressing the button and a studio asserting on their behalf both land here,
        ' and conflating the two is the thing to avoid.
        Public Property ReceivedAt As Date?

        ' ---- Delivery evidence -------------------------------------------------
        ' Courier outcome and proof of delivery, entered BY THE STUDIO from the
        ' courier's own tracking page. There is no J&T API, and that absence is the
        ' point: an automated status claim would be a guess, whereas an uploaded
        ' screenshot is something a person actually looked at. Nothing in the
        ' application infers any of these.
        Public Property CourierStatus As String
        Public Property PodReference As String
        Public Property PodImageData As Byte()
        Public Property PodMime As String
        Public Property PodCapturedAt As Date?
        Public Property PodCapturedBy As String

        ' When the customer was told the parcel had landed, and when the single
        ' follow-up nudge went out. The scheduled sweep requires BOTH before it will
        ' close anything, so no order can auto-close against a customer who was never
        ' notified in the first place.
        Public Property DeliveredNotifiedAt As Date?
        Public Property ReminderSentAt As Date?

        ' How the order reached RECEIVED: CUSTOMER | COURIER_POD | STUDIO | SYSTEM.
        ' This is the column that answers "how do you know it arrived?". It stays
        ' empty until the order is genuinely RECEIVED, so it can never pre-claim a
        ' confirmation that has not happened.
        Public Property ReceiptSource As String

        ' Dispute hold. HELD is neither CANCELLED nor RECEIVED: the parcel may well
        ' have arrived and nobody has decided yet.
        Public Property HoldReason As String
        Public Property HoldResolvedAt As Date?
        Public Property HoldResolution As String

        ''' <summary>
        ''' True while a dispute is open and the order is parked. HELD is only ever
        ''' entered from DELIVERED: a dispute means "the studio says this landed and I
        ''' say it did not", so there has to be a delivery claim to disagree with.
        ''' </summary>
        Public ReadOnly Property IsHeld As Boolean
            Get
                Return String.Equals(Status, OrderStatuses.Held, StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        ''' <summary>True once a proof of delivery has been uploaded.</summary>
        Public ReadOnly Property HasPod As Boolean
            Get
                Return PodImageData IsNot Nothing AndAlso PodImageData.Length > 0
            End Get
        End Property

        Public ReadOnly Property WasReceived As Boolean
            Get
                Return String.Equals(Status, OrderStatuses.Received, StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        ''' <summary>
        ''' How the receipt was established, in words the customer page can show. The
        ''' audit trail is only worth anything if it is also said out loud, so the
        ''' wording lives here rather than being reinvented at each call site.
        ''' </summary>
        Public ReadOnly Property ReceiptSourceLabel As String
            Get
                Select Case If(ReceiptSource, "").Trim().ToUpperInvariant()
                    Case "CUSTOMER" : Return "Received — confirmed by you"
                    Case "COURIER_POD" : Return "Received — courier proof of delivery on file"
                    Case "STUDIO" : Return "Received — marked by the studio on your behalf"
                    Case "SYSTEM" : Return "Received — closed automatically after the delivery window"
                    Case Else
                        Return If(WasReceived, "Received", "")
                End Select
            End Get
        End Property

        ''' <summary>True once the order has a final, customer-facing total.</summary>
        Public ReadOnly Property HasFinalTotal As Boolean
            Get
                Return ShippingFeeConfirmed
            End Get
        End Property

        ''' <summary>
        ''' True once the studio has nothing further to do on this order: it has
        ''' landed, the customer confirmed it landed, or it was called off.
        ''' </summary>
        ''' <remarks>
        ''' DELIVERED counts even though the customer has not confirmed receipt
        ''' yet, because the parcel is already with the courier and nothing is
        ''' pending on the studio's side. RECEIVED and CANCELLED are the two states
        ''' after which the order can never change again.
        '''
        ''' This lives on the model rather than in the page. The order page and the
        ''' receipt both need the same answer, and a status list duplicated into
        ''' markup is how the two quietly drift apart when a state is added.
        ''' </remarks>
        Public ReadOnly Property IsFinal As Boolean
            Get
                Select Case If(Status, "").ToUpperInvariant()
                    Case "DELIVERED", "RECEIVED", "REFUNDED", "CANCELLED"
                        Return True
                    Case Else
                        Return False
                End Select
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
                    Case "HELD"
                        Return "Held while the studio investigates — nothing further is required from you yet."
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
                ' HELD has no step of its own on purpose, but it must not fall through
                ' to Case Else: a disputed order had genuinely been delivered, and
                ' rendering it as "Order placed" would deny that to the customer. Only
                ' DELIVERED orders can be disputed, which is what makes 4 the honest
                ' answer here.
                Case "HELD" : reached = 4
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

    ''' <summary>How a delivery confirmation was established. Named because the
    ''' distinction is the whole point: a customer pressing a button and a studio
    ''' asserting on their behalf are not the same claim, and the audit trail has to
    ''' be able to tell them apart.
    ''' </summary>
    Public Module ReceiptSources
        Public Const Customer As String = "CUSTOMER"
        Public Const CourierPod As String = "COURIER_POD"
        Public Const Studio As String = "STUDIO"
        Public Const AutoSweep As String = "SYSTEM"
    End Module

    ''' <summary>One line of an order's delivery audit trail.</summary>
    Public Class OrderStatusEntry
        Public Property OrderId As Integer
        Public Property FromStatus As String
        Public Property ToStatus As String
        Public Property Source As String
        Public Property Reason As String
        Public Property ChangedBy As String
        Public Property CreatedAt As Date
    End Class

    ''' <summary>All known order statuses, in workflow order. Mirrors CommissionStatuses.</summary>
    ''' <remarks>
    ''' Orders used bare string literals everywhere, which is how "HELD" ended up
    ''' needing to be spelled identically in four unrelated files to be safe. HELD is
    ''' new and carries rules the rest of the pipeline does not -- the delivery
    ''' sweep must refuse to touch it, and it must not read as either finished or
    ''' refunded -- so it gets a name rather than another literal.
    ''' </remarks>
    Public Module OrderStatuses
        Public Const Pending As String = "PENDING"
        Public Const Confirmed As String = "CONFIRMED"
        Public Const Processing As String = "PROCESSING"
        Public Const Shipped As String = "SHIPPED"
        Public Const Delivered As String = "DELIVERED"
        ''' Parked by a customer dispute. Neither finished nor cancelled.</summary>
        Public Const Held As String = "HELD"
        Public Const Received As String = "RECEIVED"
        ''' <summary>
        ''' Paid, then returned. Only reachable by resolving a dispute, and terminal:
        ''' nothing further happens to the order. The money itself moves outside the system
        ''' -- the studio hands it back over the phone or in person, because there is no
        ''' gateway here that could send it. This status records that decision; it does not
        ''' perform a refund.
        ''' </summary>
        Public Const Refunded As String = "REFUNDED"
        Public Const Cancelled As String = "CANCELLED"

        ''' <summary>
        ''' True once nothing more will happen to the order without a human. HELD is
        ''' excluded on purpose: it is unfinished by definition, and Order.IsFinal
        ''' independently returns False for it.
        ''' </summary>
        Public Function IsTerminal(status As String) As Boolean
            Select Case If(status, "").Trim().ToUpperInvariant()
                Case Delivered, Received, Refunded, Cancelled : Return True
                Case Else : Return False
            End Select
        End Function
    End Module

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
        Public Property Status As String          ' PENDING / SUBMITTED / PAID / FAILED / REFUNDED / CANCELLED
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