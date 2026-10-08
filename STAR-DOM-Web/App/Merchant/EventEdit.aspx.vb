Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class EventEditPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _events As New EventService()
        Private ReadOnly _products As New ProductRepository()
        Private _editingId As Integer = 0

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                Integer.TryParse(Request.QueryString("id"), _editingId)

                If Guard.IsPost() Then
                    SaveEvent()
                    Return
                End If
                RenderForm("")
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the event editor: " & ex.Message)
            End Try
        End Sub

        Private Sub SaveEvent()
            Dim ev As PopUpEvent
            If _editingId > 0 Then
                ev = _events.GetEvent(_editingId)
                If ev Is Nothing Then
                    RenderForm("Event not found.")
                    Return
                End If
            Else
                ev = New PopUpEvent()
                ' These three are NOT NULL with no default in the insert path, and a
                ' fresh PopUpEvent leaves them Nothing. Db.P() turns Nothing into
                ' DBNull, so creating an event died on
                ' "null value in column imagefile". Seed them to "" here.
                ev.FeaturedGuest = ""
                ev.LineupText = ""
                ev.ImageFile = ""
            End If

            ev.Name = Trim(Convert.ToString(Request.Form("name")))
            ev.VenueDetail = Convert.ToString(Request.Form("venue"))
            ev.BoothNumber = Convert.ToString(Request.Form("booth"))
            ev.FeaturedGuest = If(_editingId > 0, ev.FeaturedGuest, "")
            ' "lineup" removed from the form: single-owner brand, no creator lineups.
            ' Preserve stored value so edits don't blank it.
            ev.LineupText = If(_editingId > 0, ev.LineupText, "")
            ev.Description = Convert.ToString(Request.Form("description"))

            Dim locId As Integer = 0
            Integer.TryParse(Request.Form("loc"), locId)
            ev.LocationId = locId

            Dim sd As Date
            If Date.TryParse(Convert.ToString(Request.Form("start")), sd) Then ev.StartDate = sd
            Dim ed As Date
            If Date.TryParse(Convert.ToString(Request.Form("end")), ed) Then ev.EndDate = ed
            If Request.Form("status") IsNot Nothing Then ev.Status = Convert.ToString(Request.Form("status"))

            ' OpenTime/CloseTime have no inputs of their own: the datetime-local start
            ' and end already carry the hours, so the time half of each becomes the
            ' opening and closing time. Both have to be set — a midnight value means
            ' the editor was left date-only, and then the stored text (or, for a new
            ' event, the column default) is what goes to the two NOT NULL columns.
            Dim openFromForm As Boolean = sd <> Date.MinValue AndAlso ed <> Date.MinValue AndAlso
                                           sd.TimeOfDay <> TimeSpan.Zero AndAlso ed.TimeOfDay <> TimeSpan.Zero
            If openFromForm Then
                Dim ci As System.Globalization.CultureInfo = System.Globalization.CultureInfo.GetCultureInfo("en-US")
                ev.OpenTime = sd.ToString("h:mm tt", ci)
                ev.CloseTime = ed.ToString("h:mm tt", ci)
            ElseIf _editingId > 0 Then
                Dim existing As PopUpEvent = _events.GetEvent(_editingId)
                If existing IsNot Nothing Then
                    ev.OpenTime = existing.OpenTime
                    ev.CloseTime = existing.CloseTime
                End If
            End If
            If String.IsNullOrWhiteSpace(ev.OpenTime) Then ev.OpenTime = "10:00 AM"
            If String.IsNullOrWhiteSpace(ev.CloseTime) Then ev.CloseTime = "9:00 PM"

            ' Handle event photo upload — store directly in database via AssetImages table.
            Dim photoFile As System.Web.HttpPostedFile = Request.Files("eventPhoto")
            If photoFile IsNot Nothing AndAlso photoFile.ContentLength > 0 Then
                Dim ext As String = System.IO.Path.GetExtension(photoFile.FileName).ToLowerInvariant()
                If ext <> ".jpg" AndAlso ext <> ".jpeg" AndAlso ext <> ".png" AndAlso ext <> ".webp" Then
                    RenderForm("Only JPG, PNG, and WebP images are allowed for event photos.")
                    Return
                End If
                If photoFile.ContentLength > 10485760 Then ' 10MB limit
                    RenderForm("Event photo must be under 10MB.")
                    Return
                End If

                ' Determine the path key for AssetImages.
                Dim photoPath As String = "/Assets/Malls/event-" & (If(_editingId > 0, _editingId.ToString(), Guid.NewGuid().ToString("N"))) & ext
                Dim mimeType As String = "image/" & ext.TrimStart("."c)
                If ext = ".jpeg" Then mimeType = "image/jpeg"

                ' Read file bytes into database.
                Dim fileBytes(photoFile.ContentLength - 1) As Byte
                photoFile.InputStream.Read(fileBytes, 0, photoFile.ContentLength)

                ' Upsert into AssetImages: delete old row for this event if editing.
                If _editingId > 0 Then
                    STAR_DOM.Database.Db.Exec("DELETE FROM AssetImages WHERE Path LIKE '/Assets/Malls/event-" & _editingId.ToString() & "%'")
                End If

                ' Insert new row.
                STAR_DOM.Database.Db.Exec(
                    "INSERT INTO AssetImages (Path, Data, Mime, ByteSize) VALUES (@path, @data, @mime, @size)",
                    STAR_DOM.Database.Db.P("@path", photoPath),
                    STAR_DOM.Database.Db.P("@data", fileBytes),
                    STAR_DOM.Database.Db.P("@mime", mimeType),
                    STAR_DOM.Database.Db.P("@size", photoFile.ContentLength))

                ' Point the event at the asset path.
                ev.ImageFile = photoPath
            End If

            Dim r As ServiceResult = _events.SaveEvent(ev)
            If r.Success Then
                Session("flash_msg") = "Event saved."
                Session("flash_ok") = True
                Response.Redirect("/App/Merchant/Events.aspx", True)
            Else
                RenderForm(r.Message)
            End If
        End Sub

        Private Sub RenderForm(errorMsg As String)
            Dim ev As PopUpEvent = Nothing
            If _editingId > 0 Then ev = _events.GetEvent(_editingId)

            Dim sb As New StringBuilder()
            If errorMsg <> "" Then sb.Append(WebUi.AlertBox(errorMsg))
            sb.Append("<a href=""/App/Merchant/Events.aspx"" class=""sub"">← Events</a>")
            sb.Append(WebUi.Section(If(ev Is Nothing, "Schedule New Pop-up Event", "Edit Event — " & ev.Name),
                                    "EVENT & BOOTH MANAGER", "Set venue, dates, hours and booth details."))

            sb.Append("<div class=""row"" style=""align-items:flex-start;gap:24px"">")
            sb.Append("<form method=""post"" action=""/App/Merchant/EventEdit.aspx" & If(_editingId > 0, "?id=" & _editingId.ToString(), "") & """ style=""flex:1;min-width:320px"">")
            ' Nested inside the shell form, which the browser closes at this tag — so the
            ' shell's token is not submitted with this form. Carry its own.
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<div class=""card mb"">")
            sb.Append("<div class=""field""><label>Event name *</label><input name=""name"" required value=""" & WebUi.Attr(If(ev IsNot Nothing, ev.Name, "")) & """ placeholder=""e.g. SM City Santa Rosa""></div>")
            sb.Append("<div class=""field""><label>Mall / venue location</label><select name=""loc"">")
            For Each l As Models.StoreLocation In _events.ListLocations()
                Dim sel As String = If(ev IsNot Nothing AndAlso ev.LocationId = l.Id, " selected", "")
                sb.Append("<option value=""" & l.Id.ToString() & """" & sel & ">" & WebUi.Esc(l.Name) & " — " & WebUi.Esc(l.City) & "</option>")
            Next
            sb.Append("</select></div>")
            sb.Append("<div class=""form-grid2"">")
            sb.Append(Field("venue", "Venue detail / booth area", If(ev IsNot Nothing, ev.VenueDetail, "")))
            sb.Append(Field("booth", "Booth / stall # *", If(ev IsNot Nothing, ev.BoothNumber, "")))
            sb.Append(Field("start", "Start date *", If(ev IsNot Nothing, ev.StartDate.ToString("yyyy-MM-ddTHH:mm"), ""), False, "datetime-local"))
            sb.Append(Field("end", "End date *", If(ev IsNot Nothing, ev.EndDate.ToString("yyyy-MM-ddTHH:mm"), ""), False, "datetime-local"))
            sb.Append("</div>")
            ' Open/close time fields removed: datetime-local inputs for start/end dates
            ' already include time selection, so separate time fields are redundant.
            sb.Append("<div class=""sub"" style=""font-size:11.5px;margin:-4px 0 8px"">The times on Start and End are saved as the event's opening and closing hours " &
                      "(e.g. 10:00 AM – 9:00 PM), which is what the calendar and banner display.</div>")
            sb.Append("<div class=""field""><label>Status</label><select name=""status"">")
            For Each s As String In {"UPCOMING", "NOW OPEN", "ENDED", "CANCELLED"}
                Dim sel As String = If(ev IsNot Nothing AndAlso String.Equals(ev.Status, s, StringComparison.OrdinalIgnoreCase), " selected", "")
                sb.Append("<option" & sel & ">" & s & "</option>")
            Next
            sb.Append("</select></div>")
            sb.Append("<div class=""field""><label>Description</label><textarea name=""description"" style=""min-height:70px"">" &
                      WebUi.Esc(If(ev IsNot Nothing, ev.Description, "")) & "</textarea></div>")
            sb.Append("<div class=""field""><label>Event photo (optional)</label>")
            sb.Append("<input type=""file"" name=""eventPhoto"" accept=""image/*"" style=""margin-top:4px""></div>")
            If ev IsNot Nothing AndAlso ev.ImageFile <> "" Then
                sb.Append("<div style=""margin-top:8px"">")
                sb.Append("<img src=""" & WebUi.Attr(ev.ImageFile) & """ alt=""Event photo"" style=""max-width:300px;border:1px solid var(--line);border-radius:8px;display:block"">")
                sb.Append("<p class=""sub"" style=""margin:4px 0 0"">Current photo. Upload a new one to replace it.</p></div>")
            End If
            sb.Append("<div class=""frow"">")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">save</span><span>Save Event</span></button>")
            sb.Append(WebUi.BtnHref("/App/Merchant/Events.aspx", "Cancel", "ghost", "close"))
            sb.Append("</div></div></form>")
            ' The right-hand "Event inventory" column and the "Featured guest / artist"
            ' field are gone: a single-owner brand has no guest lineup and no
            ' per-event stock to manage. EventInventory rows stay in the database
            ' untouched; only the editing UI was removed.
            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function Field(name As String, label As String, value As String, Optional required As Boolean = False,
                               Optional type As String = "text") As String
            ' No backslash before the closing quote. The old "& "\""" emitted
            ' value="Stall A-12\", so every field submitted with a trailing "\":
            ' booth saved as "4\", and a datetime-local value of
            ' "2026-09-04T10:00\" is invalid, so the browser sent an EMPTY date and
            ' DateRange() rejected the save. That is why edits appeared not to save.
            Return "<div class=""field""><label for=""" & name & """>" & WebUi.Esc(label) & "</label>" &
                   "<input id=""" & name & """ name=""" & name & """ type=""" & type & """ value=""" & WebUi.Attr(value) & """" &
                   If(required, " required", "") & "></div>"
        End Function

    End Class

End Namespace
