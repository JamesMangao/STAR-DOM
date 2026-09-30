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
    End Class

    Public Class CommissionReferenceImage
        Public Property Id As Integer
        Public Property CommissionId As Integer
        Public Property ImageFile As String
        Public Property FileName As String
        Public Property FileSizeKb As Integer
        Public Property SortOrder As Integer
    End Class

    Public Class CommissionMessage
        Public Property Id As Integer
        Public Property CommissionId As Integer
        Public Property SenderId As Integer
        Public Property SenderName As String
        Public Property Message As String
        Public Property IsRead As Boolean
        Public Property CreatedAt As Date
    End Class

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
        Public Const ClarificationRequested As String = "CLARIFICATION REQUESTED"
        Public Const Accepted As String = "ACCEPTED"
        Public Const OfferSent As String = "OFFER SENT"
        Public Const CustomerConfirmed As String = "CUSTOMER CONFIRMED"
        Public Const PaymentPending As String = "PAYMENT PENDING"
        Public Const Paid As String = "PAID"
        Public Const InProduction As String = "IN PRODUCTION"
        Public Const Revision As String = "REVISION"
        Public Const Finalized As String = "FINALIZED"
        Public Const Completed As String = "COMPLETED"
        Public Const Declined As String = "DECLINED"
        Public Const Cancelled As String = "CANCELLED"

        Public ReadOnly Property All As String() = {
            Submitted, PendingReview, ClarificationRequested, Accepted, OfferSent,
            CustomerConfirmed, PaymentPending, Paid, InProduction, Revision,
            Finalized, Completed, Declined, Cancelled
        }
    End Module

End Namespace