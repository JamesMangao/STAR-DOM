Imports System.Security.Cryptography
Imports System.Web
Imports System.Web.SessionState

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Per-session CSRF token (double-submit / synchronizer pattern).
    ''' The token lives in ASP.NET Session and is echoed back by every POST form as
    ''' a hidden field. A POST whose token is missing or does not match the session
    ''' token is rejected centrally in Global.asax.vb Application_BeginRequest.
    ''' Enforcement lives in exactly one place, so a page cannot forget to check.
    ''' </summary>
    Public NotInheritable Class Csrf

        Public Const FieldName As String = "__csrf"

        Private Const _sessionKey As String = "stardom.csrf"
        Private Const _tokenBytes As Integer = 32

        Private Sub New()
        End Sub

        ''' <summary>Current session token, created on first access.</summary>
        Public Shared Function Token() As String
            Dim s As System.Web.SessionState.HttpSessionState = HttpContext.Current.Session
            If s Is Nothing Then Return ""

            Dim existing As String = TryCast(s(_sessionKey), String)
            If Not String.IsNullOrEmpty(existing) Then Return existing

            Dim fresh As String = MakeToken()
            s(_sessionKey) = fresh
            Return fresh
        End Function

        ''' <summary>
        ''' Issue a new token for the session. Called right after authentication so a
        ''' token that was exposed before login cannot be replayed afterwards.
        ''' </summary>
        Public Shared Sub Rotate()
            Dim s As System.Web.SessionState.HttpSessionState = HttpContext.Current.Session
            If s Is Nothing Then Return
            s(_sessionKey) = MakeToken()
        End Sub

        ''' <summary>Hidden input to drop inside any POST form. Safe to call on GET renders.</summary>
        Public Shared Function HiddenField() As String
            Return "<input type=""hidden"" name=""" & FieldName & """ value=""" &
                   HttpUtility.HtmlAttributeEncode(Token()) & """ />"
        End Function

        ''' <summary>True when the request carries a token matching the session's.</summary>
        Public Shared Function IsValidRequest() As Boolean
            Dim s As System.Web.SessionState.HttpSessionState = HttpContext.Current.Session
            If s Is Nothing Then Return False

            Dim expected As String = TryCast(s(_sessionKey), String)
            If String.IsNullOrEmpty(expected) Then Return False

            ' Every value is checked, not just the first one. The shell form in
            ' Site.master and each nested form both emit a token field, and an HTML
            ' parser drops the nested <form> start tag — so a real browser submits
            ' the field twice. Request.Form then holds a String[] and reading it as
            ' a single value yields "token,token", which never matches. Accepting a
            ' match on ANY value keeps the check tight (the token is still unguessable)
            ' while surviving that duplicate-field quirk.
            Dim supplied As String() = HttpContext.Current.Request.Form.GetValues(FieldName)
            If supplied Is Nothing OrElse supplied.Length = 0 Then Return False
            For Each v As String In supplied
                If FixedTimeEquals(expected, v) Then Return True
            Next
            Return False
        End Function

        ''' <summary>
        ''' True when the session looks like it simply expired (no token was ever
        ''' issued), as opposed to a token that was issued and then tampered with.
        ''' Used to send the visitor to sign-in instead of showing a hard 403.
        ''' </summary>
        Public Shared Function SessionHasNoToken() As Boolean
            Dim s As System.Web.SessionState.HttpSessionState = HttpContext.Current.Session
            If s Is Nothing Then Return True
            Return String.IsNullOrEmpty(TryCast(s(_sessionKey), String))
        End Function

        Private Shared Function MakeToken() As String
            Dim bytes As Byte() = New Byte(_tokenBytes - 1) {}
            Using rng As RandomNumberGenerator = RandomNumberGenerator.Create()
                rng.GetBytes(bytes)
            End Using
            ' Base64url so the value is safe in HTML/URLs without extra escaping.
            Return Convert.ToBase64String(bytes).
                TrimEnd("="c).
                Replace("+", "-").
                Replace("/", "_")
        End Function

        ''' <summary>Length-independent constant-time compare (no early exit on first diff).</summary>
        Private Shared Function FixedTimeEquals(a As String, b As String) As Boolean
            If String.IsNullOrEmpty(a) OrElse String.IsNullOrEmpty(b) Then Return False
            Dim x As Byte() = System.Text.Encoding.UTF8.GetBytes(a)
            Dim y As Byte() = System.Text.Encoding.UTF8.GetBytes(b)
            Dim diff As Integer = x.Length Xor y.Length
            Dim n As Integer = Math.Max(x.Length, y.Length)
            For i As Integer = 0 To n - 1
                Dim xb As Byte = If(i < x.Length, x(i), CByte(0))
                Dim yb As Byte = If(i < y.Length, y(i), CByte(0))
                diff = diff Or (xb Xor yb)
            Next
            Return diff = 0
        End Function

    End Class

End Namespace
