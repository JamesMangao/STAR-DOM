Namespace STAR_DOM.Models

    ''' <summary>Commission request with the full merchant-atelier lifecycle.</summary>
    Public Class Commission
        Public Property Id As Integer
        Public Property CommissionNumber As String
        Public Property CustomerId As Integer
        Public Property MerchantId As Integer
        Public Property CategoryId As Integer
        Public Property Title As String
        Public Property Description As String
        Public Property Quantity As Integer
        Public Property PreferredSize As String
        Public Property PreferredDeadline As Date?
        Public Property BudgetMin As Decimal?
        Public Property BudgetMax As Decimal?
        Public Property AdditionalNotes As String

        ' Merchant-side offer fields
        Public Property FinalPrice As Decimal?
        Public Property EstimatedCompletionDate As Date?
        Public Property MerchantNotes As String
        Public Property DepositAmount As Decimal?

        ' Delivery. A finished commission is delivered rather than collected across
        ' a counter, so the request carries an address and phone of its own.
        Public Property ShippingAddress As String
        Public Property ContactPhone As String

        ' The GCash/GOtyme transaction number the customer paid against, and the
        ' moment the studio verified it. Production does not start until the latter
        ' is set.
        Public Property PaymentReference As String
        Public Property PaymentConfirmedAt As Date?

        ' Courier hand-off. Tracking is optional: a commission is often delivered in
        ' person or picked up, so a number is only recorded when one exists.
        Public Property TrackingNumber As String
        Public Property DeliveredAt As Date?

        ' Written by the customer once the piece reached them.
        Public Property ReceivedAt As Date?

        Public Property Status As String
        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date

        ' Joined display fields
        Public Property CustomerName As String
        Public Property CustomerEmail As String
        Public Property MerchantName As String
        Public Property CategoryName As String
        Public Property ReferenceCount As Integer
        Public Property MessageCount As Integer

        Public ReadOnly Property StatusDisplay As String
            Get
                Return Status.Replace("_", " ")
            End Get
        End Property

        ''' <summary>
        ''' Shipping on a commission is always free. Unlike an order, the quoted
        ''' price is the whole price and the studio covers the courier, so there is
        ''' never a shipping line to add or argue about.
        ''' </summary>
        Public ReadOnly Property ShippingFee As Decimal
            Get
                Return 0D
            End Get
        End Property

        ''' <summary>True once the studio has verified the payment reference.</summary>
        Public ReadOnly Property PaymentConfirmed As Boolean
            Get
                Return PaymentConfirmedAt.HasValue
            End Get
        End Property

        Public ReadOnly Property TrackingUrl As String
            Get
                If String.IsNullOrWhiteSpace(TrackingNumber) Then Return ""
                Return "https://www.jtexpress.ph/trajectoryQuery?billcode=" & Uri.EscapeDataString(TrackingNumber)
            End Get
        End Property

        ''' <summary>
        ''' Customer-facing courier sentence. The studio states where the piece is;
        ''' live tracking happens on J&amp;T's own site.
        ''' </summary>
        Public ReadOnly Property DeliveryStatusLine As String
            Get
                Select Case If(Status, "").ToUpperInvariant()
                    Case "FINALIZED"
                        Return "Your commission is finished and waiting to be delivered."
                    Case "DELIVERED"
                        If Not String.IsNullOrWhiteSpace(TrackingNumber) Then
                            Return "Delivered with J&T Express — tracking number " & TrackingNumber & "."
                        End If
                        Return "Delivered — waiting for you to confirm you received it."
                    Case "RECEIVED"
                        Return "Commission received. Enjoy your art!"
                    Case "PAYMENT DECLINED"
                        Return "Payment was declined. " & PaymentSetting.SupportContact
                    Case "CANCELLED"
                        Return "Commission cancelled."
                    Case Else
                        Return "Still in progress at the studio."
                End Select
            End Get
        End Property
    End Class

    Public Class CommissionReferenceImage
        Public Property Id As Integer
        Public Property CommissionId As Integer
        Public Property ImageFile As String
        Public Property FileName As String
        Public Property FileSizeKb As Integer
        Public Property SortOrder As Integer
    End Class

    ' CommissionMessage is intentionally gone: its table is dropped along with the
    ' commission clarification round-trip and message thread.

    Public Class CommissionStatusHistory
        Public Property Id As Integer
        Public Property CommissionId As Integer
        Public Property FromStatus As String
        Public Property ToStatus As String
        Public Property ChangedByName As String
        Public Property Note As String
        Public Property CreatedAt As Date
    End Class

    ''' <summary>All known commission statuses, in workflow order.</summary>
    Public Module CommissionStatuses
        Public Const Submitted As String = "SUBMITTED"
        Public Const PendingReview As String = "PENDING REVIEW"
        Public Const Accepted As String = "ACCEPTED"
        Public Const OfferSent As String = "OFFER SENT"
        Public Const CustomerConfirmed As String = "CUSTOMER CONFIRMED"
        Public Const PaymentPending As String = "PAYMENT PENDING"
        Public Const Paid As String = "PAID"
        Public Const PaymentDeclined As String = "PAYMENT DECLINED"
        Public Const InProduction As String = "IN PRODUCTION"
        Public Const Revision As String = "REVISION"
        Public Const Finalized As String = "FINALIZED"
        Public Const Delivered As String = "DELIVERED"
        Public Const Received As String = "RECEIVED"
        Public Const Completed As String = "COMPLETED"
        Public Const Declined As String = "DECLINED"
        Public Const Cancelled As String = "CANCELLED"

        Public ReadOnly Property All As String() = {
            Submitted, PendingReview, Accepted, OfferSent,
            CustomerConfirmed, PaymentPending, Paid, PaymentDeclined, InProduction, Revision,
            Finalized, Delivered, Received, Completed, Declined, Cancelled
        }
    End Module

End Namespace