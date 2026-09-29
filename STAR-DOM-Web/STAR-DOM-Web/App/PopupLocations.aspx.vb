Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class PopupLocationsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _events As New EventService()
        Private ReadOnly _catalog As New CatalogService()
        Private ReadOnly _notif As New NotificationService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                If Request.QueryString("remind") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("remind"), id)
                    Dim ev As PopUpEvent = _events.GetEvent(id)
                    If ev IsNot Nothing Then
                        _notif.Notify(STAR_DOM.Helpers.Session.CurrentUser.Id, "Event reminder — " & ev.Name,
                                      "We'll remind you before " & ev.Name & " opens (" & ev.WindowText & ").",
                                      "EVENT", "popup-locations")
                        Session("flash_msg") = "Reminder set for " & ev.Name & ". Check your notifications."
                        Session("flash_ok") = True
                    End If
                    Response.Redirect("/App/PopupLocations.aspx", True)
                End If

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

            ' ---- Top system header & breadcrumb ribbon ----
            Dim activeCount As Integer = If(current IsNot Nothing, 1, 0)
            Dim upcomingCount As Integer = upcoming.Count
            sb.Append("<div class=""page-head"">")
            sb.Append("<div><div class=""ph-title"" style=""gap:8px""><span style=""font-size:11px;letter-spacing:.12em;text-transform:uppercase;color:var(--ink-soft)"">STAR:DOM HUB / </span><span style=""font-size:11px;letter-spacing:.12em;text-transform:uppercase;color:var(--primary);font-weight:800"">Store Locations &amp; Physical Events</span><span class=""sys-rev"">TOUR 2026</span></div>")
            sb.Append("<h1 style=""font-size:24px;font-weight:800;letter-spacing:-.5px;margin-top:4px"">Shop Online or Visit Our Physical Merchant Booths Across the Philippines</h1></div>")
            sb.Append("<div class=""term-pill"" style=""background:var(--surface-low);border:1px solid var(--line)""><span class=""ph-ic"" style=""width:32px;height:32px;font-size:17px;background:var(--surface-mid);color:var(--primary)"">" & WebUi.Ic("storefront", "sm") & "</span><div style=""text-align:right""><span style=""display:block;font-size:9px;letter-spacing:.1em;color:var(--ink-soft)"">ACTIVE STALL NETWORK</span><b style=""font-size:13px;color:var(--primary);font-family:Consolas,monospace"">" & activeCount.ToString() & " ACTIVE / " & upcomingCount.ToString() & " UPCOMING</b></div></div>")
            sb.Append("</div>")

            ' ---- Tier-1 highlight: current active location ----
            If current IsNot Nothing Then
                sb.Append(CurrentHero(current))
                ' stamp card callout
                sb.Append(StampCard())
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

            ' ---- Map + guidelines row ----
            sb.Append("<div class=""grid-12"">")
            sb.Append(MapBlock(current))
            sb.Append(GuidelinesBlock())
            sb.Append("</div>")

            Out.Text = sb.ToString()
        End Sub

        ''' <summary>Split hero stage for the current active location (4.html).</summary>
        Private Function CurrentHero(ev As PopUpEvent) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""panel hero-decor""><div class=""panel-bd"">")
            sb.Append("<div class=""grid-12"" style=""grid-template-columns:1fr"">")
            sb.Append("<div style=""display:flex;gap:14px;flex-wrap:wrap"">")
            ' photo
            sb.Append("<div class=""photo-frame"" style=""width:100%;max-width:270px;height:180px;flex-shrink:0"">")
            sb.Append(WebUi.ProductImg(ev.ImageFile, ev.Id * 5 + 2, ev.Name, "height:180px"))
            sb.Append("<div class=""ph-overlay"" style=""justify-content:space-between;flex-direction:row;align-items:flex-end""><div class=""ph-title"" style=""font-size:12px"">" & WebUi.Esc(ev.BoothNumber) & " Atrium Booth</div><span style=""font-size:9px;opacity:.9"">Updated " & Clock.RelativeTime(ev.UpdatedAt) & "</span></div></div>")
            ' info
            sb.Append("<div style=""flex:1;min-width:260px"">")
            sb.Append("<div style=""display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:6px"">")
            sb.Append("<span class=""eyebrow"" style=""margin:0;color:var(--tertiary)"">CURRENT PHYSICAL LOCATION</span>")
            sb.Append("<span class=""status-chip red""><span class=""dot""></span> NOW OPEN TODAY</span>")
            sb.Append("<span class=""sys-rev"">BOOTH " & WebUi.Esc(ev.BoothNumber) & "</span></div>")
            sb.Append("<h2 style=""font-size:24px;font-weight:800;letter-spacing:-.5px;margin:4px 0"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</h2>")
            sb.Append("<p class=""sub"" style=""margin:2px 0 12px"">" & WebUi.Esc(ev.VenueDetail) & " · " & WebUi.Esc(ev.LocationAddress) & "</p>")
            ' operating snapshot matrix
            sb.Append("<div class=""grid-2"" style=""max-width:520px"">")
            sb.Append(MiniStat("TOUR RESIDENCY", ev.WindowText))
            sb.Append(MiniStat("OPERATING HOURS", ev.HoursText & " (Daily)"))
            sb.Append("</div>")
            ' offerings tag cloud
            sb.Append("<div style=""margin-top:12px""><span style=""font-size:10px;letter-spacing:.1em;text-transform:uppercase;color:var(--ink-soft);font-weight:800"">Booth Inventory &amp; Live Offerings</span>")
            sb.Append("<div class=""tagcloud"" style=""margin-top:7px"">")
            sb.Append("<span class=""tag"">" & WebUi.Ic("check_circle", "sm") & " Exclusive Physical Art Prints</span>")
            sb.Append("<span class=""tag"">" & WebUi.Ic("cards", "sm") & " Sticker Gacha Dispensers</span>")
            sb.Append("<span class=""tag"">" & WebUi.Ic("draw", "sm") & " Live Sketch Requests</span>")
            sb.Append("<span class=""tag"">" & WebUi.Ic("qr_code_2", "sm") & " GCash / Maya QR Ready</span>")
            sb.Append("<span class=""tag yellow"">" & WebUi.Ic("shopping_bag", "sm") & " Limited Convention Bags</span>")
            sb.Append("</div></div>")
            ' CTAs
            sb.Append("<div class=""btn-row"" style=""margin-top:16px"">")
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx?id=" & ev.Id.ToString(), "View Venue Floor Map & Directions", "primary", "map"))
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx?id=" & ev.Id.ToString(), "Browse Event Exclusive Inventory", "secondary", "package_2"))
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx?remind=" & ev.Id.ToString(), "Get SMS / App Reminder", "ghost", "notifications_active"))
            sb.Append("</div></div></div>")
            ' mini stats (right column)
            sb.Append("<div class=""stat-grid"" style=""margin-top:14px"">")
            sb.Append("<div class=""mini-stat""><span>Queue Status</span><b class=""orange"">~4 MINS</b></div>")
            sb.Append("<div class=""mini-stat""><span>Live Stock</span><b class=""red"">94% AVAIL</b></div>")
            sb.Append("<div class=""mini-stat""><span>Floor Staff</span><b>3 ARTISANS</b></div>")
            sb.Append("<div class=""mini-stat""><span>PH Clock</span><b style=""color:var(--primary)"">" & Clock.Now.ToString("h:mm tt") & " (UTC+8)</b></div>")
            sb.Append("</div>")
            sb.Append("</div></div></div>")
            Return sb.ToString()
        End Function

        Private Function MiniStat(label As String, value As String) As String
            Return "<div class=""mini-stat"" style=""text-align:left;background:var(--surface-low)""><span>" & WebUi.Esc(label) & "</span><b style=""font-size:13px;font-weight:700;color:var(--ink)"">" & WebUi.Esc(value) & "</b></div>"
        End Function

        ''' <summary>Merchant stamp card callout (collect 4 stamps).</summary>
        Private Function StampCard() As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""panel""><div class=""panel-bd"" style=""padding:14px 16px"">")
            sb.Append("<div style=""display:flex;align-items:center;gap:14px;flex-wrap:wrap"">")
            sb.Append("<span class=""ph-ic"" style=""width:44px;height:44px;font-size:24px;background:var(--yellow);color:var(--on-yellow)"">" & WebUi.Ic("diamond") & "</span>")
            sb.Append("<div style=""flex:1;min-width:240px""><div style=""display:flex;gap:8px;align-items:center;flex-wrap:wrap""><span class=""eyebrow"" style=""color:var(--primary);margin:0"">Pop-up Merchant Stamp Card</span><span class=""sys-rev"">PHILIPPINES TOUR 2026</span></div>")
            sb.Append("<h3 style=""font-size:15px;font-weight:700;margin:4px 0 2px"">Collect 4 Foil Stamps &amp; Claim a Free Custom Headshot Sketch!</h3>")
            sb.Append("<p class=""sub"" style=""margin:0;max-width:560px"">Spend ₱500 at any official physical STAR:DOM booth to receive an exclusive metallic foil badge stamp. Present your card at our live artist table or commission kiosk.</p></div>")
            ' stamps
            sb.Append("<div class=""stamp-row"" style=""background:var(--surface-low);padding:10px 14px;border-radius:9px"">")
            sb.Append("<div style=""display:flex;gap:8px""><span class=""stamp collected"">" & WebUi.Ic("star", "filled") & "</span><span class=""stamp collected"">" & WebUi.Ic("star", "filled") & "</span><span class=""stamp empty"">" & WebUi.Ic("star") & "</span><span class=""stamp empty"">" & WebUi.Ic("star") & "</span></div>")
            sb.Append("<div><span class=""stamp-count"">2 / 4 COLLECTED</span><br><span class=""stamp-cta"">2 STAMPS TO UNLOCK</span></div>")
            sb.Append("</div></div></div></div>")
            Return sb.ToString()
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
            If ev.LineupText <> "" Then
                sb.Append("<div class=""kv-row""><span>Lineup:</span><b style=""color:var(--primary)"">" & WebUi.Esc(ev.LineupText) & "</b></div>")
            End If
            sb.Append("</div>")
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx?id=" & ev.Id.ToString(), "View Event Details", "ghost", "arrow_forward"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        ''' <summary>Background for a venue photo: the mall photo when one is on file, else the brand gradient.</summary>
        Private Function VenueImage(ev As PopUpEvent) As String
            Dim f As String = Convert.ToString(ev.ImageFile).Trim()
            If f.Length > 0 Then
                Return "url('" & WebUi.Attr(f) & "')"
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

        Private Function MapBlock(current As PopUpEvent) As String
            Dim sb As New StringBuilder()
            Dim name As String = If(current IsNot Nothing, current.Name, "Robinson's Galleria South")
            Dim venue As String = If(current IsNot Nothing, current.VenueDetail, "Ground Floor Activity Center")
            Dim address As String = If(current IsNot Nothing, current.LocationAddress, "KM 31 National Highway, San Pedro, Laguna")
            Dim currentId As Integer = If(current IsNot Nothing, current.Id, 1)

            Dim upcoming As List(Of PopUpEvent) = _events.ListUpcoming()
            Dim allVenues As New List(Of PopUpEvent)()
            If current IsNot Nothing Then allVenues.Add(current)
            For Each u In upcoming
                If current Is Nothing OrElse u.Id <> current.Id Then allVenues.Add(u)
            Next

            sb.Append("<div class=""map-frame""><div class=""panel-hd""><div><h3 style=""display:flex;align-items:center;gap:8px""><span class=""ph-ic"" style=""width:28px;height:28px;font-size:14px;background:var(--yellow);color:var(--on-yellow)"">" & WebUi.Ic("explore", "sm") & "</span> Physical Tour Hub Map</h3><p class=""sub"" style=""margin:0;font-size:12px"">Interactive geographic locations &amp; venue navigation</p></div><span class=""status-chip yellow""><span class=""dot""></span> " & allVenues.Count.ToString() & " HUBS MAPPED</span></div>")

            ' Dynamic Stylized SVG Map canvas with interactive venue pins & visual routes
            sb.Append("<div class=""map-canvas"" style=""height:320px;position:relative;background:#f5eee9;overflow:hidden"">")
            ' Stylized SVG Grid & Transit Lines
            sb.Append("<svg style=""position:absolute;inset:0;width:100%;height:100%;pointer-events:none"" xmlns=""http://www.w3.org/2000/svg"">")
            sb.Append("<defs>")
            sb.Append("<pattern id=""grid"" width=""32"" height=""32"" patternUnits=""userSpaceOnUse""><path d=""M 32 0 L 0 0 0 32"" fill=""none"" stroke=""rgba(183,0,17,0.06)"" stroke-width=""1""/></pattern>")
            sb.Append("<linearGradient id=""tourRoute"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%""><stop offset=""0%"" stop-color=""var(--primary)"" stop-opacity=""0.7""/><stop offset=""100%"" stop-color=""var(--yellow)"" stop-opacity=""0.9""/></linearGradient>")
            sb.Append("</defs>")
            sb.Append("<rect width=""100%"" height=""100%"" fill=""url(#grid)"" />")
            sb.Append("<path d=""M 40 180 Q 180 80 320 140 T 620 220"" fill=""none"" stroke=""url(#tourRoute)"" stroke-width=""3"" stroke-dasharray=""6,6"" />")
            sb.Append("<circle cx=""120"" cy=""140"" r=""70"" fill=""rgba(254,208,27,0.08)"" />")
            sb.Append("<circle cx=""360"" cy=""160"" r=""90"" fill=""rgba(183,0,17,0.05)"" />")
            sb.Append("</svg>")

            ' Live Floating Active Card
            sb.Append("<div class=""map-pin"" style=""position:absolute;top:16px;left:16px;z-index:5;background:rgba(255,255,255,0.95);border:1.5px solid var(--line);border-radius:12px;padding:12px 14px;max-width:320px;box-shadow:0 6px 20px rgba(30,27,25,0.12)"">")
            sb.Append("<div class=""pin-live"" style=""display:flex;align-items:center;gap:7px;font-size:12px;font-weight:800;color:var(--ink)""><span class=""pulse""></span>Live Node: STAR:DOM @ " & WebUi.Esc(name) & "</div>")
            sb.Append("<p style=""margin:4px 0 8px;font-size:11.5px;color:var(--ink-soft);line-height:1.4"">" & WebUi.Esc(venue) & "<br/><b style=""color:var(--ink);font-weight:600"">" & WebUi.Esc(address) & "</b></p>")
            sb.Append("<div style=""display:flex;gap:6px;align-items:center;margin-top:6px"">")
            sb.Append("<a class=""btn primary sm"" href=""https://www.google.com/maps/search/?api=1&query=" & Server.UrlEncode("STAR:DOM " & venue & " " & address) & """ target=""_blank"" style=""padding:4px 10px;font-size:11px"">" & WebUi.Ic("directions", "sm") & " Google Maps</a>")
            sb.Append("<a class=""btn ghost sm"" href=""/App/PopupLocations.aspx?id=" & currentId.ToString() & """ style=""padding:4px 10px;font-size:11px"">" & WebUi.Ic("info", "sm") & " Booth Details</a>")
            sb.Append("</div></div>")

            ' Interactive Map Venue Chips at the bottom of the canvas
            sb.Append("<div style=""position:absolute;bottom:12px;left:12px;right:12px;z-index:5;display:flex;gap:8px;overflow-x:auto;padding-bottom:4px;scrollbar-width:none"">")
            For Each ev In allVenues
                Dim isLive As Boolean = (current IsNot Nothing AndAlso ev.Id = current.Id)
                Dim chipStyle As String = If(isLive, "background:var(--primary);color:#fff;border:1px solid var(--primary);box-shadow:var(--sh-1)", "background:rgba(255,255,255,0.92);color:var(--ink);border:1px solid var(--line)")
                Dim dotColor As String = If(isLive, "background:#fed01b", "background:var(--primary)")
                sb.Append("<a href=""/App/PopupLocations.aspx?id=" & ev.Id.ToString() & """ style=""display:inline-flex;align-items:center;gap:6px;padding:6px 12px;border-radius:999px;font-size:11px;font-weight:700;white-space:nowrap;text-decoration:none;transition:transform .12s;" & chipStyle & """>")
                sb.Append("<span style=""width:7px;height:7px;border-radius:50%;" & dotColor & """></span>")
                sb.Append(WebUi.Esc(ev.Name) & " (" & WebUi.Esc(ev.CityLabel) & ")")
                sb.Append("</a>")
            Next
            sb.Append("</div>")

            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function GuidelinesBlock() As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""panel hero-decor""><div class=""panel-bd"" style=""display:flex;flex-direction:column;gap:12px"">")
            sb.Append("<h3 style=""font-size:15px;font-weight:700""><span class=""ph-ic"" style=""width:28px;height:28px;font-size:14px;background:var(--surface-mid);color:var(--primary)"">" & WebUi.Ic("info", "sm") & "</span> Pop-up Guidelines</h3>")
            sb.Append("<p class=""sub"" style=""margin:0;font-size:12.5px"">Everything you need to know before visiting our on-site creator pavilions.</p>")
            sb.Append("<div class=""guideline""><span class=""gi"">" & WebUi.Ic("smartphone") & "</span><div><b>Cashless Preferred</b><span>All stalls accept GCash, Maya, and major Philippine bank QR Ph codes.</span></div></div>")
            sb.Append("<div class=""guideline""><span class=""gi"">" & WebUi.Ic("draw") & "</span><div><b>Live Commissions</b><span>Queue tickets for on-the-spot ink sketches open at 11:00 AM daily.</span></div></div>")
            sb.Append("<div class=""guideline""><span class=""gi"">" & WebUi.Ic("package_2") & "</span><div><b>Online Pickups</b><span>Pre-ordered online prints can be collected instantly at the booth with your order ID.</span></div></div>")
            sb.Append(WebUi.BtnHref("/App/CommissionHub.aspx", "Request a Commission", "ghost", "draw"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Sub RenderEventDetail(id As Integer)
            Dim ev As PopUpEvent = _events.GetEvent(id)
            If ev Is Nothing Then
                Out.Text = WebUi.AlertBox("Event not found.")
                Return
            End If
            Dim sb As New StringBuilder()
            sb.Append("<a href=""/App/PopupLocations.aspx"" class=""sub"" style=""display:inline-flex;align-items:center;gap:6px"">" & WebUi.Ic("arrow_back", "sm") & " All locations</a>")
            sb.Append(WebUi.Section("STAR:DOM @ " & ev.Name, "EVENT DETAIL · " & ev.WindowText, ev.Description))
            If ev.ImageFile.Trim() <> "" Then
                sb.Append("<div class=""photo-frame"" style=""height:240px;margin-bottom:16px"">")
                sb.Append(WebUi.ProductImg(ev.ImageFile, ev.Id * 5 + 2, ev.Name, "height:240px"))
                sb.Append("</div>")
            End If
            sb.Append("<div class=""card""><div class=""kv"">")
            sb.Append("<dt>Venue</dt><dd>" & WebUi.Esc(ev.VenueDetail) & "</dd>")
            sb.Append("<dt>Address</dt><dd>" & WebUi.Esc(ev.LocationAddress) & "</dd>")
            sb.Append("<dt>Booth</dt><dd>" & WebUi.Esc(ev.BoothNumber) & "</dd>")
            sb.Append("<dt>Hours</dt><dd>" & WebUi.Esc(ev.HoursText) & "</dd>")
            If ev.FeaturedGuest <> "" Then sb.Append("<dt>Featured</dt><dd>" & WebUi.Esc(ev.FeaturedGuest) & "</dd>")
            sb.Append("<dt>Status</dt><dd>" & WebUi.Badge(ev.Status) & "</dd>")
            sb.Append("</div></div>")

            ' event-exclusive offers
            Dim exclusives As List(Of Product) = _events.EventExclusive(id)
            If exclusives.Count > 0 Then
                sb.Append("<div class=""sec-head"" style=""margin-top:24px""><div>")
                sb.Append("<div class=""eyebrow"">BOOTH INVENTORY &amp; LIVE OFFERINGS</div>")
                sb.Append("<h2>Event-exclusive Products</h2></div></div>")
                sb.Append("<div class=""grid cards4"">")
                For Each p As Product In exclusives
                    sb.Append("<div class=""pcard"">")
                    sb.Append(WebUi.ProductImg(p.PrimaryImageFile, p.Id, p.Name, "height:150px"))
                    sb.Append("<div class=""pbody""><b>" & WebUi.Esc(p.Name) & "</b>" & WebUi.Money(p.EffectivePrice) &
                              WebUi.BtnHref("/App/Cart.aspx?add=" & p.Id.ToString() & "&q=1&ret=" &
                                            Server.UrlEncode("/App/PopupLocations.aspx?id=" & id.ToString()), "Add to bag", "primary", "add_shopping_cart") &
                              "</div></div>")
                Next
                sb.Append("</div>")
            End If
            sb.Append("<div class=""frow"">")
            sb.Append(WebUi.BtnHref("/App/PopupLocations.aspx?remind=" & id.ToString(), "Get SMS / App Reminder", "secondary"))
            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
