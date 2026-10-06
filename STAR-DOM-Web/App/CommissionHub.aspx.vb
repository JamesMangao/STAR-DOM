Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Single entry point for every commission flow: customers discover artists and open a
    ''' slot here, merchants are bridged to their pipeline. Other pages funnel here rather
    ''' than linking straight to the request form.
    ''' </summary>
    Public Class CommissionHubPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _catalog As New CatalogService()
        Private ReadOnly _commissions As New CommissionService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                RenderHub()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the commission hub: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderHub()
            Dim sb As New StringBuilder()
            Dim slots As List(Of CommissionSlotView) = _catalog.CommissionSlots()

            sb.Append(HubHeader(slots.Count))
            sb.Append(WebUi.Section("Open Commission Slots", "BESPOKE ART, DELIVERED TO YOU",
                                    "Commission bespoke digital art, original characters, or portraits directly from the STAR:DOM artist. " &
                                    "Commissioned products can only be claimed via delivery."))

            If slots.Count > 0 Then
                sb.Append("<div class=""grid cards"">")
                For Each s As CommissionSlotView In slots
                    sb.Append(WebUi.CommissionSlotCard(s))
                Next
                sb.Append("</div>")
            Else
                sb.Append(WebUi.EmptyRow("No commission slots are open right now — you can still submit a request and we'll quote it."))
            End If

            sb.Append(MerchantBridge())

            ' my requests
            Dim mine As List(Of Commission) = _commissions.ListMyCommissions().OrderByDescending(Function(c) c.Id).ToList()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px""><div>")
            sb.Append("<div class=""eyebrow"">MY REQUEST QUEUE</div>")
            sb.Append("<h2>My Commission Requests</h2></div>")
            sb.Append("<div class=""sub"">" & mine.Count.ToString() & " request(s) in the atelier pipeline</div></div>")

            If mine.Count = 0 Then
                sb.Append(WebUi.EmptyRow("You haven't submitted a commission request yet."))
            Else
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"REQUEST", "CATEGORY", "QTY", "STATUS", "UPDATED", ""}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each c As Commission In mine
                    sb.Append("<tr>")
                    sb.Append("<td><b>" & WebUi.Esc(c.CommissionNumber) & "</b><br><span class=""sub"" style=""font-size:12px"">" &
                              WebUi.Esc(c.Title) & "</span></td>")
                    sb.Append("<td>" & WebUi.Esc(c.CategoryName) & "</td>")
                    sb.Append("<td>" & c.Quantity.ToString() & "</td>")
                    sb.Append("<td>" & WebUi.Badge(c.Status) & "</td>")
                    sb.Append("<td>" & WebUi.Esc(c.UpdatedAt.ToString("MMM d")) & "</td>")
                    sb.Append("<td class=""rowact""><a href=""/App/CommissionDetail.aspx?id=" & c.Id.ToString() & """>View</a></td>")
                    sb.Append("</tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If

            Out.Text = sb.ToString()
        End Sub

        ''' <summary>Page-level "Request a Commission" CTA so the action is always reachable, even with zero open slots.</summary>
        Private Function HubHeader(openSlots As Integer) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec"">")
            sb.Append("<div class=""eyebrow"">STAR:DOM ATELIER</div>")
            sb.Append("<h1 style=""font-size:26px;font-weight:800;letter-spacing:-.5px"">Commission Hub</h1>")
            sb.Append("<p class=""sub"" style=""max-width:640px"">One place to browse open artist slots, submit a bespoke request, and track every commission you have in flight.</p>")
            sb.Append("<div class=""frow"">")
            sb.Append(WebUi.BtnHref("/App/CommissionRequest.aspx", "Request a Commission", "primary", "draw"))
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx", "View Pop-up Locations", "ghost", "storefront"))
            sb.Append("</div>")
            sb.Append("<p class=""sub"" style=""margin-top:12px"">" & WebUi.Ic("account_tree", "sm") & " " &
                      openSlots.ToString() & " artist(s) currently accepting commission slots</p>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ''' <summary>Bridges the customer-facing hub to the merchant pipeline, which used to be unreachable from here.</summary>
        Private Function MerchantBridge() As String
            ' Fully qualified: Page.Session (HttpSessionState) shadows the helper class.
            If Not STAR_DOM.Helpers.Session.CanManageStore Then Return ""
            Dim inbound As List(Of Commission) = _commissions.ListMerchantCommissions("", "")
            Dim pending As Integer = inbound.Where(Function(c) c.Status = "PENDING REVIEW").Count()
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px""><div>")
            sb.Append("<div class=""eyebrow"">MERCHANT STUDIO</div>")
            sb.Append("<h2>Incoming Requests</h2></div>")
            sb.Append("<div class=""sub"">" & inbound.Count.ToString() & " total · " & pending.ToString() & " need attention</div></div>")
            sb.Append("<div class=""frow"">")
            sb.Append(WebUi.BtnHref("/App/Merchant/Pipeline.aspx", "Open Commission Pipeline", "primary", "account_tree"))
            sb.Append("</div>")
            Return sb.ToString()
        End Function

    End Class

End Namespace
