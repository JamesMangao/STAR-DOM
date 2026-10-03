Imports System.Web
Imports System.Web.UI
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Streams one payment channel's QR image straight out of the database.
    '''
    ''' The image used to be a file under Uploads\payments, and Uploads is not in
    ''' Git — so on any other machine the file was missing and the popup fell back
    ''' to a placeholder. Holding the bytes in the database instead means a
    ''' pg_dump carries them along with everything else and the QR simply works
    ''' after a restore.
    '''
    ''' This endpoint is unauthenticated on purpose, matching what it replaced:
    ''' the file under Uploads was web-accessible too, and the customer needs to
    ''' see the QR in their order page. It serves only the two known channels, so
    ''' nothing else in the settings table is reachable.
    ''' </summary>
    Public Class PaymentQrPage
        Inherits Page

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Try
                Serve()
            Catch ex As System.Threading.ThreadAbortException
                ' Response.End() re-raises this by design once the response is
                ' committed. Let it stand, otherwise IIS appends to the stream we
                ' just wrote binary bytes into.
            End Try
        End Sub

        Private Sub Serve()
            Dim channel As String = Trim(Convert.ToString(Request.QueryString("ch"))).ToUpperInvariant()
            If channel = "MAYA" Then channel = "GOTYME"

            Dim ps As PaymentSetting = New PaymentSettingRepository().GetByChannel(channel)
            If ps Is Nothing OrElse channel = "" Then
                Fail("no image")
                Return
            End If

            Dim bytes As Byte() = ps.QrImageData
            Dim mime As String = If(ps.QrImageMime, "")
            If bytes Is Nothing OrElse bytes.Length = 0 Then
                ' A row uploaded before images moved into the database still points
                ' at a file. Serve that rather than showing a broken image.
                Dim fileUrl As String = UploadedFile(ps.QrImageFile)
                If fileUrl = "" Then
                    Fail("no image")
                    Return
                End If
                Dim path As String = Server.MapPath("~" & fileUrl)
                bytes = IO.File.ReadAllBytes(path)
                mime = MimeFor(path)
            End If
            If bytes Is Nothing OrElse bytes.Length = 0 Then
                Fail("no image")
                Return
            End If

            ' Cheap validators: the browser re-asks only when the bytes change,
            ' which happens only when the admin uploads a different QR.
            Dim etag As String = """" & ps.Channel & "-" & ps.UpdatedAt.Ticks.ToString() & "-" & bytes.Length.ToString() & """"

            Response.Clear()
            Response.Buffer = True
            Response.TrySkipIisCustomErrors = True
            Response.ContentType = If(mime = "", "application/octet-stream", mime)
            Response.AddHeader("Cache-Control", "public, max-age=3600")
            Response.AddHeader("ETag", etag)
            If String.Equals(Request.Headers("If-None-Match"), etag, StringComparison.Ordinal) Then
                Response.StatusCode = 304
                Response.End()
                Return
            End If

            ' HEAD is how a client checks the type and size without the body.
            If Not String.Equals(Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase) Then
                Response.BinaryWrite(bytes)
            End If
            Response.End()
        End Sub

        ''' <summary>
        ''' A 404 with a 1x1 transparent GIF so an img tag degrades to nothing
        ''' instead of the site's error page appearing inside the popup.
        ''' </summary>
        Private Sub Fail(reason As String)
            Response.Clear()
            Response.TrySkipIisCustomErrors = True
            Response.StatusCode = 404
            Response.ContentType = "image/gif"
            Response.AddHeader("X-Qr-Reason", reason)
            Response.BinaryWrite(Convert.FromBase64String(
                "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"))
            Response.End()
        End Sub

        Private Function MimeFor(path As String) As String
            Select Case IO.Path.GetExtension(path).ToLowerInvariant()
                Case ".png" : Return "image/png"
                Case ".jpg", ".jpeg" : Return "image/jpeg"
                Case ".gif" : Return "image/gif"
                Case ".webp" : Return "image/webp"
                Case Else : Return "application/octet-stream"
            End Select
        End Function

    End Class

End Namespace