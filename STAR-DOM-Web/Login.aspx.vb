Imports System.Web
Imports System.Web.UI
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class LoginPage
        Inherits Page

        Public ReadOnly Property Message As String
            Get
                Dim m As String = Convert.ToString(Session("flash_msg"))
                Session("flash_msg") = Nothing
                Return m
            End Get
        End Property

        Public ReadOnly Property Identifier As String
            Get
                Return Convert.ToString(Session("flash_id"))
            End Get
        End Property

        Public ReadOnly Property ReturnUrl As String
            Get
                ' The value arrives in the query string on GET and in the form body
                ' on POST, so check both.
                Dim r As String = Convert.ToString(Request.QueryString("r"))
                If String.IsNullOrEmpty(r) Then r = Convert.ToString(Request.Form("r"))
                If String.IsNullOrEmpty(r) Then r = ""
                ' Local paths only. "//evil.com" starts with "/" but browsers read it
                ' as a protocol-relative host, so it is rejected explicitly.
                If Not r.StartsWith("/", StringComparison.Ordinal) Then r = ""
                If r.StartsWith("//", StringComparison.Ordinal) Then r = ""
                Return r
            End Get
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            If STAR_DOM.Helpers.Session.IsAuthenticated Then
                Response.Redirect(DefaultHome(), True)
            End If
            If Guard.IsPost() Then
                Dim identifier As String = Request.Form("identifier")
                Dim password As String = Request.Form("password")
                Dim result As ServiceResult = New AuthService().Login(identifier, password)
                If result.Success Then
                    Session("flash_id") = Nothing
                    ' New token for the privileged session: a token captured before
                    ' login cannot be replayed after it.
                    Csrf.Rotate()
                    Response.Redirect(If(ReturnUrl <> "", ReturnUrl, DefaultHome()), True)
                Else
                    Session("flash_msg") = result.Message
                    Session("flash_id") = identifier
                    Response.Redirect("/Login.aspx" & If(ReturnUrl <> "", "?r=" & HttpUtility.UrlEncode(ReturnUrl), ""), True)
                End If
            End If
        End Sub

        Private Function DefaultHome() As String
            If STAR_DOM.Helpers.Session.IsAdmin Then Return "/App/Merchant/Dashboard.aspx"
            Return "/App/Marketplace.aspx"
        End Function

    End Class

End Namespace
