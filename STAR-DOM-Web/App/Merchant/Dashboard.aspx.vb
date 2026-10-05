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

            ' ---- Page header ----
            ' Standard WebUi.Section header, like every other Merchant Studio page
            ' (Events, Products, Orders, Reviews). The bespoke "Merchant Master
            ' Console" console bar is gone.
            sb.Append(WebUi.Section("Merchant Dashboard", "MERCHANT STUDIO / OVERVIEW",
                                    "Your pop-up booths, marketplace sales and studio queue at a glance."))
            ' No POS terminal status strip and no X-Read button here: sales figures come
            ' from Event Sales Reports, which is the one place in-person takings are read.

            ' ---- Current location controller ----
            Dim current As PopUpEvent = _events.CurrentEvent()
            If current IsNot Nothing Then
                sb.Append(LocationController(current))
            End If

            ' ---- KPI row ----
            ' Only the digital marketplace tile survives. The booth-sales, order-tally
            ' and hot-seller tiles, the omnichannel donut and the Quick Node panel
            ' duplicated Event Sales Reports and repeated values the merchant has no
            ' way to act on, so they were removed.
            Dim eventStats As Dictionary(Of Integer, (rev As Decimal, salesCnt As Integer)) = _reports.EventStats()
            sb.Append("<div class=""grid kpis"">")
            sb.Append(DigitalMarketplaceKpi())
            sb.Append("</div>")

            ' ---- Workbench ----
            sb.Append(ScheduledEventsPanel(current))
            sb.Append(HistoricalPanel(eventStats))
            sb.Append(LowStockPanel())
            sb.Append(RecentOrdersPanel())

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
            sb.Append("<span class=""ph-title"">" & WebUi.BoothLabel(ev.BoothNumber) & "</span></div></div>")
            ' info
            sb.Append("<div style=""flex:1;min-width:240px"">")
            sb.Append("<div style=""display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:6px"">")
            sb.Append("<span class=""eyebrow"" style=""margin:0;color:var(--tertiary)"">CURRENT PHYSICAL STORE LOCATION</span>")
            sb.Append("<span class=""status-chip red""><span class=""dot""></span> " & WebUi.Esc(ev.Status) & "</span>")
            sb.Append("<span class=""sys-rev"">ID: LOC-GLS</span></div>")
            sb.Append("<h2 style=""font-size:22px;font-weight:800;letter-spacing:-.5px;margin:4px 0"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</h2>")
            sb.Append("<div style=""display:flex;flex-wrap:wrap;gap:4px 16px;color:var(--ink-soft);font-size:13px;margin-top:6px"">")
            sb.Append("<span>" & WebUi.Ic("schedule", "sm") & " Daily Hours: <b style=""color:var(--ink)"">" & WebUi.Esc(ev.HoursText) & "</b></span>")
            sb.Append(closing)
            sb.Append("</div></div></div>")
            ' quick controls
            sb.Append("<div class=""btn-row"" style=""margin-top:14px"">")
            sb.Append(WebUi.BtnHref("/App/Merchant/EventEdit.aspx?id=" & ev.Id.ToString(), "Configure " & WebUi.BoothLabel(ev.BoothNumber), "ghost", "grid_view"))
            sb.Append("</div></div></div>")
            ' No booth-pulse footer strip: the cash-drawer float, cellular mesh and
            ' soundbox readouts were POS terminal status, not anything the app tracks.
            sb.Append("</div>")
            Return sb.ToString()
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
            sb.Append("</div><div class=""board""><table><thead><tr><th>Venue &amp; Run Name</th><th>Term Window</th><th>Gross Take</th><th>Completed Orders</th><th>Top Selling SKU</th></tr></thead><tbody>")
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
                sb.Append("<td>" & WebUi.Esc(LastOrEmpty(ev.BoothNumber)) & "</td></tr>")
                firstRow = False
            Next
            sb.Append("</tbody></table></div></div>")
            Return sb.ToString()
        End Function

        Private Function LowStockPanel() As String
            Dim sb As New StringBuilder()
            ' Whole catalogue, not "mine": see ProductRepository.LowStock. Scoping this
            ' to the signed-in user is what made the panel read "All good" forever.
            Dim lowStock As List(Of Product) = _products.LowStock()
            sb.Append("<div class=""panel""><div class=""panel-hd""><h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--tertiary)"">" & WebUi.Ic("warning", "sm") & "</span> Low Stock Alerts" &
                      "<span class=""htag"">" & lowStock.Count.ToString() & " SKU" & If(lowStock.Count = 1, "", "S") & "</span></h3></div><div class=""panel-bd"">")
            If lowStock.Count = 0 Then
                sb.Append(WebUi.EmptyRow("All good — nothing low."))
            Else
                For Each p As Product In lowStock.Take(8)
                    sb.Append("<div class=""row space-between"" style=""padding:5px 0;border-bottom:1px solid #f3e9e6""><span>" &
                              WebUi.Esc(p.Name) & " <span class=""sub"" style=""font-size:11px"">· alert at " &
                              p.LowStockThreshold.ToString() & "</span></span>" &
                              "<span style=""color:var(--primary);font-weight:700;white-space:nowrap"">" & p.StockQuantity.ToString() & " left</span></div>")
                Next
                If lowStock.Count > 8 Then
                    sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-top:8px"">+ " &
                              (lowStock.Count - 8).ToString() & " more at or below threshold</div>")
                End If
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
