Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class MerchantDashboardPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _reports As New ReportService()
        Private ReadOnly _events As New EventService()
        Private ReadOnly _orders As New OrderService()
        Private ReadOnly _commissions As New CommissionService()
        Private ReadOnly _products As New Repositories.ProductRepository()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                RenderDashboard()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the dashboard: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderDashboard()
            Dim sb As New StringBuilder()

            ' ---- Console header (context bar) ----
            sb.Append("<div class=""page-head"">")
            sb.Append("<div><div class=""ph-title""><span class=""ph-ic"">" & WebUi.Ic("storefront", "lg") & "</span>")
            sb.Append("<div><h1>Merchant Master Console</h1>")
            sb.Append("<p class=""ph-sub"">Synchronized Physical Bazaar Nodes &amp; Digital Commerce Gateway</p></div><span class=""sys-rev"">SYS_REV::2026.09.WIN32</span></div></div>")
            sb.Append("<div class=""ph-actions"">")
            sb.Append("<span class=""term-pill""><span class=""pulse""></span>Terminal POS Link: Active</span>")
            sb.Append(WebUi.BtnHref("/App/Merchant/Reports.aspx", "X-Read POS Snapshot", "ghost", "receipt_long"))
            sb.Append("</div></div>")

            ' ---- Current location controller ----
            Dim current As PopUpEvent = _events.CurrentEvent()
            If current IsNot Nothing Then
                sb.Append(LocationController(current))
            End If

            ' ---- KPI row ----
            Dim eventStats As Dictionary(Of Integer, (rev As Decimal, salesCnt As Integer)) = _reports.EventStats()
            sb.Append("<div class=""grid kpis"">")
            sb.Append(BazaarSalesKpi(current))
            sb.Append(BazaarOrdersKpi(current))
            sb.Append(DigitalMarketplaceKpi())
            sb.Append(HotSellerKpi())
            sb.Append("</div>")

            ' ---- Two-column workbench ----
            sb.Append("<div class=""grid-12"">")
            sb.Append("<div>")
            sb.Append(ScheduledEventsPanel(current))
            sb.Append(HistoricalPanel(eventStats))
            sb.Append(LowStockPanel())
            sb.Append(RecentOrdersPanel())
            sb.Append("</div>")
            sb.Append("<div>")
            sb.Append(RevenueMixPanel())
            sb.Append(QuickNodePanel(current))
            sb.Append("</div>")
            sb.Append("</div>")

            Out.Text = sb.ToString()
        End Sub

        ''' <summary>Top store-status widget with gradient accent, photo overlay and booth pulse.</summary>
        Private Function LocationController(ev As PopUpEvent) As String
            Dim sb As New StringBuilder()
            Dim closing As String = ""
            If ev.EndDate >= Clock.Now Then
                Dim daysLeft As Integer = (ev.EndDate.Date - Clock.Now.Date).Days
                If daysLeft >= 0 Then
                    closing = "<span class=""tag yellow"">" & WebUi.Ic("hourglass_top", "sm") & "<b style=""font-family:var(--font-mono)"">Closing in " & daysLeft.ToString() & "d</b></span>"
                End If
            End If

            sb.Append("<div class=""panel""><div class=""top-accent""></div><div class=""panel-bd"">")
            sb.Append("<div class=""page-head"" style=""margin-bottom:0;border:0;padding:0"">")
            sb.Append("<div style=""display:flex;gap:14px;flex-wrap:wrap"">")
            ' photo
            sb.Append("<div class=""photo-frame"" style=""width:100%;max-width:230px;height:150px;flex-shrink:0"">")
            sb.Append(WebUi.Art(ev.Id * 7 + 3, ev.Name, "height:150px"))
            sb.Append("<div class=""ph-overlay""><span class=""ph-eyebrow"">Active Footprint</span>")
            sb.Append("<span class=""ph-title"">" & WebUi.Esc(ev.BoothNumber) & " Atrium</span></div></div>")
            ' info
            sb.Append("<div style=""flex:1;min-width:240px"">")
            sb.Append("<div style=""display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:6px"">")
            sb.Append("<span class=""eyebrow"" style=""margin:0;color:var(--tertiary)"">CURRENT PHYSICAL STORE LOCATION</span>")
            sb.Append("<span class=""status-chip red""><span class=""dot""></span> " & WebUi.Esc(ev.Status) & "</span>")
            sb.Append("<span class=""sys-rev"">ID: LOC-GLS</span></div>")
            sb.Append("<h2 style=""font-size:22px;font-weight:800;letter-spacing:-.5px;margin:4px 0"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</h2>")
            sb.Append("<div style=""display:flex;flex-wrap:wrap;gap:4px 16px;color:var(--ink-soft);font-size:13px;margin-top:6px"">")
            sb.Append("<span>" & WebUi.Ic("schedule", "sm") & " Daily Hours: <b style=""color:var(--ink)"">" & WebUi.Esc(ev.HoursText) & "</b></span>")
            sb.Append("<span>" & WebUi.Ic("event_available", "sm") & " Active Term: <b style=""color:var(--ink)"">" & WebUi.Esc(ev.WindowText) & "</b></span>")
            sb.Append(closing)
            sb.Append("</div></div></div>")
            ' quick controls
            sb.Append("<div class=""btn-row"" style=""margin-top:14px"">")
            sb.Append(WebUi.BtnHref("/App/Merchant/EventEdit.aspx?id=" & ev.Id.ToString(), "Configure " & WebUi.Esc(ev.BoothNumber), "ghost", "grid_view"))
            sb.Append("</div></div></div>")
            ' booth pulse (full-bleed footer strip, outside .panel-bd)
            sb.Append("<div class=""booth-pulse""><div style=""display:flex;gap:14px;flex-wrap:wrap"">")
            sb.Append("<span>" & WebUi.Ic("wifi_tethering", "sm") & " <b style=""color:var(--ink)"">Cellular Mesh:</b> 99.4% · 18ms</span>")
            sb.Append("<span>Cash Drawer: <b style=""color:var(--ink)"">₱6,400.00 Float</b></span>")
            sb.Append("<span>GCash / Maya Soundbox: <b style=""color:var(--ink)"">Online</b></span>")
            sb.Append("</div><span class=""sync"">LAST SYNC: 14:18 PHT</span></div>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function BazaarSalesKpi(ev As PopUpEvent) As String
            Dim rev As Decimal = 0D
            If ev IsNot Nothing Then rev = _reports.RevenueForEvent(ev.Id)
            Return "<div class=""kpi k-icon"">" &
                   "<span class=""k-ic"" style=""border-radius:9px;background:#ffe0de"">" & WebUi.Ic("payments") & "</span>" &
                   "<div class=""k-label"">Galleria South Bazaar Sales</div>" &
                   "<div class=""k-value"" style=""font-size:26px"">" & Fmt_Php(rev) & "</div>" &
                   "<div class=""k-sub""><span class=""trend-pill up"">" & WebUi.Ic("trending_up", "sm") & " +34% vs SM Santa Rosa</span> &nbsp; Day 3 of 4</div></div>"
        End Function

        Private Function BazaarOrdersKpi(ev As PopUpEvent) As String
            Dim total As Integer = 0
            If ev IsNot Nothing Then total = _reports.EventSalesCount(ev.Id)
            Dim qr As Integer = CInt(Math.Round(total * 0.7D))
            Dim pre As Integer = total - qr
            Dim pctQr As Integer = If(total > 0, CInt(qr * 100 / total), 0)
            pctQr = Math.Max(0, Math.Min(100, pctQr))
            Return "<div class=""kpi k-icon"">" &
                   "<span class=""k-ic"" style=""border-radius:9px;background:var(--yellow-soft);color:var(--on-yellow)"">" & WebUi.Ic("shopping_cart_checkout") & "</span>" &
                   "<div class=""k-label"">Bazaar Orders Tally</div>" &
                   "<div class=""k-value"">" & total.ToString() & " Orders</div>" &
                   "<div class=""k-bar""><i style=""width:" & pctQr.ToString() & "%;background:var(--primary)""></i>" &
                   "<i style=""width:" & (100 - pctQr).ToString() & "%;background:var(--yellow)""></i></div>" &
                   "<div class=""k-legend""><span><span class=""sw"" style=""background:var(--primary)""></span>" & qr.ToString() & " In-Person QR/Cash</span>" &
                   "<span><span class=""sw"" style=""background:var(--yellow)""></span> " & pre.ToString() & " Pre-Orders</span></div></div>"
        End Function

        Private Function DigitalMarketplaceKpi() As String
            Dim rev As Decimal = _reports.RevenueTotal()
            Dim evRev As Decimal = _reports.EventRevenueTotal()
            Dim digital As Decimal = Math.Max(0D, rev - evRev)
            Return "<div class=""kpi k-icon"">" &
                   "<span class=""k-ic"" style=""border-radius:9px;background:var(--tertiary-fixed);color:var(--tertiary)"">" & WebUi.Ic("hub") & "</span>" &
                   "<div class=""k-label"">Digital Marketplace Revenue</div>" &
                   "<div class=""k-value"">" & Fmt_Php(digital) & "</div>" &
                   "<div class=""k-sub""><b style=""color:var(--ink)"">" & _reports.TotalCommissions().ToString() & " Commissions</b> &nbsp;&bull;&nbsp; <b style=""color:var(--ink)"">" &
                   _reports.OrdersCount().ToString() & " Parcel Shipments</b></div></div>"
        End Function

        Private Function HotSellerKpi() As String
            Dim hot = _reports.HotSeller()
            Return "<div class=""kpi k-icon"">" &
                   "<span class=""k-ic"" style=""border-radius:9px;background:var(--surface-mid)"">" & WebUi.Ic("star") & "</span>" &
                   "<div class=""k-label"">Hot Booth Seller (Galleria)</div>" &
                   "<div class=""k-value"" style=""font-size:18px;line-height:1.2"">" & WebUi.Esc(hot.name) & "</div>" &
                   "<div style=""font-size:21px;font-weight:800;color:var(--primary);margin:2px 0;font-family:var(--font-display)"">" & hot.units.ToString() & " Units Sold</div>" &
                   "<div class=""k-sub""><b style=""color:var(--primary)"">Only " & hot.stock.ToString() & " left in booth tray</b> &nbsp;<a href=""/App/Merchant/Products.aspx"" style=""font-weight:800"">Restock " & WebUi.Ic("arrow_forward", "sm") & "</a></div></div>"
        End Function

        Private Function ScheduledEventsPanel(current As PopUpEvent) As String
            Dim sb As New StringBuilder()
            Dim events As List(Of PopUpEvent) = _events.ListEvents()
            sb.Append("<div class=""panel""><div class=""panel-hd"">")
            sb.Append("<h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--primary)"">" & WebUi.Ic("calendar_month", "sm") & "</span> Scheduled Pop-up Tour &amp; Mall Events <span class=""htag"">" & events.Count.ToString() & " SLOTTED</span></h3>")
            sb.Append(WebUi.BtnHref("/App/Merchant/EventEdit.aspx", "+ Schedule New Pop-up Event", "primary"))
            sb.Append("</div>")
            sb.Append("<div class=""board"">")
            sb.Append("<table><thead><tr><th>Event Designation</th><th>Mall Anchor / Venue</th><th>Calendar Dates</th><th>Operating Window</th><th>Readiness State</th><th class=""rowact"" style=""text-align:right"">Quick Actions</th></tr></thead><tbody>")
            For Each ev As PopUpEvent In events
                Dim daysMark As String = ""
                If ev.Status = "UPCOMING" Then
                    Dim d As Integer = (ev.StartDate.Date - Clock.Now.Date).Days
                    If d >= 0 Then daysMark = " (" & d.ToString() & "d left)"
                End If
                Dim isCurrent As Boolean = (current IsNot Nothing AndAlso current.Id = ev.Id)
                sb.Append("<tr>")
                sb.Append("<td><span class=""row-dot"" style=""background:" & If(isCurrent, "var(--primary)", "var(--yellow)") & """></span><span class=""rhead"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</span></td>")
                sb.Append("<td>" & WebUi.Esc(ev.VenueDetail) & "</td>")
                sb.Append("<td class=""mono"">" & WebUi.Esc(ev.WindowText) & "</td>")
                sb.Append("<td>" & WebUi.Esc(ev.HoursText) & "</td>")
                sb.Append("<td>" & EventStateChip(ev.Status & daysMark) & "</td>")
                sb.Append("<td class=""rowact"" style=""text-align:right;white-space:nowrap""><a href=""/App/Merchant/EventEdit.aspx?id=" & ev.Id.ToString() & """>Edit</a>")
                sb.Append("</td></tr>")
            Next
            sb.Append("</tbody></table></div>")
            sb.Append("<div class=""panel-ft""><span><b>Pop-up Logistics:</b> All venues pre-cleared for mall merchant badges.</span><span class=""mono"">SHOWING " & events.Count.ToString() & " SCHEDULED NODES</span></div></div>")
            Return sb.ToString()
        End Function

        Private Function HistoricalPanel(stats As Dictionary(Of Integer, (rev As Decimal, salesCnt As Integer))) As String
            Dim sb As New StringBuilder()
            Dim current As PopUpEvent = _events.CurrentEvent()
            sb.Append("<div class=""panel""><div class=""panel-hd"">")
            sb.Append("<h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--yellow)"">" & WebUi.Ic("history", "sm") & "</span> Pop-up Historical Performance Register <span class=""htag"">ARCHIVED AUDIT TRAILS</span></h3>")
            sb.Append("</div><div class=""board""><table><thead><tr><th>Venue &amp; Run Name</th><th>Term Window</th><th>Gross Take</th><th>Completed Orders</th><th>Top Selling SKU</th><th>Register State</th></tr></thead><tbody>")
            Dim evs As List(Of PopUpEvent) = _events.ListEvents()
            Dim firstRow As Boolean = True
            For Each ev As PopUpEvent In evs
                Dim rev As Decimal = 0D
                Dim orders As Integer = 0
                If stats.ContainsKey(ev.Id) Then
                    rev = stats(ev.Id).rev
                    orders = stats(ev.Id).salesCnt
                End If
                Dim isCurrent As Boolean = (current IsNot Nothing AndAlso current.Id = ev.Id)
                If isCurrent OrElse firstRow Then
                    sb.Append("<tr style=""background:var(--yellow-soft)"">")
                Else
                    sb.Append("<tr>")
                End If
                sb.Append("<td><span class=""row-dot"" style=""background:var(--primary)""></span><span class=""rhead"">" & WebUi.Esc(ev.Name) & "</span></td>")
                sb.Append("<td class=""mono"">" & WebUi.Esc(ev.WindowText) & "</td>")
                sb.Append("<td class=""mono"" style=""color:var(--primary);font-weight:800"">" & Fmt_Php(rev) & "</td>")
                sb.Append("<td style=""font-weight:700"">" & orders.ToString() & " Orders</td>")
                sb.Append("<td>" & WebUi.Esc(LastOrEmpty(ev.BoothNumber)) & "</td>")
                sb.Append("<td>" & EventStateChip(ev.Status) & "</td></tr>")
                firstRow = False
            Next
            sb.Append("</tbody></table></div></div>")
            Return sb.ToString()
        End Function

        Private Function LowStockPanel() As String
            Dim sb As New StringBuilder()
            Dim lowStock As List(Of Product) = _products.LowStock(STAR_DOM.Helpers.Session.CurrentUser.Id)
            sb.Append("<div class=""panel""><div class=""panel-hd""><h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--tertiary)"">" & WebUi.Ic("warning", "sm") & "</span> Low Stock Alerts</h3></div><div class=""panel-bd"">")
            If lowStock.Count = 0 Then
                sb.Append(WebUi.EmptyRow("All good — nothing low."))
            Else
                For Each p As Product In lowStock.Take(6)
                    sb.Append("<div class=""row space-between"" style=""padding:5px 0;border-bottom:1px solid #f3e9e6""><span>" & WebUi.Esc(p.Name) & "</span>" &
                              "<span style=""color:var(--primary);font-weight:700"">" & p.StockQuantity.ToString() & " left</span></div>")
                Next
                sb.Append("<div class=""btn-row"" style=""margin-top:12px"">" & WebUi.BtnHref("/App/Merchant/Products.aspx", "Restock", "ghost", "add_circle") & "</div>")
            End If
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function RecentOrdersPanel() As String
            Dim sb As New StringBuilder()
            Dim recent As List(Of Order) = _reports.RecentOrders(5)
            sb.Append("<div class=""panel""><div class=""panel-hd""><h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--primary)"">" & WebUi.Ic("package_2", "sm") & "</span> Recent Orders</h3></div><div class=""panel-bd"">")
            If recent.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No orders yet."))
            Else
                For Each o As Order In recent
                    sb.Append("<div class=""row space-between"" style=""padding:5px 0;border-bottom:1px solid #f3e9e6"">")
                    sb.Append("<span><a href=""/App/Merchant/Orders.aspx"" style=""font-weight:700"">" & WebUi.Esc(o.OrderNumber) & "</a> · " & WebUi.Esc(o.CustomerName) & "</span>")
                    sb.Append("<span>" & WebUi.Money(o.TotalAmount) & " " & WebUi.Badge(o.Status) & "</span></div>")
                Next
            End If
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function RevenueMixPanel() As String
            Dim sb As New StringBuilder()
            Dim mix = _reports.OmnichannelMix()
            Dim total As Decimal = mix.eventTotal + mix.onlineTotal
            Dim pctEv As Integer = If(total > 0, CInt(mix.eventTotal / total * 100), 0)
            pctEv = Math.Max(0, Math.Min(100, pctEv))
            sb.Append("<div class=""panel""><div class=""panel-hd""><h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--primary)"">" & WebUi.Ic("donut_large", "sm") & "</span> Omnichannel Revenue Mix <span class=""htag"">ACTIVE RUN</span></h3></div><div class=""panel-bd"">")
            sb.Append("<p class=""sub"" style=""margin:0 0 12px"">Event pop-ups vs Direct Web Marketplace (current run)</p>")
            sb.Append(WebUi.Donut(pctEv, 100 - pctEv, "TOTAL GROSS", Fmt_K(total)))
            sb.Append("<div class=""rev-mix"" style=""margin-top:12px"">")
            sb.Append("<div class=""mix-row""><span class=""ml""><span class=""swatch"" style=""background:var(--primary)""></span>Galleria South Pop-up</span>")
            sb.Append("<span class=""mm""><b>" & Fmt_Php(mix.eventTotal) & "</b><small>(" & pctEv.ToString() & "%)</small></span></div>")
            sb.Append("<div class=""mix-row""><span class=""ml""><span class=""swatch"" style=""background:var(--yellow)""></span>Web &amp; Commissions Hub</span>")
            sb.Append("<span class=""mm""><b>" & Fmt_Php(mix.onlineTotal) & "</b><small>(" & (100 - pctEv).ToString() & "%)</small></span></div>")
            sb.Append("</div>")
            sb.Append("<div class=""row space-between sub"" style=""margin:14px 0 4px;font-size:12px""><span>Hourly Peak Flow: 2PM–6PM</span><span style=""color:var(--primary);font-weight:800"">High Footfall</span></div>")
            sb.Append("<div class=""sparkline""><svg viewBox=""0 0 200 40"" preserveAspectRatio=""none"" style=""color:var(--primary)""><path d=""M0,35 Q30,30 50,15 T100,20 T150,5 T200,12"" fill=""none"" stroke=""currentColor"" stroke-width=""2.5""></path><path d=""M0,35 Q30,30 50,15 T100,20 T150,5 T200,12 L200,40 L0,40 Z"" fill=""currentColor"" opacity=""0.15""></path></svg></div>")
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function QuickNodePanel(current As PopUpEvent) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""panel""><div class=""panel-hd""><h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--yellow)"">" & WebUi.Ic("edit_location_alt", "sm") & "</span> Quick Node Configuration <span class=""htag"">DOCK PANEL</span></h3></div><div class=""panel-bd"">")
            sb.Append("<p class=""sub"" style=""margin:0 0 14px"">Re-assign active store presence or schedule the transition payload.</p>")
            sb.Append("<div class=""kv"" style=""grid-template-columns:1fr;gap:8px"">")
            sb.Append("<div class=""field""><label>Pop-up / Event Name</label><input type=""text"" value=""STAR:DOM @ " & WebUi.Attr(If(current IsNot Nothing, current.Name, "—")) & """ readonly /></div>")
            sb.Append("<div class=""grid-2""><div class=""field""><label>Booth / Stall No.</label><input type=""text"" value=""" & WebUi.Attr(If(current IsNot Nothing, current.BoothNumber, "—")) & """ readonly /></div><div class=""field""><label>Op. Window</label><input type=""text"" value=""" & WebUi.Attr(If(current IsNot Nothing, current.WindowText, "—")) & """ readonly /></div></div>")
            sb.Append("<div class=""field""><label>Deployment Status</label><input type=""text"" value=""NOW OPEN"" readonly /></div>")
            sb.Append("</div>")
            sb.Append("<div class=""btn-row"" style=""margin-top:14px"">")
            sb.Append(WebUi.BtnHref("/App/Merchant/Events.aspx", "Sync", "ghost", "sync"))
            sb.Append(WebUi.BtnHref("/App/Merchant/EventEdit.aspx?id=" & If(current IsNot Nothing, current.Id.ToString(), "0"), "Commit Node Change", "primary", "verified"))
            sb.Append("</div></div></div>")
            Return sb.ToString()
        End Function

        Private Function EventStateChip(state As String) As String
            Dim s As String = Convert.ToString(state).ToUpperInvariant()
            Dim label As String = Convert.ToString(state)
            If s.StartsWith("UPCOMING") Then
                Return "<span class=""status-chip yellow""><span class=""dot""></span> UPCOMING</span>"
            ElseIf s.StartsWith("NOW OPEN") OrElse s.StartsWith("ACTIVE") OrElse s.StartsWith("LIVE") Then
                Return "<span class=""status-chip red""><span class=""dot""></span> NOW OPEN</span>"
            ElseIf s.StartsWith("ENDED") OrElse s.StartsWith("CANCELLED") Then
                Return "<span class=""status-chip gray""><span class=""dot""></span> " & WebUi.Esc(label) & "</span>"
            Else
                Return "<span class=""status-chip gray""><span class=""dot""></span> " & WebUi.Esc(label) & "</span>"
            End If
        End Function

        Private Function LastOrEmpty(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return "—"
            Return value
        End Function

        Private Function Fmt_Php(v As Decimal) As String
            Return "₱" & v.ToString("N2")
        End Function

        Private Function Fmt_K(v As Decimal) As String
            If v >= 1000D Then Return "₱" & (v / 1000D).ToString("0.#") & "k"
            Return Fmt_Php(v)
        End Function

        Private Function Table(headers() As String) As StringBuilder
            Return Nothing
        End Function

        Private Function Table(p1 As String, p2 As String, p3 As String, p4 As String, p5 As String, p6 As String,
                               rowWriter As Action(Of StringBuilder)) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {p1, p2, p3, p4, p5, p6}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            rowWriter(sb)
            sb.Append("</tbody></table></div>")
            Return sb.ToString()
        End Function

    End Class

End Namespace
