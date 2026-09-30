Imports System.Web
Imports System.Web.UI

Namespace STAR_DOM.Web

    Public Class LogoutPage
        Inherits Page

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            STAR_DOM.Helpers.Session.Clear()
            Session.Abandon()
            Response.Redirect("/Login.aspx", True)
        End Sub

    End Class

End Namespace
