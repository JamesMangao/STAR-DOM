Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    ''' <summary>
    ''' The studio's earnings ledger: what each order has earned, and whether that
    ''' money has actually been earned yet.
    ''' </summary>
    ''' <remarks>
    ''' This page exists because the order list answers a different question. Orders
    ''' tell you where a parcel is; the tally of what the studio has actually earned
    ''' is not visible anywhere in that view, and a refund that clawed back money
    ''' already released would otherwise leave no trace at all.
    '''
    ''' Read-only by design. Every row here is a consequence of the order's own
    ''' delivery lifecycle -- held on delivery, released when the customer confirms,
    ''' voided by a refund -- so there is nothing for a human to press. A ledger that
    ''' could be edited by hand would stop being evidence of anything.
    ''' </remarks>
    Public Class MerchantEarningsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _ledger As New CommissionLedgerService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the earnings ledger: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Studio earnings", "MERCHANT STUDIO / LEDGER",
                                    "Your earning on each order. It is held from delivery until the customer confirms the " &
                                    "parcel arrived, released when they do, and voided if the order is refunded. " &
                                    "Nothing here moves money — it records what has been earned."))

            Dim totals As CommissionLedgerTotals = _ledger.Totals()

            ' A refunded order whose earning had already been released is the case this
            ' KPI strip has to be able to show: the released total has money removed
            ' from it without the order count changing, and the reversal count is what
            ' explains where it went.
            sb.Append("<div class=""frow"" style=""gap:12px"">")
            sb.Append(Kpi("RELEASED", Fmt.PHP(totals.ReleasedAmount), totals.ReleasedCount.ToString() & " order(s)"))
            sb.Append(Kpi("HELD", Fmt.PHP(totals.HeldAmount), totals.HeldCount.ToString() & " awaiting confirmation"))
            sb.Append(Kpi("VOID", Fmt.PHP(totals.VoidAmount), totals.VoidCount.ToString() & " refunded order(s)"))
            sb.Append(Kpi("CLAWED BACK", totals.ReversedCount.ToString(), "released then reversed"))
            sb.Append("</div>")

            Dim rows As List(Of OrderCommission) = _ledger.ListRecent(300)
            If rows.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No earnings yet. An order starts earning the studio money when it is marked DELIVERED."))
            Else
                sb.Append("<div class=""tblwrap""><table class=""tbl""><colgroup>" &
                          "<col style=""width:150px""><col style=""width:150px""><col style=""width:110px"">" &
                          "<col style=""width:110px""><col style=""width:190px""><col></colgroup><thead><tr>")
                For Each h As String In {"ORDER", "CUSTOMER", "AMOUNT", "STATE", "TIMELINE", "NOTE"}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each c As OrderCommission In rows
                    sb.Append("<tr>")
                    sb.Append("<td><a href=""" & WebUi.Attr("/App/OrderDetail.aspx?id=" & c.OrderId.ToString()) & """>" &
                              "<span class=""ord-no"">" & WebUi.Esc(c.OrderNumber) & "</span></a></td>")
                    sb.Append("<td class=""cust"">" & WebUi.Esc(c.CustomerName) & "</td>")
                    sb.Append("<td class=""amt"">" & WebUi.Money(c.Amount) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(c.StatusLabel) & "</td>")
                    sb.Append("<td class=""sub"" style=""font-size:11.5px"">" & TimelineCell(c) & "</td>")
                    sb.Append("<td class=""sub"" style=""font-size:11.5px"">" & WebUi.Esc(c.Reason) & "</td>")
                    sb.Append("</tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If
            Out.Text = sb.ToString()
        End Sub

        ''' <summary>
        ''' One KPI tile, reusing the dashboard's own k-label / k-value / k-sub markup
        ''' so the strip lines up with every other tile on the site.
        ''' </summary>
        ''' <remarks>
        ''' The third argument is subText, not sub: `Sub` is a VB keyword and cannot be
        ''' used as a parameter name. Reports.aspx.vb named the same thing subText, so
        ''' the two pages match.
        ''' </remarks>
        Private Shared Function Kpi(label As String, value As String, subText As String) As String
            Return "<div class=""kpi"" style=""min-width:170px""><div class=""k-label"">" & WebUi.Esc(label) & "</div>" &
                   "<div class=""k-value"">" & WebUi.Esc(value) & "</div>" &
                   "<div class=""k-sub"">" & WebUi.Esc(subText) & "</div></div>"
        End Function

        ''' <summary>The dates that matter for one row: held, released, voided.</summary>
        Private Shared Function TimelineCell(c As OrderCommission) As String
            Dim sb As New StringBuilder()
            sb.Append("Held " & WebUi.Esc(Fmt.DateF(c.HeldAt)))
            If c.ReleasedAt.HasValue Then
                sb.Append(" &middot; released " & WebUi.Esc(Fmt.DateF(c.ReleasedAt.Value)))
            End If
            If c.VoidedAt.HasValue Then
                sb.Append(" &middot; voided " & WebUi.Esc(Fmt.DateF(c.VoidedAt.Value)))
            End If
            Return sb.ToString()
        End Function

    End Class

End Namespace
