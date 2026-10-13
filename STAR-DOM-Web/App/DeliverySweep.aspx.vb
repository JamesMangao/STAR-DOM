Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports STAR_DOM.Database
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    ''' <summary>
    ''' POST-only endpoint that runs the delivery sweep. Called by the scheduled
    ''' GitHub Action, never by a browser.
    ''' </summary>
    ''' <remarks>
    ''' This page is deliberately not part of the site's session/CSRF machinery.
    ''' It has no user, no cookie and no form: the caller is a cron job holding a
    ''' shared secret, so the CSRF check has to be bypassed for this one exact path
    ''' (see Global.asax.vb) and the request is authenticated by the
    ''' DELIVERY_SWEEP_TOKEN header instead.
    '''
    ''' Three things are load-bearing:
    '''   1. The token comes from the environment, never from git. A secret in the
    '''      repository is a secret everyone has.
    '''   2. A request with no token configured on the server is REFUSED (503) rather
    '''      than allowed. Failing open would turn a forgotten environment variable
    '''      into an unauthenticated endpoint that closes orders.
    '''   3. The comparison is constant-time, and the mismatching request is logged
    '''      with the same Context the CSRF guard uses, so a probe looks the same as
    '''      any other blocked request in AppErrors.
    '''
    ''' GET returns 405 rather than running anything: a sweep changes orders, and a
    ''' method a crawler, a prefetcher or a link can trigger must never do that.
    ''' </remarks>
    Public Class DeliverySweepPage
        Inherits Page

        ''' <summary>Environment variable holding the shared secret.</summary>
        Private Const TokenEnvVar As String = "DELIVERY_SWEEP_TOKEN"

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Response.TrySkipIisCustomErrors = True
            Response.ContentType = "text/plain; charset=utf-8"
            Response.Cache.SetCacheability(HttpCacheability.NoCache)

            If Not String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) Then
                Response.StatusCode = 405
                Response.StatusDescription = "405 Method Not Allowed"
                Response.Write("Use POST." & vbLf)
                Return
            End If

            Dim expected As String = If(Environment.GetEnvironmentVariable(TokenEnvVar), "").Trim()
            If expected = "" Then
                ' Not configured. Refuse, and say so plainly: the fix is an
                ' environment variable on the host, not a code change.
                Response.StatusCode = 503
                Response.StatusDescription = "503 Service Unavailable"
                Response.Write(TokenEnvVar & " is not configured on this host." & vbLf)
                Return
            End If

            Dim supplied As String = If(Request.Headers(TokenEnvVar), "").Trim()
            If Not FixedTimeEquals(expected, supplied) Then
                Try
                    Db.LogError("Security", New InvalidOperationException(
                        "Delivery sweep rejected: bad or missing " & TokenEnvVar & " from " & Request.UserHostAddress))
                Catch
                End Try
                Response.StatusCode = 403
                Response.StatusDescription = "403 Forbidden"
                Response.Write("Forbidden." & vbLf)
                Return
            End If

            ' ?dry=1 reports what a real run would do without writing anything. Useful
            ' for the first deploy of the workflow, and safe because it passes through
            ' exactly the same token gate.
            Dim dryRun As Boolean = Trim(Convert.ToString(Request.QueryString("dry"))) = "1"
            Try
                Dim report As OrderService.DeliverySweepReport = New OrderService().RunDeliverySweep(dryRun)
                Response.StatusCode = 200
                Response.Write(If(dryRun, "dry run - ", "") & report.Summary & vbLf)
            Catch ex As Exception
                Db.LogError("DeliverySweep", ex)
                Response.StatusCode = 500
                Response.Write("Sweep failed: " & ex.Message & vbLf)
            End Try
        End Sub

        ''' <summary>
        ''' Length-independent comparison (no early exit on the first differing
        ''' byte), so the endpoint cannot be used as an oracle to guess the token one
        ''' character at a time.
        ''' </summary>
        Private Shared Function FixedTimeEquals(a As String, b As String) As Boolean
            If String.IsNullOrEmpty(a) OrElse String.IsNullOrEmpty(b) Then Return False
            Dim x As Byte() = Encoding.UTF8.GetBytes(a)
            Dim y As Byte() = Encoding.UTF8.GetBytes(b)
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
