Imports System.Web
Imports System.Web.SessionState
Imports STAR_DOM.Models

Namespace STAR_DOM.Helpers

    ''' <summary>
    ''' Signed-in user context for the current web request, backed by ASP.NET Session.
    ''' Mirrors the desktop module API so the shared Services layer compiles unchanged.
    ''' </summary>
    Public Module Session

        Private Const _userKey As String = "stardom.user"
        Private Const _roleKey As String = "stardom.role"

        Public Property CurrentUser As User
            Get
                Dim s As HttpSessionState = HttpContext.Current.Session
                If s Is Nothing Then Return Nothing
                Return TryCast(s(_userKey), User)
            End Get
            Set(value As User)
                Dim s As HttpSessionState = HttpContext.Current.Session
                If s Is Nothing Then Return
                s(_userKey) = value
                If value Is Nothing Then s(_roleKey) = "" Else s(_roleKey) = value.RoleName
            End Set
        End Property

        Public Property CurrentRoleName As String
            Get
                Dim s As HttpSessionState = HttpContext.Current.Session
                If s Is Nothing Then Return ""
                Return Convert.ToString(s(_roleKey))
            End Get
            Set(value As String)
                Dim s As HttpSessionState = HttpContext.Current.Session
                If s Is Nothing Then Return
                s(_roleKey) = value
            End Set
        End Property

        Public ReadOnly Property IsAuthenticated As Boolean
            Get
                Return CurrentUser IsNot Nothing
            End Get
        End Property

        Public ReadOnly Property IsCustomer As Boolean
            Get
                Return String.Equals(CurrentRoleName, "CUSTOMER", StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        Public ReadOnly Property IsMerchant As Boolean
            Get
                Return String.Equals(CurrentRoleName, "MERCHANT", StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        Public ReadOnly Property IsAdmin As Boolean
            Get
                Return String.Equals(CurrentRoleName, "ADMIN", StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        ''' <summary>True when the user may open merchant management screens.</summary>
        Public ReadOnly Property CanManageStore As Boolean
            Get
                Return IsMerchant OrElse IsAdmin
            End Get
        End Property

        Public ReadOnly Property DisplayName As String
            Get
                If CurrentUser Is Nothing Then Return "Guest"
                Return CurrentUser.FullName
            End Get
        End Property

        Public Sub Clear()
            CurrentUser = Nothing
            CurrentRoleName = ""
        End Sub

    End Module

End Namespace
