Imports System.Web
Imports System.Web.UI

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Ends the session. The page markup in Logout.aspx then shows a "you're signed
    ''' out" modal and returns the visitor to the public storefront at /.
    '''
    ''' This used to redirect straight to /Login.aspx, which reads as "you were never
    ''' signed in" and asks for a password again right after the visitor deliberately
    ''' signed out. Saying what happened, then landing them somewhere that needs no
    ''' account, is the honest version of the same flow.
    '''
    ''' No Site.master here on purpose: a master renders the signed-in chrome (avatar,
    ''' cart count, admin nav) that this very request has just invalidated.
    ''' </summary>
    Public Class LogoutPage
        Inherits Page

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            STAR_DOM.Helpers.Session.Clear()
            Session.Abandon()

            ' No redirect and no Response.End: the modal is already open in the
            ' markup and the page carries the "Back to home" link, so a browser that
            ' blocks the meta-refresh still gets somewhere sensible to click.
        End Sub

    End Class

End Namespace