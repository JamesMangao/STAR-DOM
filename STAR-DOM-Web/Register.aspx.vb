Imports System.Web
Imports System.Web.UI
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class RegisterPage
        Inherits Page

        Public ReadOnly Property Message As String
            Get
                Dim m As String = Convert.ToString(Session("flash_msg"))
                Session("flash_msg") = Nothing
                Return m
            End Get
        End Property

        Public Function Prev(key As String) As String
            Return Convert.ToString(Session("flash_" & key))
        End Function

        Public ReadOnly Property ReturnUrl As String
            Get
                ' The value arrives in the query string on GET and in the form body
                ' on POST, so check both.
                Dim r As String = Convert.ToString(Request.QueryString("r"))
                If String.IsNullOrEmpty(r) Then r = Convert.ToString(Request.Form("r"))
                Return Guard.SafeReturnUrl(r, "")
            End Get
        End Property

        ''' <summary>Query string that carries the return target to Login or the form.</summary>
        Public ReadOnly Property ReturnQuery As String
            Get
                Return If(ReturnUrl <> "", "?r=" & HttpUtility.UrlEncode(ReturnUrl), "")
            End Get
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            If STAR_DOM.Helpers.Session.IsAuthenticated Then
                Response.Redirect(DefaultHome(), True)
            End If
            If Guard.IsPost() Then
                Dim fullName As String = Request.Form("fullName")
                Dim email As String = Request.Form("email")
                Dim username As String = Request.Form("username")
                Dim phone As String = Request.Form("phone")
                Dim password As String = Request.Form("password")
                Dim confirm As String = Request.Form("confirm")
                Dim role As String = Request.Form("role")
                Dim result As ServiceResult = New AuthService().Register(fullName, email, username, phone,
                                                                         password, confirm, role)
                If result.Success Then
                    For Each k As String In {"fullName", "email", "username", "phone"}
                        Session("flash_" & k) = Nothing
                    Next
                    ' Registration signs the customer in, so reissue the token like Login does.
                    Csrf.Rotate()
                    Response.Redirect(If(ReturnUrl <> "", ReturnUrl, DefaultHome()), True)
                Else
                    Session("flash_msg") = result.Message
                    Session("flash_fullName") = fullName
                    Session("flash_email") = email
                    Session("flash_username") = username
                    Session("flash_phone") = phone
                    Response.Redirect("/Register.aspx" & ReturnQuery, True)
                End If
            End If
        End Sub

        Private Function DefaultHome() As String
            If STAR_DOM.Helpers.Session.IsAdmin Then Return "/App/Merchant/Dashboard.aspx"
            Return "/App/Marketplace.aspx"
        End Function

    End Class

End Namespace
