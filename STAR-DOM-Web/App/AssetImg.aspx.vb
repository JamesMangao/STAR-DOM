Imports System.Web
Imports System.Web.UI
Imports STAR_DOM.Database

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Streams a stored image out of AssetImages (BYTEA). Venue photos fall back to
    ''' the file under Assets\Malls when no row exists; user uploads (product images
    ''' and commission references) are DB-only, and a legacy on-disk upload is
    ''' backfilled into the database the first time it is served.
    '''
    ''' Why a database copy: Assets\Malls ships in Git, but the itinerary grid is
    ''' the first thing a visitor sees, and a stray folder delete -- or a clone
    ''' that never carried the images -- leaves every venue with the gradient
    ''' placeholder. Holding the bytes here means a pg_dump carries them with
    ''' everything else and the photos restore themselves, exactly like the QR
    ''' image in PaymentQr.aspx. Customer and product uploads live here for the same
    ''' reason: a local Uploads folder does not survive a redeploy.
    '''
    ''' Only whitelisted prefixes are served, so this endpoint cannot be turned
    ''' into a general file reader: the query string is checked against the allowed
    ''' roots and against ".." before it is used at all.
    ''' </summary>
    Public Class AssetImgPage
        Inherits Page

        ''' <summary>The folders this endpoint will read from.</summary>
        Private Shared ReadOnly AllowedRoots As String() = {
            "/Assets/Malls/",
            "/Uploads/products/",
            "/Uploads/comm/"
        }

        Private Const MallsRoot As String = "/Assets/Malls/"

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

            ' Database copy first: that row is the durable source. A pre-migration
            ' install without the table falls through to the file copy below.
            Try
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
            Catch
                ' Table absent or unreachable: serve whatever file may still exist.
            End Try

            ' No row: a legacy upload may still be on this machine. Read it, store it
            ' in the database so every future request is DB-served, and send it.
            If bytes Is Nothing OrElse bytes.Length = 0 Then
                Dim rel As String = path.TrimStart("/"c)
                Try
                    Dim physical As String = Server.MapPath("~/" & rel)
                    If IO.File.Exists(physical) Then
                        bytes = IO.File.ReadAllBytes(physical)
                        mime = MimeFor(physical)
                        BackfillInsert(path, bytes, mime)
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
        ''' Best-effort move of an on-disk upload into the database, so that after
        ''' the first view the image no longer depends on the local folder.
        ''' </summary>
        Private Sub BackfillInsert(path As String, bytes As Byte(), mime As String)
            Try
                Db.Exec(
                    "INSERT INTO AssetImages (Path, Data, Mime, ByteSize, UpdatedAt) VALUES (@p, @d, @m, @s, NOW()) " &
                    "ON CONFLICT (Path) DO NOTHING",
                    Db.P("@p", path), Db.P("@d", bytes),
                    Db.P("@m", If(mime = "", "application/octet-stream", mime)),
                    Db.P("@s", bytes.Length))
            Catch
                ' Backfill is opportunistic; a failure must not break the response.
            End Try
        End Sub

        ''' <summary>
        ''' Accepts only a root-relative path under one of AllowedRoots. Anything
        ''' else -- a different folder, a traversal, a query string, a backslash --
        ''' becomes "" and the caller 404s, so the endpoint can never read outside
        ''' the folders it exists for.
        ''' </summary>
        Private Function Normalize(raw As String) As String
            Dim p As String = Trim(Convert.ToString(raw))
            If p = "" Then Return ""
            p = p.Replace("\", "/")
            ' Legacy rows: a commission upload was once stored without its leading
            ' slash ("Uploads/comm/x.jpg"). Normalise that back before routing.
            If p.StartsWith("Uploads/", StringComparison.OrdinalIgnoreCase) Then p = "/" & p
            ' A bare filename (no leading slash) is a legacy venue name only.
            If Not p.StartsWith("/") Then p = MallsRoot & p

            Dim ok As Boolean = False
            For Each root As String In AllowedRoots
                If p.StartsWith(root, StringComparison.OrdinalIgnoreCase) Then
                    ok = True
                    Exit For
                End If
            Next
            If Not ok Then Return ""
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
