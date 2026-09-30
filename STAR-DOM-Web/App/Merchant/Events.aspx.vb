Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class MerchantEventsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _events As New EventService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                If Request.QueryString("setstatus") <> "" AndAlso Request.QueryString("to") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("setstatus"), id)
                    Dim toStatus As String = CStr(Request.QueryString("to")).ToUpperInvariant()
                    ' Manual overrides are intentionally limited to cancellation: NOW OPEN / UPCOMING /
                    ' ENDED are derived live from the Asia/Manila clock and event date+houring window.
                    If toStatus = "CANCELLED" AndAlso id > 0 Then _events.SetStatus(id, "CANCELLED")
                    Session("flash_msg") = "Event cancelled."
                    Session("flash_ok") = True
                    Response.Redirect("/App/Merchant/Events.aspx", True)
                End If
                If Request.QueryString("del") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("del"), id)
                    If id > 0 Then
                        Dim r As ServiceResult = _events.DeleteEvent(id)
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
                    End If
                    Response.Redirect("/App/Merchant/Events.aspx", True)
                End If
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load events: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append(WebUi.Section("Event & Booth Manager", "MERCHANT STUDIO / POP-UP TOUR",
                                    "Schedule mall pop-ups across the 2026 Luzon tour. Statuses are derived live from the Asia/Manila clock; only cancellation is manual."))

            Dim events As List(Of PopUpEvent) = _events.ListEvents().OrderBy(Function(e) e.StartDate).ToList()
            sb.Append("<div class=""sec-head""><div><h3>Pop-up events (" & events.Count.ToString() & ")</h3></div>" &
                      WebUi.BtnHref("/App/Merchant/EventEdit.aspx", "+ Schedule New Pop-up Event", "primary", "add_location_alt") & "</div>")

            If events.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No events scheduled."))
            Else
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"EVENT", "VENUE", "WINDOW", "HOURS", "STATUS", "ACTIONS"}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each ev As PopUpEvent In events
                    sb.Append("<tr>")
                    sb.Append("<td><b>STAR:DOM @ " & WebUi.Esc(ev.Name) & "</b>" & If(ev.IsCurrent, " " & WebUi.Pill("ACTIVE NODE", "red"), "") &
                              "<br><span class=""sub"" style=""font-size:11px"">Booth " & WebUi.Esc(ev.BoothNumber) & "</span></td>")
                    sb.Append("<td>" & WebUi.Esc(ev.VenueDetail) & "<br><span class=""sub"" style=""font-size:11px"">" &
                              WebUi.Esc(ev.CityLabel) & "</span></td>")
                    sb.Append("<td>" & WebUi.Esc(ev.WindowText) & "</td>")
                    sb.Append("<td>" & WebUi.Esc(ev.HoursText) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(ev.Status) & "</td>")
                    sb.Append("<td class=""rowact"">")
                    sb.Append("<a href=""/App/Merchant/EventEdit.aspx?id=" & ev.Id.ToString() & """>Edit</a>")
                    If ev.Status <> "CANCELLED" Then
                        sb.Append("<a href=""/App/Merchant/Events.aspx?setstatus=" & ev.Id.ToString() & "&to=CANCELLED"" data-confirm=""Cancel this event?"" data-confirm-danger"">Cancel</a>")
                    End If
                    sb.Append("<a href=""/App/Merchant/Events.aspx?del=" & ev.Id.ToString() & """ data-confirm=""Delete this event permanently?"" data-confirm-danger"">Delete</a>")
                    sb.Append("</td></tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If

            ' locations
            Dim locations As List(Of StoreLocation) = _events.ListLocations()
            sb.Append("<div class=""sec-head"" style=""margin-top:26px""><div><h3>Store locations (" & locations.Count.ToString() & ")</h3></div></div>")
            If locations.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No store locations on file."))
            Else
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"LOCATION", "VENUE", "CITY / REGION", "STATUS"}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each l As StoreLocation In locations
                    sb.Append("<tr><td><b>" & WebUi.Esc(l.Name) & "</b></td>")
                    sb.Append("<td>" & WebUi.Esc(l.Venue) & "</td>")
                    sb.Append("<td>" & WebUi.Esc(l.City) & ", " & WebUi.Esc(l.Region) & "</td>")
                    sb.Append("<td>" & If(l.IsActive, WebUi.Badge("ACTIVE"), WebUi.Badge("INACTIVE")) & "</td></tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If
            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
