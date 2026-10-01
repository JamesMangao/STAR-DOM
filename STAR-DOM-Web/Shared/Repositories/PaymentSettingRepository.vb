Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    ''' <summary>
    ''' Read/write access to the PaymentSettings table (one row per e-wallet channel:
    ''' GCash and Maya). All values are admin-managed from App/Admin/PaymentSettings.aspx.
    ''' </summary>
    Public Class PaymentSettingRepository

        Public Const Gcash As String = "GCASH"
        Public Const Maya As String = "MAYA"

        ''' <summary>Both rows, ordered GCash first. Missing rows come back as safe defaults.</summary>
        Public Function ListAll() As List(Of PaymentSetting)
            Dim found As New List(Of PaymentSetting)()
            Try
                found = Db.Rows("SELECT * FROM PaymentSettings ORDER BY CASE Channel WHEN 'GCASH' THEN 0 WHEN 'MAYA' THEN 1 ELSE 2 END").
                    Select(Function(r) Map(r)).ToList()
            Catch
                ' Table not created yet (pre-migration install): fall through to defaults.
            End Try
            found.RemoveAll(Function(p) p.Channel <> Gcash AndAlso p.Channel <> Maya)

            For Each ch As String In {Gcash, Maya}
                If Not found.Any(Function(p) String.Equals(p.Channel, ch, StringComparison.OrdinalIgnoreCase)) Then
                    found.Add(DefaultFor(ch))
                End If
            Next
            Return found
        End Function

        Public Function GetByChannel(channel As String) As PaymentSetting
            Dim ch As String = If(channel, "").Trim().ToUpperInvariant()
            If ch <> Gcash AndAlso ch <> Maya Then Return Nothing
            Dim rows As List(Of DataRow) = Nothing
            Try
                rows = Db.Rows("SELECT * FROM PaymentSettings WHERE Channel = @c", Db.P("@c", ch))
            Catch
                rows = Nothing
            End Try
            If rows Is Nothing OrElse rows.Count = 0 Then Return DefaultFor(ch)
            Return Map(rows(0))
        End Function

        ''' <summary>Insert or update one channel's settings. Returns False when the channel is unknown.</summary>
        Public Function Save(setting As PaymentSetting) As Boolean
            If setting Is Nothing Then Return False
            Dim ch As String = If(setting.Channel, "").Trim().ToUpperInvariant()
            If ch <> Gcash AndAlso ch <> Maya Then Return False
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

        ''' <summary>Replace (or clear) just the QR image of one channel.</summary>
        Public Sub UpdateQrImage(channel As String, imageFile As String, updatedBy As String)
            Dim ch As String = If(channel, "").Trim().ToUpperInvariant()
            If ch <> Gcash AndAlso ch <> Maya Then Return
            Db.Exec("UPDATE PaymentSettings SET QrImageFile = @qr, UpdatedBy = @by WHERE Channel = @c",
                    Db.P("@qr", If(imageFile, "")), Db.P("@by", If(updatedBy, "")), Db.P("@c", ch))
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
                    "QrCaption VARCHAR(120) NOT NULL DEFAULT '', " &
                    "QrDisplayMode VARCHAR(20) NOT NULL DEFAULT 'BOTH', " &
                    "IsEnabled BOOLEAN NOT NULL DEFAULT TRUE, " &
                    "UpdatedBy VARCHAR(120) NOT NULL DEFAULT '', " &
                    "UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP)")
            Catch
                ' A concurrently-created table or a read-only session is fine — the
                ' upsert below will surface a real error if the write cannot proceed.
            End Try
        End Sub

        Private Function DefaultFor(channel As String) As PaymentSetting
            Return New PaymentSetting With {
                .Channel = channel,
                .AccountName = "STAR:DOM ATELIER / JAMES M.",
                .AccountNumber = If(channel = Gcash, "0917 839 2041", "0998 552 1928"),
                .QrImageFile = "",
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
                .QrCaption = RowReader.AsStr(r, "QrCaption"),
                .QrDisplayMode = PaymentSetting.NormalizeMode(RowReader.AsStr(r, "QrDisplayMode")),
                .IsEnabled = RowReader.AsBool(r, "IsEnabled"),
                .UpdatedBy = RowReader.AsStr(r, "UpdatedBy"),
                .UpdatedAt = RowReader.AsDate(r, "UpdatedAt")
            }
        End Function

    End Class

End Namespace
