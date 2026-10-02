Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    ''' <summary>
    ''' Read/write access to the PaymentSettings table (one row per e-wallet channel:
    ''' GCash and GOtyme). All values are admin-managed from App/Admin/PaymentSettings.aspx.
    ''' </summary>
    Public Class PaymentSettingRepository

        Public Const Gcash As String = "GCASH"
        Public Const GOtyme As String = "GOTYME"

        ''' <summary>
        ''' How the GOtyme channel was stored before the rename. Still read and adopted
        ''' (see ListAll / MigrateLegacyChannel) so an admin who already configured an
        ''' account number or uploaded a QR image does not lose it to a rebrand.
        ''' </summary>
        Public Const LegacyMaya As String = "MAYA"

        ''' <summary>Both rows, ordered GCash first. Missing rows come back as safe defaults.</summary>
        Public Function ListAll() As List(Of PaymentSetting)
            Dim found As New List(Of PaymentSetting)()
            Try
                found = Db.Rows("SELECT * FROM PaymentSettings ORDER BY CASE Channel WHEN 'GCASH' THEN 0 WHEN 'GOTYME' THEN 1 WHEN 'MAYA' THEN 2 ELSE 3 END").
                    Select(Function(r) Map(r)).ToList()
            Catch
                ' Table not created yet (pre-migration install): fall through to defaults.
            End Try
            found.RemoveAll(Function(p) p.Channel <> Gcash AndAlso p.Channel <> GOtyme AndAlso p.Channel <> LegacyMaya)

            ' Adopt the pre-rename MAYA row as GOtyme unless a real GOtyme row already
            ' exists, so the editor shows the settings that were actually configured
            ' instead of a blank default. Save() then persists the rename.
            Dim legacy As PaymentSetting = found.FirstOrDefault(
                Function(p) String.Equals(p.Channel, LegacyMaya, StringComparison.OrdinalIgnoreCase))
            If legacy IsNot Nothing Then
                If found.Any(Function(p) String.Equals(p.Channel, GOtyme, StringComparison.OrdinalIgnoreCase)) Then
                    found.Remove(legacy)
                Else
                    legacy.Channel = GOtyme
                End If
            End If

            For Each ch As String In {Gcash, GOtyme}
                If Not found.Any(Function(p) String.Equals(p.Channel, ch, StringComparison.OrdinalIgnoreCase)) Then
                    found.Add(DefaultFor(ch))
                End If
            Next
            Return found
        End Function

        Public Function GetByChannel(channel As String) As PaymentSetting
            Dim ch As String = If(channel, "").Trim().ToUpperInvariant()
            If ch <> Gcash AndAlso ch <> GOtyme AndAlso ch <> LegacyMaya Then Return Nothing

            Dim found As PaymentSetting = FindRow(ch)
            If found Is Nothing AndAlso ch <> LegacyMaya Then
                ' Nothing is stored under the current key yet, so adopt the pre-rename
                ' row if there is one. Without this the first save after the rename
                ' reads a blank default and wipes the account number and QR image the
                ' admin had already configured under 'MAYA'.
                found = FindRow(LegacyMaya)
            End If
            If found Is Nothing Then Return DefaultFor(ch)
            found.Channel = ch
            Return found
        End Function

        ''' <summary>One mapped row for a channel key, or Nothing when there is none.</summary>
        Private Function FindRow(channel As String) As PaymentSetting
            Try
                Dim rows As List(Of DataRow) = Db.Rows(
                    "SELECT * FROM PaymentSettings WHERE Channel = @c", Db.P("@c", channel))
                If rows IsNot Nothing AndAlso rows.Count > 0 Then Return Map(rows(0))
            Catch
                ' Table not created yet (pre-migration install): treat as unconfigured.
            End Try
            Return Nothing
        End Function

        ''' <summary>Insert or update one channel's settings. Returns False when the channel is unknown.</summary>
        Public Function Save(setting As PaymentSetting) As Boolean
            If setting Is Nothing Then Return False
            Dim ch As String = If(setting.Channel, "").Trim().ToUpperInvariant()
            If ch = LegacyMaya Then ch = GOtyme
            If ch <> Gcash AndAlso ch <> GOtyme Then Return False
            setting.Channel = ch
            EnsureTable()

            Db.Exec(
                "INSERT INTO PaymentSettings (Channel, AccountName, AccountNumber, QrImageFile, QrCaption, QrDisplayMode, IsEnabled, UpdatedBy) " &
                "VALUES (@ch, @an, @num, @qr, @cap, @mode, @en, @by) " &
                "ON CONFLICT (Channel) DO UPDATE SET " &
                "AccountName = EXCLUDED.AccountName, AccountNumber = EXCLUDED.AccountNumber, " &
                "QrImageFile = EXCLUDED.QrImageFile, QrCaption = EXCLUDED.QrCaption, " &
                "QrDisplayMode = EXCLUDED.QrDisplayMode, IsEnabled = EXCLUDED.IsEnabled, UpdatedBy = EXCLUDED.UpdatedBy",
                Db.P("@ch", ch), Db.P("@an", If(setting.AccountName, "")), Db.P("@num", If(setting.AccountNumber, "")),
                Db.P("@qr", If(setting.QrImageFile, "")), Db.P("@cap", If(setting.QrCaption, "")),
                Db.P("@mode", PaymentSetting.NormalizeMode(setting.QrDisplayMode)), Db.P("@en", setting.IsEnabled),
                Db.P("@by", If(setting.UpdatedBy, "")))
            Return True
        End Function

        ''' <summary>
        ''' Replace (or clear) just the QR image of one channel. The bytes go into
        ''' the database so they travel with a pg_dump; passing Nothing clears the
        ''' image. QrImageFile is cleared at the same time so a stale path cannot
        ''' keep pointing at a file that is no longer the live image.
        ''' </summary>
        Public Sub UpdateQrImage(channel As String, imageBytes As Byte(), mime As String, updatedBy As String)
            Dim ch As String = If(channel, "").Trim().ToUpperInvariant()
            If ch = LegacyMaya Then ch = GOtyme
            If ch <> Gcash AndAlso ch <> GOtyme Then Return
            EnsureTable()
            Dim m As String = If(mime, "").Trim()
            If m = "" AndAlso imageBytes IsNot Nothing AndAlso imageBytes.Length > 0 Then m = "image/png"
            Db.Exec("UPDATE PaymentSettings SET QrImageData = @img, QrImageMime = @mime, " &
                    "QrImageFile = '', UpdatedBy = @by WHERE Channel = @c",
                    Db.P("@img", imageBytes), Db.P("@mime", m),
                    Db.P("@by", If(updatedBy, "")), Db.P("@c", ch))
        End Sub

        ''' <summary>Stores an image that is still on disk into the row, then clears the path.</summary>
        Public Sub AdoptFileAsImage(channel As String, imageBytes As Byte(), mime As String, updatedBy As String)
            If imageBytes Is Nothing OrElse imageBytes.Length = 0 Then Return
            UpdateQrImage(channel, imageBytes, mime, updatedBy)
        End Sub

        ''' <summary>True when the channel should be offered at checkout.</summary>
        Public Function IsChannelEnabled(channel As String) As Boolean
            Dim s As PaymentSetting = GetByChannel(channel)
            Return s IsNot Nothing AndAlso s.IsEnabled
        End Function

        ''' <summary>
        ''' Creates the PaymentSettings table on demand, the same way Db.LogError
        ''' bootstraps AppErrors — so the feature works on installs that have not
        ''' re-run Database/supabase_schema.sql yet. Idempotent and cheap.
        ''' </summary>
        Private Sub EnsureTable()
            Try
                Db.Exec(
                    "CREATE TABLE IF NOT EXISTS PaymentSettings (" &
                    "Id SERIAL PRIMARY KEY, " &
                    "Channel VARCHAR(20) NOT NULL UNIQUE, " &
                    "AccountName VARCHAR(120) NOT NULL DEFAULT '', " &
                    "AccountNumber VARCHAR(60) NOT NULL DEFAULT '', " &
                    "QrImageFile VARCHAR(255) NOT NULL DEFAULT '', " &
                    "QrImageData BYTEA, " &
                    "QrImageMime VARCHAR(50) NOT NULL DEFAULT '', " &
                    "QrCaption VARCHAR(120) NOT NULL DEFAULT '', " &
                    "QrDisplayMode VARCHAR(20) NOT NULL DEFAULT 'BOTH', " &
                    "IsEnabled BOOLEAN NOT NULL DEFAULT TRUE, " &
                    "UpdatedBy VARCHAR(120) NOT NULL DEFAULT '', " &
                    "UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP)")
            Catch
                ' A concurrently-created table or a read-only session is fine — the
                ' upsert below will surface a real error if the write cannot proceed.
            End Try
            MigrateLegacyChannel()
        End Sub

        ''' <summary>
        ''' Renames a pre-GOtyme 'MAYA' settings row in place, carrying its account name,
        ''' number, QR image and display mode across to GOtyme. Channel is UNIQUE, so
        ''' this is skipped when a GOtyme row already exists — that row wins. Historical
        ''' Orders/Payments rows are deliberately left alone: their label is resolved
        ''' when they are displayed (PaymentSetting.DisplayName).
        ''' </summary>
        Private Sub MigrateLegacyChannel()
            Try
                Db.Exec(
                    "UPDATE PaymentSettings SET Channel = @to WHERE Channel = @from " &
                    "AND NOT EXISTS (SELECT 1 FROM PaymentSettings WHERE Channel = @to)",
                    Db.P("@to", GOtyme), Db.P("@from", LegacyMaya))
            Catch
                ' Best-effort: a rename that cannot land is not a reason to block a save.
                ' ListAll still reads the legacy row as GOtyme until it does.
            End Try
        End Sub

        Private Function DefaultFor(channel As String) As PaymentSetting
            Return New PaymentSetting With {
                .Channel = channel,
                .AccountName = "STAR:DOM ATELIER / JAMES M.",
                .AccountNumber = If(channel = Gcash, "0917 839 2041", "0998 552 1928"),
                .QrImageFile = "",
                .QrImageData = Nothing,
                .QrImageMime = "",
                .QrCaption = "",
                .QrDisplayMode = "BOTH",
                .IsEnabled = True,
                .UpdatedBy = "",
                .UpdatedAt = Date.MinValue
            }
        End Function

        Private Function Map(r As DataRow) As PaymentSetting
            Return New PaymentSetting With {
                .Id = RowReader.AsInt(r, "Id"),
                .Channel = RowReader.AsStr(r, "Channel"),
                .AccountName = RowReader.AsStr(r, "AccountName"),
                .AccountNumber = RowReader.AsStr(r, "AccountNumber"),
                .QrImageFile = RowReader.AsStr(r, "QrImageFile"),
                .QrImageData = RowReader.AsBytes(r, "QrImageData"),
                .QrImageMime = RowReader.AsStr(r, "QrImageMime"),
                .QrCaption = RowReader.AsStr(r, "QrCaption"),
                .QrDisplayMode = PaymentSetting.NormalizeMode(RowReader.AsStr(r, "QrDisplayMode")),
                .IsEnabled = RowReader.AsBool(r, "IsEnabled"),
                .UpdatedBy = RowReader.AsStr(r, "UpdatedBy"),
                .UpdatedAt = RowReader.AsDate(r, "UpdatedAt")
            }
        End Function

    End Class

End Namespace
