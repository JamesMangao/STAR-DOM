Imports System.Drawing
Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class ReportsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _reports As New ReportService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load reports: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Event Sales Reports", "MERCHANT STUDIO / ANALYTICS",
                                    "Daily, monthly, product, category, event and payment-method analytics."))

            ' KPIs
            Dim mix = _reports.OmnichannelMix()
            sb.Append("<div class=""grid kpis"">")
            sb.Append(Kpi("TOTAL REVENUE", "₱" & _reports.RevenueTotal().ToString("N2"), "all channels"))
            sb.Append(Kpi("ORDERS", _reports.OrdersCount().ToString(), _reports.OrdersCount("PENDING").ToString() & " pending"))
            sb.Append(Kpi("COMMISSION REVENUE", "₱" & _reports.CommissionRevenue().ToString("N2"), _reports.TotalCommissions().ToString() & " commissions"))
            sb.Append(Kpi("EVENT GROSS", "₱" & mix.eventTotal.ToString("N2"), "in-person + QR + preorders"))
            sb.Append(Kpi("WEB GROSS", "₱" & mix.onlineTotal.ToString("N2"), "online marketplace"))
            sb.Append("</div>")

            sb.Append("<div class=""grid"" style=""grid-template-columns:repeat(auto-fit,minmax(320px,1fr))"">")

            ' daily sales last 14 days
            Dim daily As List(Of ChartSeries) = _reports.DailySales(14)
            sb.Append("<div class=""card""><h3 style=""margin-bottom:4px"">Daily sales — last 14 days</h3>")
            sb.Append(BarList(daily, 140))
            sb.Append("</div>")

            ' monthly
            Dim monthly As List(Of ChartSeries) = _reports.MonthlySales(6)
            sb.Append("<div class=""card""><h3 style=""margin-bottom:4px"">Monthly sales</h3>")
            sb.Append(BarList(monthly, 140))
            sb.Append("</div>")

            ' product sales
            Dim top As List(Of ChartSeries) = _reports.ProductSales(6)
            sb.Append("<div class=""card""><h3 style=""margin-bottom:4px"">Top products</h3>")
            sb.Append(BarList(top, 120, True))
            sb.Append("</div>")

            ' category sales
            Dim cats As List(Of ChartSeries) = _reports.CategorySales()
            sb.Append("<div class=""card""><h3 style=""margin-bottom:4px"">Sales by category</h3>")
            sb.Append(BarList(cats, 120, True))
            sb.Append("</div>")

            ' event revenue
            Dim evrev As List(Of ChartSeries) = _reports.EventRevenue()
            sb.Append("<div class=""card""><h3 style=""margin-bottom:4px"">Revenue by event</h3>")
            sb.Append(BarList(evrev, 120, True))
            sb.Append("</div>")

            ' payment method breakdown
            Dim pays As List(Of ChartSeries) = _reports.PaymentMethodBreakdown()
            sb.Append("<div class=""card""><h3 style=""margin-bottom:4px"">Payment method mix</h3>")
            sb.Append(BarList(pays, 120, True))
            sb.Append("</div>")

            ' hourly traffic
            Dim hourly As List(Of Decimal) = _reports.HourlyTraffic()
            sb.Append("<div class=""card""><h3 style=""margin-bottom:4px"">Hourly peak flow (busiest)</h3>")
            sb.Append(RenderHourly(hourly))
            sb.Append("</div>")
            sb.Append("</div>")

            ' review health
            sb.Append("<div class=""card"" style=""margin-top:18px;max-width:560px""><div class=""kv"">")
            sb.Append("<dt>Average rating</dt><dd>" & WebUi.Stars(CInt(Math.Round(_reports.AvgRating()))) & " " & _reports.AvgRating().ToString("0.0") & "</dd>")
            sb.Append("<dt>Reviews</dt><dd>" & _reports.ReviewCount().ToString() & "</dd>")
            sb.Append("<dt>Low stock SKUs</dt><dd>" & _reports.LowStockCount().ToString() & "</dd>")
            sb.Append("<dt>Customers</dt><dd>" & _reports.TotalCustomers().ToString() & "</dd>")
            sb.Append("</div></div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function Kpi(label As String, value As String, subText As String) As String
            Dim icon As String = ""
            Select Case label
                Case "TOTAL REVENUE" : icon = "payments"
                Case "ORDERS" : icon = "receipt_long"
                Case "COMMISSION REVENUE" : icon = "account_balance"
                Case "EVENT GROSS" : icon = "storefront"
                Case "WEB GROSS" : icon = "language"
            End Select
            Return "<div class=""kpi k-icon""><span class=""k-ic"">" & WebUi.Ic(icon) & "</span><div class=""k-label"">" & WebUi.Esc(label) & "</div>" &
                   "<div class=""k-value"">" & WebUi.Esc(value) & "</div><div class=""k-sub"">" & WebUi.Esc(subText) & "</div></div>"
        End Function

        Private Function BarList(series As List(Of ChartSeries), maxPct As Integer, Optional labelsRight As Boolean = False) As String
            Dim sb As New StringBuilder()
            If series Is Nothing OrElse series.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No data yet."))
                Return sb.ToString()
            End If
            Dim maxVal As Decimal = 0D
            For Each s As ChartSeries In series
                If s.Value > maxVal Then maxVal = s.Value
            Next
            If maxVal <= 0D Then maxVal = 1D
            For Each s As ChartSeries In series
                Dim pct As Integer = CInt(s.Value / maxVal * maxPct)
                If pct < 1 AndAlso s.Value > 0 Then pct = 1
                Dim hexColor As String = "#b70011"
                If Not s.Color.IsEmpty Then
                    hexColor = "#" & s.Color.R.ToString("X2") & s.Color.G.ToString("X2") & s.Color.B.ToString("X2")
                End If
                sb.Append("<div style=""margin:7px 0"">")
                sb.Append("<div class=""row space-between"" style=""gap:8px""><span style=""font-size:12.5px;flex:1;min-width:90px"">" &
                          WebUi.Esc(s.Label) & "</span><b style=""font-size:12.5px"">₱" & s.Value.ToString("N0") & "</b></div>")
                sb.Append("<div class=""meter""><i style=""width:" & pct.ToString() & "%;background:" & hexColor & """></i></div>")
                sb.Append("</div>")
            Next
            Return sb.ToString()
        End Function

        Private Function RenderHourly(series As List(Of Decimal)) As String
            Dim sb As New StringBuilder()
            If series Is Nothing OrElse series.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No traffic data."))
                Return sb.ToString()
            End If
            Dim maxVal As Decimal = 0D
            For Each v As Decimal In series
                If v > maxVal Then maxVal = v
            Next
            If maxVal <= 0D Then maxVal = 1D
            Dim hour As Integer = 0
            For Each v As Decimal In series
                Dim pct As Integer = CInt(v / maxVal * 100)
                If pct < 1 AndAlso v > 0 Then pct = 1
                sb.Append("<div style=""margin:5px 0""><div class=""row space-between""><span style=""font-size:12px"">" &
                          hour.ToString("00") & ":00</span><span class=""sub"" style=""font-size:12px"">" & v.ToString("N0") & "</span></div>" &
                          "<div class=""meter""><i style=""width:" & pct.ToString() & "%;background:#a16207""></i></div></div>")
                hour += 1
            Next
            Return sb.ToString()
        End Function

    End Class

End Namespace
