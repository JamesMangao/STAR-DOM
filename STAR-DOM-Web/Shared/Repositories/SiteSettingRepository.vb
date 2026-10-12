Imports STAR_DOM.Database

Namespace STAR_DOM.Repositories

    ''' <summary>
    ''' Read/write access to the SiteSettings table — a tiny key/value store for
    ''' admin-managed storefront options that do not deserve a table of their own
    ''' (currently the landing-page "pop-up spotlight" product). Values are always
    ''' stored as text; callers parse them back into the type they need.
    ''' </summary>
    Public Class SiteSettingRepository

        ''' <summary>Key holding the Product.Id the admin pinned to the landing hero. "0"/"" means auto.</summary>
        Public Const SpotlightProductIdKey As String = "SPOTLIGHT_PRODUCT_ID"

        ''' <summary>
        ''' One setting's raw text, or <paramref name="fallback"/> when the key is
        ''' unset (or the table does not exist yet on a pre-migration install).
        ''' </summary>
        Public Function Fetch(key As String, fallback As String) As String
            If String.IsNullOrWhiteSpace(key) Then Return fallback
            Try
                Dim rows As List(Of DataRow) = Db.Rows(
                    "SELECT SettingValue FROM SiteSettings WHERE SettingKey = @k", Db.P("@k", key))
                If rows IsNot Nothing AndAlso rows.Count > 0 Then
                    Dim v As String = RowReader.AsStr(rows(0), "SettingValue")
                    If v <> "" Then Return v
                End If
            Catch
                ' Table not created yet (pre-migration install): fall through to the fallback.
            End Try
            Return fallback
        End Function

        ''' <summary>Insert or overwrite one setting.</summary>
        Public Sub Store(key As String, value As String, updatedBy As String)
            If String.IsNullOrWhiteSpace(key) Then Return
            EnsureTable()
            Db.Exec(
                "INSERT INTO SiteSettings (SettingKey, SettingValue, UpdatedBy) VALUES (@k, @v, @by) " &
                "ON CONFLICT (SettingKey) DO UPDATE SET " &
                "SettingValue = EXCLUDED.SettingValue, UpdatedBy = EXCLUDED.UpdatedBy, UpdatedAt = CURRENT_TIMESTAMP",
                Db.P("@k", key), Db.P("@v", If(value, "")), Db.P("@by", If(updatedBy, "")))
        End Sub

        ''' <summary>The Product.Id pinned to the landing hero, or 0 when left on automatic.</summary>
        Public Function GetSpotlightProductId() As Integer
            Dim id As Integer = 0
            Integer.TryParse(Fetch(SpotlightProductIdKey, "0"), id)
            Return id
        End Function

        ''' <summary>Pins a product to the landing hero. Pass 0 to go back to automatic.</summary>
        Public Sub SetSpotlightProductId(productId As Integer, updatedBy As String)
            Store(SpotlightProductIdKey, Math.Max(0, productId).ToString(), updatedBy)
        End Sub

        ''' <summary>
        ''' Creates the SiteSettings table on demand, the same way Db.LogError
        ''' bootstraps AppErrors and PaymentSettingRepository bootstraps its table —
        ''' so the feature works on installs that have not re-run the schema yet.
        ''' Idempotent and cheap.
        ''' </summary>
        Private Sub EnsureTable()
            Try
                Db.Exec(
                    "CREATE TABLE IF NOT EXISTS SiteSettings (" &
                    "SettingKey VARCHAR(60) PRIMARY KEY, " &
                    "SettingValue TEXT NOT NULL DEFAULT '', " &
                    "UpdatedBy VARCHAR(120) NOT NULL DEFAULT '', " &
                    "UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP)")
            Catch
                ' A concurrently-created table or a read-only session is fine — the
                ' upsert below will surface a real error if the write cannot proceed.
            End Try
        End Sub

    End Class

End Namespace
