Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class MarketplacePage
        Inherits Page

        Protected Out As Literal

        Private ReadOnly _catalog As New CatalogService()
        Private ReadOnly _events As New EventService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Try
                RenderPage()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the marketplace: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderPage()
            Dim sb As New StringBuilder()
            Dim featured As List(Of Product) = _catalog.Featured(8)
            Dim categories As List(Of Category) = _catalog.ListCategories()
            Dim current As PopUpEvent = _events.CurrentEvent()
            Dim upcoming As List(Of PopUpEvent) = _events.ListUpcoming()

            ' ---------- hero ----------
            sb.Append("<div class=""hero"">")
            sb.Append("<div class=""row space-between"" style=""align-items:stretch;gap:26px"">")
            sb.Append("<div style=""flex:1.4;min-width:320px"">")
            sb.Append(WebUi.Pill("OMNICHANNEL ARTISAN PLATFORM • PHILIPPINES", "yellow"))
            sb.Append("<h1 style=""font-size:38px;line-height:1.12;margin:12px 0 10px;letter-spacing:-1px"">Turn Your Ideas Into " &
                      "<span class=""grad-text"">Living Art.</span></h1>")
            sb.Append("<p class=""sub"" style=""font-size:16px;max-width:560px"">Shop artisan prints, handcrafted stickers, " &
                      "limited merch, and custom commissions online or in-person at our physical pop-up tours.</p>")
            sb.Append("<div class=""frow"">")
            sb.Append(WebUi.BtnHref("/App/Catalog.aspx", "Explore Marketplace Catalog"))
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx", "Visit Current Pop-up Booth", "secondary", "storefront"))
            sb.Append(WebUi.BtnHref("/App/CommissionHub.aspx", "Request Custom Commission", "ghost", "draw"))
            sb.Append("</div>")
            Dim rep As New ReportService()
            Dim totalCreators As Integer = Math.Max(1, rep.TotalCreators())
            Dim totalSalesVol As Decimal = rep.EventRevenueTotal() + rep.RevenueTotal()
            Dim formattedVol As String = If(totalSalesVol >= 1000D, "₱" & (totalSalesVol / 1000D).ToString("0.#") & "K+", "₱" & totalSalesVol.ToString("N0"))

            sb.Append("<div class=""grid kpis"" style=""grid-template-columns:repeat(3,minmax(130px,1fr));max-width:600px;margin:20px 0 0"">")
            sb.Append("<div class=""kpi k-icon""><span class=""k-ic"" style=""background:#ffe0de"">" & WebUi.Ic("groups") & "</span><div class=""k-value"" style=""font-size:26px"">" & totalCreators.ToString() & "+</div><div class=""k-label"">Active Indie Creators</div></div>")
            sb.Append("<div class=""kpi k-icon""><span class=""k-ic"" style=""background:var(--yellow-soft);color:var(--on-yellow)"">" & WebUi.Ic("paid") & "</span><div class=""k-value"" style=""font-size:26px"">" & formattedVol & "</div><div class=""k-label"">Bazaar Sales Volume</div></div>")
            sb.Append("<div class=""kpi k-icon""><span class=""k-ic"" style=""background:var(--tertiary-fixed);color:var(--tertiary)"">" & WebUi.Ic("verified") & "</span><div class=""k-value"" style=""font-size:26px"">100%</div><div class=""k-label"">Verified PH Guild</div></div>")
            sb.Append("</div></div>")

            ' hero collage — first few featured products
            sb.Append("<div style=""flex:1;min-width:280px;max-width:430px"">")
            If featured.Count >= 2 Then
                sb.Append(ProductSpotlight(featured(0), featured(1)))
            End If
            sb.Append("</div></div></div>")

            ' ---------- current location strip ----------
            If current IsNot Nothing Then
                sb.Append(LocationStrip(current))
            End If

            ' ---------- catalog discovery ----------
            sb.Append("<div class=""sec-head"" style=""margin-top:26px"">")
            sb.Append("<div>")
            sb.Append("<div class=""eyebrow"">CATALOG DISCOVERY</div>")
            sb.Append("<h2>Handcrafted Products &amp; Art</h2>")
            sb.Append("</div>")
            sb.Append("<div class=""sub"">" & featured.Count.ToString() & " of " & _catalog.ListProducts().Count.ToString() &
                      " SKUs · <span style=""color:var(--green);font-weight:700"">" & WebUi.Ic("sync", "sm") & " Live POS sync</span></div>")
            sb.Append("</div>")

            Dim chips As New StringBuilder()
            chips.Append(WebUi.OutLink("/App/Catalog.aspx", "ALL", True))
            For Each c As Category In categories
                chips.Append(WebUi.OutLink("/App/Catalog.aspx?cat=" & c.Id.ToString(), c.Name.ToUpperInvariant(), False))
            Next
            sb.Append("<div class=""chips"">" & chips.ToString() & "</div>")

            If featured.Count > 0 Then
                sb.Append("<div class=""grid cards4"">")
                For Each p As Product In featured
                    sb.Append(ProductCard(p))
                Next
                sb.Append("</div>")
            Else
                sb.Append(WebUi.EmptyRow("No products available yet — check back soon."))
            End If
            sb.Append("<div style=""text-align:center;margin:18px 0 6px"">")
            sb.Append(WebUi.BtnHref("/App/Catalog.aspx", "Browse Full Products Catalog", "secondary"))
            sb.Append("</div>")

            ' ---------- commission slots ----------
            Dim slots As List(Of CommissionSlotView) = _catalog.CommissionSlots()
            If slots.Count > 0 Then
                sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
                sb.Append("<div>")
                sb.Append("<div class=""eyebrow"">CUSTOM CREATIONS, DELIVERED TO YOU</div>")
                sb.Append("<h2>Open Commission Slots</h2>")
                sb.Append("</div>")
                sb.Append("<div class=""sub"">Queue status: Fast turnaround (3–5 days)</div>")
                sb.Append("</div>")
                sb.Append("<div class=""grid cards"">")
                For Each s As CommissionSlotView In slots
                    sb.Append(WebUi.CommissionSlotCard(s, "", "/App/CommissionHub.aspx", "View on Commission Hub"))
                Next
                sb.Append("</div>")
            End If

            ' ---------- tour itinerary ----------
            If upcoming.Count > 0 Then
                sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
                sb.Append("<div>")
                sb.Append("<div class=""eyebrow"">TOUR ITINERARY</div>")
                sb.Append("<h2>Upcoming 2026 Pop-up Roadshow</h2>")
                sb.Append("</div>")
                sb.Append("<div class=""sub"">Updated daily from central terminal registry</div>")
                sb.Append("</div>")
                sb.Append(ItineraryTable(upcoming))
            End If

            Out.Text = sb.ToString()
        End Sub

        ' ---------- building blocks ----------

        Private Function ProductSpotlight(a As Product, b As Product) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card"" style=""overflow:hidden;padding:0"">")
            sb.Append("<div class=""badge"" style=""background:var(--primary);color:#fff;position:absolute;margin:10px"">POP-UP SPOTLIGHT</div>")
            sb.Append(WebUi.ProductImg(a.PrimaryImageFile, a.Id, a.Name, "height:210px"))
            sb.Append("<div style=""padding:12px 14px;display:flex;justify-content:space-between;align-items:center;gap:8px"">" &
                      "<b>" & WebUi.Esc(a.Name) & "</b> <span style=""color:var(--primary);font-weight:800"">" & WebUi.Money(a.EffectivePrice) & "</span></div>")
            sb.Append(If(a.StockQuantity <= a.LowStockThreshold, "<div class=""stockline"" style=""padding:0 14px 12px;color:var(--primary)"">Only " &
                        a.StockQuantity.ToString() & " remaining!</div>", "<div class=""stockline"" style=""padding:0 14px 12px"">" &
                        a.StockQuantity.ToString() & " in booth stock</div>"))
            sb.Append("</div>")
            sb.Append("<div class=""grid"" style=""grid-template-columns:1fr 1fr;gap:12px;margin-top:12px"">")
            sb.Append("<div class=""card"" style=""padding:0;overflow:hidden"">" & WebUi.ProductImg(b.PrimaryImageFile, b.Id, b.Name, "height:120px") &
                      "<div style=""padding:8px 10px;font-size:12px""><b>" & WebUi.Esc(b.Name) & "</b><br>" & WebUi.Money(b.EffectivePrice) & "</div></div>")
            sb.Append("<div class=""card"" style=""background:var(--yellow);border-color:#eec200;display:flex;flex-direction:column;justify-content:center;gap:4px"">" &
                      "<span class=""k-label"" style=""font-size:9px;letter-spacing:.12em;font-weight:800"">BAZAAR EXCLUSIVE</span>" &
                      "<span style=""font-weight:800;font-size:20px"">" & WebUi.Money(b.EffectivePrice) & "</span>" &
                      "<span style=""font-size:12px"">On-site pickup at booth</span></div>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function LocationStrip(ev As PopUpEvent) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card"" style=""display:flex;align-items:center;gap:18px;flex-wrap:wrap;border-color:#eec200"">")
            sb.Append("<span class=""pulse""></span>")
            sb.Append("<div style=""flex:1;min-width:240px"">")
            sb.Append("<div class=""eyebrow"" style=""display:block"">" & WebUi.Ic("pin_drop", "sm") & " CURRENT PHYSICAL LOCATION</div>")
            sb.Append("<b style=""font-size:19px;display:block"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</b>")
            sb.Append("<div class=""sub"">" & WebUi.Esc(ev.VenueDetail) & " · " & WebUi.Esc(ev.HoursText) & " · Booth " &
                      WebUi.Esc(ev.BoothNumber) & "</div>")
            sb.Append("</div>")
            sb.Append(WebUi.Badge("NOW OPEN"))
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx", "View Booth Location & Map", "secondary", "map"))
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function ProductCard(p As Product) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""pcard"">")
            sb.Append("<div class=""artwrap"" style=""position:relative"">")
            sb.Append(WebUi.ProductImg(p.PrimaryImageFile, p.Id, p.Name, "height:185px"))
            sb.Append("<div class=""badges"">")
            sb.Append(If(p.BadgeLabel <> "", "<span class=""badge warn"">" & WebUi.Esc(p.BadgeLabel) & "</span>", ""))
            sb.Append(If(p.HasDiscount, "<span class=""badge live"">" & p.DiscountPercent.ToString() & "% OFF</span>", ""))
            sb.Append("</div></div>")
            sb.Append("<div class=""pbody"">")
            sb.Append("<span class=""brand"">" & WebUi.Esc(p.BrandName) & "</span>")
            sb.Append("<a class=""pname"" href=""/App/Product.aspx?id=" & p.Id.ToString() & """ style=""color:inherit"">" & WebUi.Esc(p.Name) & "</a>")
            sb.Append("<span class=""pdesc"">" & WebUi.Esc(p.MaterialDetails) & "</span>")
            If p.RatingCount > 0 Then
                sb.Append("<span class=""stars"">" & WebUi.Stars(CInt(Math.Round(p.RatingAvg))) & " <small style=""color:var(--ink-soft)"">(" & p.RatingCount.ToString() & ")</small></span>")
            End If
            sb.Append("<div class=""pfoot"">")
            sb.Append(WebUi.Money(p.EffectivePrice))
            sb.Append(WebUi.BtnHref("/App/Cart.aspx?add=" & p.Id.ToString() & "&q=1&ret=/App/Marketplace.aspx", "Add", "primary", "add_shopping_cart",
                                   WebUi.AuthGateAttrs(p.Name, p.PrimaryImageFile, p.Id, p.EffectivePrice)))
            sb.Append("</div>")
            sb.Append(If(p.StockQuantity <= p.LowStockThreshold, "<span class=""stockline"" style=""color:var(--primary)"">Only " &
                        p.StockQuantity.ToString() & " remaining!</span>", "<span class=""stockline"">" &
                        p.StockQuantity.ToString() & " in booth stock</span>"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function ItineraryTable(events As List(Of PopUpEvent)) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"STATUS", "LOCATION / VENUE", "REGION", "EVENT DATES", "BOOTH INFO"}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            For Each ev As PopUpEvent In events
                Dim label As String = If(ev.Status = "NOW OPEN", "ACTIVE TODAY", ev.Status)
                sb.Append("<tr><td>" & WebUi.Badge(label) & "</td>")
                sb.Append("<td><b>" & WebUi.Esc(ev.Name) & "</b><br><span class=""sub"" style=""font-size:12px"">" &
                          WebUi.Esc(ev.VenueDetail) & "</span></td>")
                sb.Append("<td>" & WebUi.Esc(ev.CityLabel) & "</td>")
                sb.Append("<td>" & WebUi.Esc(ev.WindowText) & "</td>")
                sb.Append("<td>" & WebUi.Esc(ev.BoothNumber) & "</td></tr>")
            Next
            sb.Append("</tbody></table></div>")
            Return sb.ToString()
        End Function

    End Class

End Namespace
