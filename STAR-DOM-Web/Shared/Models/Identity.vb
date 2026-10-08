Namespace STAR_DOM.Models

    Public Class Role
        Public Property Id As Integer
        Public Property Name As String
        Public Property Description As String
    End Class

    Public Class User
        Public Property Id As Integer
        Public Property Email As String
        Public Property Username As String
        Public Property FullName As String
        Public Property Phone As String
        Public Property PasswordHash As String
        Public Property RoleId As Integer
        Public Property RoleName As String
        Public Property AvatarFile As String
        Public Property Status As String        ' ACTIVE / SUSPENDED
        Public Property EmailVerified As Boolean
        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date
        Public Property LastLoginAt As Date?

        ' Merchant commission-atelier profile (drives the Commission Hub cards)
        Public Property CommissionSlotCapacity As Integer = 5
        Public Property CommissionStartingPrice As Decimal = 0D
        Public Property CommissionTurnaround As String = "3-5 business days"
        Public Property CommissionFormats As String = "High-Res PNG + Print"
        Public Property CommissionSampleImage As String = ""
        Public Property CommissionTagline As String = ""

        ''' <summary>
        ''' STAR:DOM is a single-owner brand, so ADMIN *is* the merchant. The
        ''' MERCHANT role only survives as a legacy alias that the guards honour for
        ''' older databases; showing it in the UI would imply store staff are a
        ''' separate role, so it is presented as ADMIN everywhere.
        ''' </summary>
        Public ReadOnly Property DisplayRoleName As String
            Get
                If String.Equals(RoleName, "MERCHANT", StringComparison.OrdinalIgnoreCase) Then Return "ADMIN"
                If String.IsNullOrWhiteSpace(RoleName) Then Return "CUSTOMER"
                Return RoleName
            End Get
        End Property
    End Class

End Namespace