Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class PipelinePage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _svc As New CommissionService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the pipeline: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim sb As New StringBuilder()
            Dim filter As String = Convert.ToString(Request.QueryString("st"))
            ' A commission notification deep-links here with ?hl=<id> so the merchant
            ' lands on the exact request already scrolled to and flashing.
            Dim hl As Integer = 0
            Integer.TryParse(Request.QueryString("hl"), hl)
            Dim all As List(Of Commission) = _svc.ListMerchantCommissions("", "").OrderByDescending(Function(c) c.UpdatedAt).ToList()
            Dim shown As List(Of Commission) = all
            If filter <> "" Then
                shown = all.Where(Function(c) String.Equals(c.Status, filter, StringComparison.OrdinalIgnoreCase)).ToList()
            End If

            Dim pending As Integer = all.Where(Function(c) c.Status = "PENDING REVIEW").Count()
            sb.Append(WebUi.Section("Commission Pipeline", "MERCHANT STUDIO / REQUESTS",
                                    "Review requests, quote, confirm payment, and drive production through delivery to receipt."))

            sb.Append("<div class=""frow"">")
            For Each chip As String In {"", "PENDING REVIEW", "PAYMENT PENDING", "PAID", "IN PRODUCTION", "REVISION", "FINALIZE REQUESTED", "FINALIZED", "DELIVERED", "RECEIVED", "DECLINED"}
                Dim label As String = If(chip = "", "ALL", chip.Replace("_", " "))
                Dim url As String = If(chip = "", "/App/Merchant/Pipeline.aspx", "/App/Merchant/Pipeline.aspx?st=" & Server.UrlEncode(chip))
                sb.Append(WebUi.OutLink(url, label, String.Equals(filter, chip, StringComparison.OrdinalIgnoreCase)))
            Next
            sb.Append("</div>")
            sb.Append("<p class=""sub"">" & shown.Count.ToString() & " request(s) · " & pending.ToString() & " need attention</p>")

            If shown.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No commissions in this view."))
                Out.Text = sb.ToString()
                Return
            End If

            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"REQUEST", "CUSTOMER", "CATEGORY", "QTY", "BUDGET", "STATUS", ""}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            For Each c As Commission In shown
                Dim budget As String = "—"
                If c.BudgetMin.HasValue AndAlso c.BudgetMax.HasValue Then
                    budget = "₱" & c.BudgetMin.Value.ToString("N0") & " – ₱" & c.BudgetMax.Value.ToString("N0")
                ElseIf c.BudgetMax.HasValue Then
                    budget = "up to ₱" & c.BudgetMax.Value.ToString("N0")
                End If
                sb.Append("<tr" & If(hl > 0 AndAlso c.Id = hl,
                                     " id=""cm-" & c.Id.ToString() & """ class=""cm-flash""", "") &
                          "><td><b>" & WebUi.Esc(c.CommissionNumber) & "</b><br><span class=""sub"" style=""font-size:12px"">" &
                          WebUi.Esc(c.Title) & "</span></td>")
                sb.Append("<td>" & WebUi.Esc(c.CustomerName) & "</td>")
                sb.Append("<td>" & WebUi.Esc(c.CategoryName) & "</td>")
                sb.Append("<td>" & c.Quantity.ToString() & "</td>")
                sb.Append("<td>" & budget & "</td>")
                sb.Append("<td>" & WebUi.Badge(c.Status) & "</td>")
                sb.Append("<td class=""rowact""><a href=""/App/CommissionDetail.aspx?id=" & c.Id.ToString() & """><span class=""ms sm"">open_in_new</span> Open</a></td></tr>")
            Next
            sb.Append("</tbody></table></div>")
            If hl > 0 Then sb.Append(HighlightScript(hl))
            Out.Text = sb.ToString()
        End Sub

        ''' <summary>
        ''' Scrolls the deep-linked row (?hl=) into view and lets it flash for a few
        ''' seconds so the merchant spots which request the notification was about.
        ''' No-op when that id is not on the page.
        ''' </summary>
        Private Function HighlightScript(id As Integer) As String
            Return "<script>(function(){var el=document.getElementById('cm-" & id.ToString() &
                   "');if(!el)return;setTimeout(function(){el.scrollIntoView({behavior:'smooth',block:'center'});},60);" &
                   "setTimeout(function(){el.classList.remove('cm-flash');},3200);})();</" & "script>"
        End Function

    End Class

End Namespace
