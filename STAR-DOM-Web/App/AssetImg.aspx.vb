Imports System.Web
Imports System.Web.UI
Imports STAR_DOM.Database

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Streams one venue photo out of AssetImages (BYTEA), falling back to the
    ''' file under Assets\Malls when no row exists.
    '''
    ''' Why a database copy: Assets\Malls ships in Git, but the itinerary grid is
    ''' the first thing a visitor sees, and a stray folder delete -- or a clone
    ''' that never carried the images -- leaves every venue with the gradient
    ''' placeholder. Holding the bytes here means a pg_dump carries them with
    ''' everything else and the photos restore themselves, exactly like the QR
    ''' image in PaymentQr.aspx.
    '''
    ''' Only paths under /Assets/Malls/ are served, so this endpoint cannot be
    ''' turned into a general file reader: the query string is checked against
    ''' that prefix and against ".." before it is used at all.
    ''' </summary>
    Public Class AssetImgPage
        Inherits Page

        ''' <summary>The only folder this endpoint will read from.</summary>
        Private Const Root As String = "/Assets/Malls/"

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
            Dim path As String = Normalize(Convert.ToString(Request.QueryString("p")))
            If path = "" Then
                Fail("bad path")
                Return
            End If

            Dim bytes As Byte() = Nothing
            Dim mime As String = ""
            Dim etag As String = ""

            ' Database copy first: that row is the durable source.
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT Data, Mime, ByteSize, Extract(EPOCH FROM UpdatedAt)::bigint AS Up " &
                "FROM AssetImages WHERE Path = @p", Db.P("@p", path))
            If rows.Count > 0 Then
                Dim row As DataRow = rows(0)
                bytes = RowReader.AsBytes(row, "Data")
                mime = RowReader.AsStr(row, "Mime")
                ' EPOCH seconds through Convert, not RowReader: RowReader has no
                ' 64-bit reader, and this value passes Int32 in 2038.
                etag = """" & RowReader.AsInt(row, "ByteSize").ToString() & "-" &
                       Convert.ToInt64(row("Up")).ToString() & """"
            End If

            ' No row: the file may still be on this machine (a fresh clone, or a
            ' row deleted on purpose). Serve it rather than a broken image.
            If bytes Is Nothing OrElse bytes.Length = 0 Then
                Dim rel As String = path.TrimStart("/"c)
                Try
                    Dim physical As String = Server.MapPath("~/" & rel)
                    If IO.File.Exists(physical) Then
                        bytes = IO.File.ReadAllBytes(physical)
                        mime = MimeFor(physical)
                    End If
                Catch
                    ' Unmapped virtual path -- fall through to the 404.
                End Try
                If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                    etag = """" & bytes.Length.ToString() & "-file"""
                End If
            End If

            If bytes Is Nothing OrElse bytes.Length = 0 Then
                Fail("no image")
                Return
            End If

            Response.Clear()
            Response.Buffer = True
            Response.TrySkipIisCustomErrors = True
            Response.ContentType = If(mime = "", "application/octet-stream", mime)
            Response.AddHeader("Cache-Control", "public, max-age=604800")
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
        ''' Accepts only a root-relative path under /Assets/Malls. Anything else --
        ''' a different folder, a traversal, a query string, a backslash -- becomes
        ''' "" and the caller 404s, so the endpoint can never read outside the one
        ''' folder it exists for.
        ''' </summary>
        Private Function Normalize(raw As String) As String
            Dim p As String = Trim(Convert.ToString(raw))
            If p = "" Then Return ""
            p = p.Replace("\", "/")
            ' Accept both a bare filename and the full root-relative path, since
            ' an <img src> may be written either way by a future caller.
            If Not p.StartsWith("/") Then p = Root & p
            If Not p.StartsWith(Root, StringComparison.OrdinalIgnoreCase) Then Return ""
            If p.Contains("..") OrElse p.Contains("?") OrElse p.Contains("#") Then Return ""
            Return p
        End Function

        ''' <summary>
        ''' A 404 with a 1x1 transparent GIF so an img tag degrades to nothing
        ''' instead of the site's error page appearing inside the photo frame.
        ''' </summary>
        Private Sub Fail(reason As String)
            Response.Clear()
            Response.TrySkipIisCustomErrors = True
            Response.StatusCode = 404
            Response.ContentType = "image/gif"
            Response.AddHeader("X-Asset-Reason", reason)
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
