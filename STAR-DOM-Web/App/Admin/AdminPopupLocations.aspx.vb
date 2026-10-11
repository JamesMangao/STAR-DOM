Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services
Imports STAR_DOM.Database

Namespace STAR_DOM.Web

    Public Class AdminPopupLocationsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _events As New EventService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireAdmin()
            Try
                Dim detailId As Integer = 0
                Integer.TryParse(Request.QueryString("id"), detailId)
                If detailId > 0 Then
                    RenderEventDetail(detailId)
                Else
                    RenderOverview()
                End If
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load locations: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderOverview()
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Session("flash_msg") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, "ok"))

            Dim current As PopUpEvent = _events.CurrentEvent()
            Dim upcoming As List(Of PopUpEvent) = _events.ListUpcoming()

            Dim activeCount As Integer = If(current IsNot Nothing, 1, 0)
            Dim upcomingCount As Integer = upcoming.Count

            ' ---- Top system header ----
            sb.Append("<div class=""page-head"">")
            sb.Append("<div><div class=""ph-title"" style=""gap:8px""><span style=""font-size:11px;letter-spacing:.12em;text-transform:uppercase;color:var(--ink-soft)"">STAR:DOM HUB / </span><span style=""font-size:11px;letter-spacing:.12em;text-transform:uppercase;color:var(--primary);font-weight:800"">Store Locations &amp; Physical Events</span><span class=""sys-rev"">TOUR 2026</span></div>")
            sb.Append("<h1 style=""font-size:24px;font-weight:800;letter-spacing:-.5px;margin-top:4px"">Shop Online or Visit Our Physical Merchant Booths Across the Philippines</h1></div>")
            sb.Append("<div class=""term-pill"" style=""background:var(--surface-low);border:1px solid var(--line)""><span class=""ph-ic"" style=""width:32px;height:32px;font-size:17px;background:var(--surface-mid);color:var(--primary)"">" & WebUi.Ic("storefront", "sm") & "</span><div style=""text-align:right""><span style=""display:block;font-size:9px;letter-spacing:.1em;color:var(--ink-soft)"">ACTIVE STALL NETWORK</span><b style=""font-size:13px;color:var(--primary);font-family:Consolas,monospace"">" & activeCount.ToString() & " ACTIVE / " & upcomingCount.ToString() & " UPCOMING</b></div></div>")
            sb.Append("</div>")

            ' ---- Tier-1 highlight: current active location ----
            If current IsNot Nothing Then
                sb.Append(CurrentHero(current))
            End If

            ' ---- Upcoming events ----
            sb.Append("<div class=""section-block""><h2><span class=""ph-ic"" style=""width:30px;height:30px;font-size:16px;background:var(--yellow);color:var(--on-yellow)"">" & WebUi.Ic("event", "sm") & "</span> Upcoming Merchant Events &amp; Mall Pop-ups</h2><span class=""sb-sub"">Displaying " & upcomingCount.ToString() & " Verified Booth Installations</span></div>")

            If upcoming.Count > 0 Then
                sb.Append("<div class=""event-grid"">")
                For Each ev As PopUpEvent In upcoming
                    sb.Append(EventCard(ev))
                Next
                sb.Append("</div>")
            Else
                sb.Append(WebUi.EmptyRow("No upcoming events scheduled yet."))
            End If

            Out.Text = sb.ToString()
        End Sub

        Private Function CurrentHero(ev As PopUpEvent) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""panel hero-decor""><div class=""panel-bd"">")
            sb.Append("<div class=""grid-12"" style=""grid-template-columns:1fr"">")
            sb.Append("<div style=""display:flex;gap:14px;flex-wrap:wrap"">")
            ' photo — same ProductImg pipeline as the customer locations page
            sb.Append("<div class=""photo-frame"" style=""width:100%;max-width:270px;height:180px;flex-shrink:0"">")
            sb.Append(WebUi.ProductImg(ev.ImageFile, ev.Id * 5 + 2, ev.Name, "height:180px"))
            sb.Append("<div class=""ph-overlay"" style=""justify-content:space-between;flex-direction:row;align-items:flex-end""><div class=""ph-title"" style=""font-size:12px"">" & WebUi.BoothLabel(ev.BoothNumber) & " &middot; Atrium</div><span style=""font-size:9px;opacity:.9"">Updated " & Clock.RelativeTime(ev.UpdatedAt) & "</span></div></div>")
            ' info
            sb.Append("<div style=""flex:1;min-width:260px"">")
            sb.Append("<div style=""display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:6px"">")
            sb.Append("<span class=""eyebrow"" style=""margin:0;color:var(--tertiary)"">CURRENT PHYSICAL LOCATION</span>")
            sb.Append("<span class=""status-chip red""><span class=""dot""></span> NOW OPEN TODAY</span>")
            sb.Append("<span class=""sys-rev"">" & WebUi.BoothLabel(ev.BoothNumber).ToUpperInvariant() & "</span></div>")
            sb.Append("<h2 style=""font-size:24px;font-weight:800;letter-spacing:-.5px;margin:4px 0"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</h2>")
            sb.Append("<p class=""sub"" style=""margin:2px 0 12px"">" & WebUi.Esc(ev.VenueDetail) & " · " & WebUi.Esc(ev.LocationAddress) & "</p>")
            sb.Append("<div class=""grid-2"" style=""max-width:520px"">")
            sb.Append(MiniStat("TOUR RESIDENCY", ev.WindowText))
            sb.Append(MiniStat("OPERATING HOURS", ev.HoursText & " (Daily)"))
            sb.Append("</div>")
            ' CTAs
            sb.Append("<div class=""btn-row"" style=""margin-top:16px"">")
            sb.Append(WebUi.BtnHref("/App/Admin/AdminPopupLocations.aspx?id=" & ev.Id.ToString(), "View Venue Floor Map & Directions", "primary", "map"))
            sb.Append("<a class=""btn ghost"" href=""/App/Merchant/EventEdit.aspx?id=" & ev.Id.ToString() & """><span class=""ms sm"">edit</span>Edit Event</a>")
            sb.Append("</div></div></div>")
            sb.Append("</div></div></div>")
            Return sb.ToString()
        End Function

        Private Function MiniStat(label As String, value As String) As String
            Return "<div class=""mini-stat"" style=""text-align:left;background:var(--surface-low)""><span>" & WebUi.Esc(label) & "</span><b style=""font-size:13px;font-weight:700;color:var(--ink)"">" & WebUi.Esc(value) & "</b></div>"
        End Function

        Private Function EventCard(ev As PopUpEvent) As String
            Dim sb As New StringBuilder()
            Dim days As Integer = (ev.StartDate.Date - Clock.Now.Date).Days
            Dim daysTxt As String = If(ev.Status = "NOW OPEN", "LIVE NOW", If(days >= 0, days.ToString() & " DAYS TO GO", "SOON"))
            sb.Append("<div class=""event-card""><div class=""ec-top""><span class=""ec-tag"">" & WebUi.Esc(ev.RegionLabel) & "</span><span class=""ec-days"">" & daysTxt & "</span></div>")
            sb.Append("<div class=""ec-img""><div class=""ph-img"" style=""background-image:" & VenueImage(ev) & """></div><span class=""ec-booth"">" & WebUi.Esc(ev.BoothNumber) & "</span></div>")
            sb.Append("<div class=""ec-body""><div><div class=""ec-title"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</div><div class=""ec-addr"">" & WebUi.Esc(ev.VenueDetail) & ", " & WebUi.Esc(ev.CityLabel) & "</div></div>")
            sb.Append("<div class=""ec-meta"">")
            sb.Append("<div class=""kv-row""><span>Dates:</span><b>" & WebUi.Esc(ev.WindowText) & "</b></div>")
            sb.Append("<div class=""kv-row""><span>Hours:</span><b>" & WebUi.Esc(ev.HoursText) & "</b></div>")
            sb.Append("</div>")
            sb.Append(WebUi.BtnHref("/App/Admin/AdminPopupLocations.aspx?id=" & ev.Id.ToString(), "View Event Details", "ghost", "arrow_forward"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function VenueImage(ev As PopUpEvent) As String
            Dim f As String = Convert.ToString(ev.ImageFile).Trim()
            If f.Length > 0 Then
                Return "url('" & WebUi.Attr(WebUi.AssetUrl(f)) & "')"
            End If
            Return CategoryGradient(ev.Id)
        End Function

        Private Function CategoryGradient(seed As Integer) As String
            Dim gradients As String() = {
                "linear-gradient(135deg,#b70011,#1e1b19)",
                "linear-gradient(135deg,#bc5200,#1e1b19)",
                "linear-gradient(135deg,#6d28d9,#1e1b19)",
                "linear-gradient(135deg,#0e7490,#1e1b19)",
                "linear-gradient(135deg,#15803d,#1e1b19)"
            }
            Return gradients(seed Mod gradients.Length)
        End Function

        Private Sub RenderEventDetail(id As Integer)
            Dim ev As PopUpEvent = _events.GetEvent(id)
            If ev Is Nothing Then
                Out.Text = WebUi.AlertBox("Event not found.")
                Return
            End If
            Dim sb As New StringBuilder()
            sb.Append("<a href=""/App/Admin/AdminPopupLocations.aspx"" class=""sub"" style=""display:inline-flex;align-items:center;gap:6px"">" & WebUi.Ic("arrow_back", "sm") & " All locations</a>")
            sb.Append(WebUi.Section("STAR:DOM @ " & ev.Name, "EVENT DETAIL · " & ev.WindowText, ev.Description))

            ' ---- Photo with admin replace/upload ----
            sb.Append("<div class=""card"" style=""max-width:640px;margin-bottom:16px"">")
            sb.Append("<div style=""display:flex;gap:16px;align-items:flex-start;flex-wrap:wrap"">")
            sb.Append("<div class=""photo-frame"" style=""width:240px;height:180px;flex-shrink:0"">")
            sb.Append(WebUi.ProductImg(ev.ImageFile, ev.Id * 5 + 2, ev.Name, "height:180px"))
            sb.Append("<div class=""ph-overlay""><span class=""ph-eyebrow"">Booth Photo</span><span class=""ph-title"">" & WebUi.BoothLabel(ev.BoothNumber) & "</span></div></div>")
            sb.Append("<div style=""flex:1;min-width:200px"">")
            sb.Append("<h3 style=""font-size:14px;font-weight:700;margin:0 0 6px"">Event Photo</h3>")
            If ev.ImageFile.Trim() <> "" Then
                sb.Append("<p class=""sub"" style=""margin:0 0 8px;font-size:12px"">Current photo is stored. Upload a new one below to replace it.</p>")
            Else
                sb.Append("<p class=""sub"" style=""margin:0 0 8px;font-size:12px"">No photo uploaded yet. Add one so customers see the actual booth.</p>")
            End If
            sb.Append("<form method=""post"" action=""/App/Admin/AdminPopupLocations.aspx?id=" & ev.Id.ToString() & """ style=""display:flex;gap:8px;align-items:center;flex-wrap:wrap"">")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<input type=""file"" name=""eventPhoto"" accept=""image/*"" style=""flex:1;min-width:160px"">")
            sb.Append("<button class=""btn primary sm"" type=""submit"" name=""uploadPhoto"" value=""1""><span class=""ms sm"">upload</span>Upload New Photo</button>")
            sb.Append("</form>")
            sb.Append("</div></div></div>")

            ' ---- Detail card ----
            sb.Append("<div class=""card""><div class=""kv"">")
            sb.Append("<dt>Venue</dt><dd>" & WebUi.Esc(ev.VenueDetail) & "</dd>")
            sb.Append("<dt>Address</dt><dd>" & WebUi.Esc(ev.LocationAddress) & "</dd>")
            sb.Append("<dt>Booth</dt><dd>" & WebUi.BoothLabel(ev.BoothNumber) & "</dd>")
            sb.Append("<dt>Hours</dt><dd>" & WebUi.Esc(ev.HoursText) & "</dd>")
            sb.Append("<dt>Dates</dt><dd>" & WebUi.Esc(ev.WindowText) & "</dd>")
            sb.Append("<dt>Status</dt><dd>" & WebUi.Badge(ev.Status) & "</dd>")
            sb.Append("<dt>Region</dt><dd>" & WebUi.Esc(ev.RegionLabel) & "</dd>")
            sb.Append("<dt>City</dt><dd>" & WebUi.Esc(ev.CityLabel) & "</dd>")
            If ev.FeaturedGuest <> "" Then sb.Append("<dt>Featured Guest</dt><dd>" & WebUi.Esc(ev.FeaturedGuest) & "</dd>")
            sb.Append("<dt>Created</dt><dd>" & ev.CreatedAt.ToString("MMM d, yyyy") & "</dd>")
            sb.Append("<dt>Updated</dt><dd>" & ev.UpdatedAt.ToString("MMM d, yyyy h:mm tt") & "</dd>")
            sb.Append("</div></div>")

            ' ---- Actions row ----
            sb.Append("<div class=""btn-row"" style=""margin-top:16px;display:flex;gap:10px;flex-wrap:wrap"">")
            sb.Append(WebUi.BtnHref("/App/Merchant/EventEdit.aspx?id=" & ev.Id.ToString(), "Edit Event Details", "primary", "edit"))
            If ev.Status <> "CANCELLED" Then
                sb.Append("<a class=""btn danger"" href=""/App/Merchant/Events.aspx?setstatus=" & ev.Id.ToString() & "&to=CANCELLED"" data-confirm=""Cancel this event?"" data-confirm-danger""><span class=""ms sm"">cancel</span>Cancel Event</a>")
            End If
            sb.Append("<a class=""btn ghost"" href=""/App/Merchant/Events.aspx?del=" & ev.Id.ToString() & """ data-confirm=""Delete this event permanently?"" data-confirm-danger""><span class=""ms sm"">delete</span>Delete</a>")
            sb.Append("</div>")

            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
