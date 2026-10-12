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
            ' from Sales Reports, which is the one place online revenue is read.

            ' ---- Current location controller ----
            Dim current As PopUpEvent = _events.CurrentEvent()
            If current IsNot Nothing Then
                sb.Append(LocationController(current))
            End If

            ' ---- KPI row ----
            ' One sales channel only. The booth-takings tile and the Pop-up Historical
            ' Performance Register read EventSales, which no longer records anything:
            ' a pop-up is a marketing presence, not a till. Leaving them would have
            ' put a permanently-zero "Event Booth Takings" card beside a reports page
            ' that no longer knows booth money exists.
            '
            ' The other tiles went with them, which was the wrong call. Stripping the
            ' booth numbers also left one lonely revenue card in a grid built for
            ' five, and the counts a merchant actually steers by -- orders paid,
            ' commissions waiting on a reply, catalogue size, customers, rating --
            ' had nowhere to live. None of those read EventSales, so putting them
            ' back contradicts nothing.
            sb.Append("<div class=""grid kpis"">")
            sb.Append(DigitalMarketplaceKpi())
            sb.Append(PaidOrdersKpi())
            sb.Append(CommissionsWaitingKpi())
            sb.Append(SkuCountKpi())
            sb.Append(CustomerKpi())
            sb.Append(RatingKpi())
            sb.Append("</div>")

            ' ---- Workbench ----
            sb.Append(ScheduledEventsPanel(current))
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
            ' photo — use the same image pipeline as the customer-facing locations page.
            ' ProductImg checks ev.ImageFile and only falls back to gradient art when
            ' the event has no photo on file, so the admin sees the real booth photo
            ' the customer sees instead of a bare maroon gradient.
            sb.Append("<div class=""photo-frame"" style=""width:100%;max-width:230px;height:150px;flex-shrink:0"">")
            sb.Append(WebUi.ProductImg(ev.ImageFile, ev.Id * 7 + 3, ev.Name, "height:150px"))
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
            ' Paid online orders only. The old formula subtracted the event sales table
            ' (in-person booth takings) from PAID orders — two different populations —
            ' so the KPI read 0.00 or worse even when online orders were landing.
            Dim digital As Decimal = _reports.RevenueTotal()

            ' The foot line used to carry the commission and parcel counts, which was
            ' then the only place they appeared. They are proper tiles now, so repeating
            ' them here told the merchant the same thing twice. A recent trend instead:
            ' a lifetime total on its own cannot say whether the shop is moving.
            Dim recent As Decimal = 0D
            ' Named row, not day: Day is a VB built-in, and shadowing it here
            ' makes the loop read as a call to Day(date).
            For Each row In _reports.RevenueLastNDays(30)
                recent += row.total
            Next

            Return KpiTile("hub", "var(--tertiary-fixed)", "var(--tertiary)",
                           "Digital Marketplace Revenue", Fmt_Php(digital),
                           "<b style=""color:var(--ink)"">" & Fmt_Php(recent) &
                           "</b> in the last 30 days")
        End Function

        ''' <summary>
        ''' One KPI tile, built in one place.
        ''' </summary>
        ''' <remarks>
        ''' Six near-identical string literals is how a KPI row drifts: one tile gets
        ''' a heavier icon, another loses the icon entirely, and nothing looks wrong
        ''' enough to notice. Every tile on the dashboard goes through this.
        '''
        ''' footHtml is pre-built markup and is deliberately NOT escaped — callers
        ''' want to bold a figure inside it. Everything else IS escaped: the labels are
        ''' literal today, but escaping should be the default rather than the thing
        ''' someone remembers.
        ''' </remarks>
        Private Function KpiTile(icon As String, accentBg As String, accentFg As String,
                                 label As String, value As String,
                                 Optional footHtml As String = "") As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""kpi k-icon"">")
            sb.Append("<span class=""k-ic"" style=""border-radius:9px;background:" & accentBg &
                      ";color:" & accentFg & """>" & WebUi.Ic(icon) & "</span>")
            sb.Append("<div class=""k-label"">" & WebUi.Esc(label) & "</div>")
            sb.Append("<div class=""k-value"">" & WebUi.Esc(value) & "</div>")
            If footHtml <> "" Then sb.Append("<div class=""k-sub"">" & footHtml & "</div>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ''' <summary>Orders whose payment has actually landed.</summary>
        Private Function PaidOrdersKpi() As String
            Return KpiTile("payments", "#e9f7ee", "var(--green)",
                           "Paid Orders", _reports.PaidOrdersCount().ToString(),
                           "<b style=""color:var(--ink)"">" & _reports.OrdersCount().ToString() &
                           "</b> placed in total")
        End Function

        ''' <summary>
        ''' Commission requests in the two states only the studio can move: submitted,
        ''' or pending review. This is the tile that says "there is work waiting on you".
        ''' </summary>
        Private Function CommissionsWaitingKpi() As String
            Return KpiTile("account_tree", "var(--primary)", "var(--on-primary)",
                           "Awaiting Your Reply", _reports.PendingCommissions().ToString(),
                           "<b style=""color:var(--ink)"">" & _reports.TotalCommissions().ToString() &
                           "</b> commissions all-time")
        End Function

        ''' <summary>Active catalogue size, with the restock count attached.</summary>
        Private Function SkuCountKpi() As String
            ' --on-yellow is the palette's own text-on-yellow, so the amber icon keeps
            ' the contrast the other tiles have instead of yellow-on-yellow.
            Return KpiTile("inventory_2", "var(--yellow)", "var(--on-yellow)",
                           "Handcrafted SKUs", _reports.TotalProducts().ToString(),
                           "<b style=""color:var(--ink)"">" & _reports.LowStockCount().ToString() &
                           "</b> low or out of stock")
        End Function

        Private Function CustomerKpi() As String
            Return KpiTile("group", "var(--surface-mid)", "var(--tertiary)",
                           "Customers", _reports.TotalCustomers().ToString(),
                           "<b style=""color:var(--ink)"">" & _reports.ReviewCount().ToString() &
                           "</b> published reviews")
        End Function

        ''' <summary>
        ''' Average star rating. Reads an em dash rather than 0.00 when nothing is
        ''' rated yet: COALESCE turns an unrated catalogue into a hard zero, and a zero
        ''' looks like a failing score instead of an absence of scores.
        ''' </summary>
        Private Function RatingKpi() As String
            Dim count As Integer = _reports.ReviewCount()
            Dim value As String
            If count = 0 Then
                value = "—"
            Else
                value = _reports.AvgRating().ToString("0.00") & " / 5"
            End If
            Return KpiTile("star", "var(--yellow-soft)", "var(--on-yellow)",
                           "Average Rating", value,
                           "<b style=""color:var(--ink)"">" & count.ToString() & "</b> reviews")
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

        ''' <summary>
        ''' The last five paid online orders, newest first. Booth takings used to be
        ''' merged into this list from EventSales; that table no longer records sales,
        ''' so orders alone are the list.
        ''' </summary>
        Private Function RecentOrdersPanel() As String
            Dim sb As New StringBuilder()
            Dim rows As New List(Of (sortDate As Date, html As String))()
            For Each o As Order In _reports.RecentOrders(5)
                rows.Add((o.CreatedAt,
                          "<span><a href=""/App/Merchant/Orders.aspx"" style=""font-weight:700"">" & WebUi.Esc(o.OrderNumber) & "</a> · " & WebUi.Esc(o.CustomerName) & "</span>" &
                          "<span>" & WebUi.Money(o.TotalAmount) & " " & WebUi.Badge(o.Status) & "</span>"))
            Next
            rows.Sort(Function(x, y) y.sortDate.CompareTo(x.sortDate))

            sb.Append("<div class=""panel""><div class=""panel-hd""><h3><span class=""ph-ic"" style=""width:28px;height:28px;font-size:15px;background:var(--surface-mid);color:var(--primary)"">" & WebUi.Ic("package_2", "sm") & "</span> Recent Sales</h3></div><div class=""panel-bd"">")
            If rows.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No sales yet — online orders appear here."))
            Else
                For Each r As (sortDate As Date, html As String) In rows.Take(5)
                    sb.Append("<div class=""row space-between"" style=""padding:5px 0;border-bottom:1px solid #f3e9e6"">" & r.html & "</div>")
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
