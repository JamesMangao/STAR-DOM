Imports System.IO
Imports STAR_DOM.Database

Namespace STAR_DOM.Repositories

    ''' <summary>
    ''' Writes user-uploaded images (product photos, commission references) into the
    ''' AssetImages blob table instead of the local Uploads folder, so a redeploy --
    ''' or a fresh clone without the folder -- still shows every picture. The bytes
    ''' are served back through App/AssetImg.aspx, exactly like venue photos.
    ''' </summary>
    Public Class AssetImageRepository

        ''' <summary>Refuse anything larger; a product/reference photo never legitimately needs more.</summary>
        Public Const MaxBytes As Integer = 6 * 1024 * 1024

        ''' <summary>
        ''' Stores one image under <paramref name="folder"/> and returns its
        ''' root-relative path ("/Uploads/products/&lt;id&gt;.png"), or "" when the
        ''' input is empty or not an allowed image. Set <paramref name="allowDocuments"/>
        ''' to also accept PDFs (commission references can be spec sheets).
        ''' </summary>
        Public Function Save(folder As String, fileName As String, bytes As Byte(), mime As String,
                             Optional allowDocuments As Boolean = False) As String
            If bytes Is Nothing OrElse bytes.Length = 0 Then Return ""
            If bytes.Length > MaxBytes Then Return ""

            Dim ext As String = ExtensionFor(fileName, mime)
            Dim m As String = MimeFor(ext)
            If m = "application/pdf" AndAlso Not allowDocuments Then Return ""
            If m = "" Then Return ""

            Dim root As String = "/" & folder.Trim("/"c) & "/"
            Dim path As String = root & Guid.NewGuid().ToString("N") & ext

            Db.Exec(
                "INSERT INTO AssetImages (Path, Data, Mime, ByteSize, UpdatedAt) VALUES (@p, @d, @m, @s, NOW()) " &
                "ON CONFLICT (Path) DO UPDATE SET Data = EXCLUDED.Data, Mime = EXCLUDED.Mime, " &
                "ByteSize = EXCLUDED.ByteSize, UpdatedAt = NOW()",
                Db.P("@p", path), Db.P("@d", bytes), Db.P("@m", m), Db.P("@s", bytes.Length))
            Return path
        End Function

        ''' <summary>Removes a stored image; harmless when the path is unknown or empty.</summary>
        Public Sub Delete(path As String)
            If String.IsNullOrWhiteSpace(path) Then Return
            Try
                Db.Exec("DELETE FROM AssetImages WHERE Path = @p", Db.P("@p", path))
            Catch
                ' A missing row is not an error worth surfacing to a merchant.
            End Try
        End Sub

        ''' <summary>The extension to store under, favouring the uploaded name and
        ''' falling back to the declared content type. "" when neither is usable.</summary>
        Private Function ExtensionFor(fileName As String, mime As String) As String
            Dim ext As String = IO.Path.GetExtension(If(fileName, "")).ToLowerInvariant()
            If MimeFor(ext) <> "" Then Return ext

            Select Case If(mime, "").Trim().ToLowerInvariant()
                Case "image/png" : Return ".png"
                Case "image/jpeg", "image/jpg", "image/pjpeg" : Return ".jpg"
                Case "image/gif" : Return ".gif"
                Case "image/webp" : Return ".webp"
                Case "application/pdf" : Return ".pdf"
                Case Else : Return ""
            End Select
        End Function

        ''' <summary>Content type for a stored extension, or "" when it is not an allowed image.</summary>
        Private Function MimeFor(ext As String) As String
            Select Case If(ext, "").ToLowerInvariant()
                Case ".png" : Return "image/png"
                Case ".jpg", ".jpeg" : Return "image/jpeg"
                Case ".gif" : Return "image/gif"
                Case ".webp" : Return "image/webp"
                Case ".pdf" : Return "application/pdf"
                Case Else : Return ""
            End Select
        End Function

    End Class

End Namespace
