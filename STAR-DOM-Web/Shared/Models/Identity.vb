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
    End Class

End Namespace