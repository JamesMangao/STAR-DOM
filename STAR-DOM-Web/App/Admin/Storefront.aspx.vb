Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Admin console page for storefront presentation. Its one job right now is
    ''' choosing the product shown in the "POP-UP SPOTLIGHT" card at the top of the
    ''' landing page (and the marketplace hero). The choice is stored in SiteSettings
    ''' and read back by CatalogService.Spotlight, which falls back to the store's
    ''' best-selling active product — so the hero is never empty and always reflects
    ''' what actually sells, without anything needing to be flagged "featured".
    ''' </summary>
    Public Class AdminStorefrontPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _settings As New SiteSettingRepository()
        Private ReadOnly _products As New ProductRepository()
        Private ReadOnly _catalog As New CatalogService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireAdmin()
            Try
                If Guard.IsPost() Then
                    HandlePost()
                    Return
                End If
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load storefront settings: " & ex.Message)
            End Try
        End Sub

        ' ---------------- POST: save the chosen spotlight ----------------

        Private Sub HandlePost()
            If Not Csrf.IsValidRequest() Then
                Out.Text = WebUi.AlertBox("Your session expired — please try again.")
                Render()
                Return
            End If
            ' The Save button is the discriminator: the page renders no other submit,
            ' so a stray post (e.g. Enter in a field) must not silently rewrite the
            ' setting. Absent button means "not this action" — render the page again.
            If Request.Form("saveSpotlight") Is Nothing Then
                Render()
                Return
            End If

            Dim id As Integer = 0
            Integer.TryParse(Convert.ToString(Request.Form("spotlightId")), id)
            _settings.SetSpotlightProductId(id, UpdatedBy())
            Session("flash_msg") = If(id > 0, "Landing spotlight updated.", "Landing spotlight set to automatic.")
            Session("flash_ok") = True
            Response.Redirect("/App/Admin/Storefront.aspx", True)
        End Sub

        ' ---------------- GET: the editor ----------------

        Private Sub Render()
            Dim pinnedId As Integer = _settings.GetSpotlightProductId()
            Dim products As List(Of Product) = _products.ListActive()
            Dim current As Product = _catalog.Spotlight()

            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Storefront",
                                    "SYSTEM ADMIN · LANDING PRESENTATION",
                                    "Choose what the storefront promotes at the top of the landing and marketplace pages."))
            sb.Append(Flash())

            ' --- spotlight chooser ---
            sb.Append("<div class=""card"" style=""margin-bottom:16px"">")
            sb.Append(Csrf.HiddenField())
            sb.Append("<h3 style=""margin-bottom:4px"">" & WebUi.Ic("star", "sm") & " Landing spotlight</h3>")
            sb.Append("<div class=""sub"" style=""margin-bottom:14px"">The single &ldquo;POP-UP SPOTLIGHT&rdquo; product card at the top of the storefront.</div>")

            sb.Append("<div class=""field"" style=""max-width:540px;margin:0""><label for=""spotlightId"">Spotlight product</label>")
            sb.Append("<select id=""spotlightId"" name=""spotlightId"">")
            sb.Append("<option value=""0""" & If(pinnedId <= 0, " selected", "") & ">Automatic — best-selling product</option>")
            For Each p As Product In products
                sb.Append("<option value=""" & p.Id.ToString() & """" & If(p.Id = pinnedId, " selected", "") & ">" &
                          WebUi.Esc(p.Name) & "  ·  " & WebUi.Esc(p.Sku) & "  ·  " & WebUi.Esc(p.CategoryName) & "</option>")
            Next
            sb.Append("</select></div>")

            sb.Append("<button class=""btn primary"" type=""submit"" name=""saveSpotlight"" value=""1"" style=""margin-top:14px"">" &
                      "<span class=""ic ms"">save</span><span>Save Spotlight</span></button>")
            sb.Append("</div>")

            ' --- live preview ---
            sb.Append("<div class=""card""><h3 style=""margin-bottom:12px"">" & WebUi.Ic("visibility", "sm") & " Preview</h3>")
            If current Is Nothing Then
                sb.Append("<div class=""sub"">No active product to show — add or activate a product first.</div>")
            Else
                sb.Append("<div style=""display:flex;gap:16px;align-items:center;flex-wrap:wrap"">")
                sb.Append("<div style=""width:190px;flex:0 0 auto;border:1px solid var(--line);border-radius:12px;overflow:hidden;background:#fff"">")
                sb.Append(WebUi.ProductImg(current.PrimaryImageFile, current.Id, current.Name, "height:150px"))
                sb.Append("<div style=""padding:8px 10px;display:flex;justify-content:space-between;gap:8px;align-items:center""><b style=""font-size:13px"">" &
                          WebUi.Esc(current.Name) & "</b> " & WebUi.Money(current.BasePrice) & "</div>")
                sb.Append("</div>")
                sb.Append("<div class=""sub"" style=""font-size:12.5px;max-width:360px"">")
                If pinnedId > 0 Then
                    sb.Append("Showing the pinned product on the storefront hero.")
                Else
                    sb.Append("Automatic: showing <b>" & WebUi.Esc(current.Name) & "</b>, the store's best-selling product.")
                End If
                If current.StockQuantity <= current.LowStockThreshold Then
                    sb.Append("<br>Heads up — this product is low on stock (" & current.StockQuantity.ToString() & " left).")
                End If
                sb.Append("</div></div>")
            End If
            sb.Append("</div>")

            Out.Text = sb.ToString()
        End Sub

        Private Function UpdatedBy() As String
            If STAR_DOM.Helpers.Session.CurrentUser IsNot Nothing Then
                Return STAR_DOM.Helpers.Session.CurrentUser.Username
            End If
            Return ""
        End Function

        ''' <summary>Same one-shot flash message convention used across the app.</summary>
        Private Function Flash() As String
            Dim msg As String = Convert.ToString(Session("flash_msg"))
            If msg = "" Then Return ""
            Session("flash_msg") = Nothing
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_ok") = Nothing
            Return WebUi.AlertBox(msg, If(ok, "ok", "err"))
        End Function

    End Class

End Namespace
