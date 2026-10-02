Imports System.Web
Imports STAR_DOM.Database

Namespace STAR_DOM.Web

    Public Class GlobalApp
        Inherits HttpApplication

        Sub Application_Start(sender As Object, e As EventArgs)
            ' Keep the AppErrors log bounded; prune rows older than the retention window.
            Db.PruneErrors()
        End Sub

        ''' <summary>
        ''' Single choke point for CSRF protection: every POST in the site is checked
        ''' here, before any page code runs, so no individual page can forget to check.
        '''
        ''' This must run on AcquireRequestState, NOT BeginRequest: SessionStateModule
        ''' loads the session during AcquireRequestState, so HttpContext.Session is
        ''' still Nothing in BeginRequest and every POST would look token-less.
        ''' </summary>
        Sub Application_AcquireRequestState(sender As Object, e As EventArgs)
            Dim ctx As HttpContext = HttpContext.Current
            If ctx Is Nothing Then Return
            If Not String.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) Then Return

            ' No token in the session means the session expired (or never existed):
            ' send the visitor to sign-in rather than showing a scary error page.
            '
            ' Set the redirect by hand and call CompleteRequest instead of using
            ' Response.Redirect(url, True): the latter does not reliably stop the
            ' pipeline on .NET 4, and execution would fall through into the 403
            ' branch below and overwrite the 302.
            If Csrf.SessionHasNoToken() Then
                ctx.Response.StatusCode = 302
                ctx.Response.RedirectLocation = "/Login.aspx?r=" & HttpUtility.UrlEncode(ctx.Request.RawUrl)
                ctx.ApplicationInstance.CompleteRequest()
                Return
            End If


            If Csrf.IsValidRequest() Then Return

            ' Token was issued but did not match -> treat as a forgery attempt.
            Try
                Db.LogError("Security", New InvalidOperationException(
                    "CSRF token mismatch on " & ctx.Request.Path & " from " & ctx.Request.UserHostAddress))
            Catch
            End Try

            ctx.Response.Clear()
            ctx.Response.StatusCode = 403
            ctx.Response.StatusDescription = "403 Forbidden"
            ctx.Response.TrySkipIisCustomErrors = True
            ctx.Response.ContentType = "text/html; charset=utf-8"
            ctx.Response.Write(
                "<!DOCTYPE html><html lang=""en""><head><meta charset=""utf-8"">" &
                "<title>403 - Request blocked</title><link rel=""stylesheet"" href=""/css/site.css?v=20261003""></head>" &
                "<body><div class=""layout"" style=""display:block;max-width:640px;margin:80px auto;padding:0 20px"">" &
                "<div class=""card"" style=""padding:28px"">" &
                "<h1 style=""font-size:22px;margin:0 0 8px"">403 &mdash; Request blocked</h1>" &
                "<p class=""sub"" style=""margin:0 0 14px"">This form submission failed its security check, " &
                "so it was rejected and the attempt was logged. Nothing was changed.</p>" &
                "<p class=""sub"" style=""margin:0"">If this keeps happening, your session may have expired " &
                "or cookies may be blocked. Reload the page and try again.</p>" &
                "<p style=""margin:16px 0 0""><a class=""btn primary"" href=""/App/Marketplace.aspx"">Back to STAR:DOM</a></p>" &
                "</div></div></body></html>")
            ctx.ApplicationInstance.CompleteRequest()
        End Sub

        Sub Application_Error(sender As Object, e As EventArgs)
            Dim ex As Exception = Server.GetLastError()
            If ex IsNot Nothing Then
                Db.LogError("Web", ex)
            End If
            Dim baseEx As Exception = If(ex IsNot Nothing, ex.GetBaseException(), Nothing)
            ' Session can be unavailable during teardown/error paths; never let that
            ' replace the real exception with a secondary one.
            Try
                Session("LastError") = If(baseEx IsNot Nothing, baseEx.Message, "An unexpected error occurred.")
            Catch
            End Try
            ' Logged + user-friendly page; do not let the raw exception reach the client.
        End Sub

        Sub Session_Start(sender As Object, e As EventArgs)
        End Sub

        Sub Session_End(sender As Object, e As EventArgs)
        End Sub

    End Class

End Namespace
