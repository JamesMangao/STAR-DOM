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
            ev.OpenTime = Convert.ToString(Request.Form("open"))
            ev.CloseTime = Convert.ToString(Request.Form("close"))
            ev.FeaturedGuest = If(_editingId > 0, ev.FeaturedGuest, "")
            ' "guest" is no longer posted: the field was removed from the editor.
            ' Keep whatever is already stored so an edit does not blank the column,
            ' which is NOT NULL, and seed "" on create.
            ' "lineup" removed from the form: single-owner brand, no creator lineups.
            ev.LineupText = ""
            ev.Description = Convert.ToString(Request.Form("description"))

            Dim locId As Integer = 0
            Integer.TryParse(Request.Form("loc"), locId)
            ev.LocationId = locId

            Dim sd As Date
            If Date.TryParse(Convert.ToString(Request.Form("start")), sd) Then ev.StartDate = sd
            Dim ed As Date
            If Date.TryParse(Convert.ToString(Request.Form("end")), ed) Then ev.EndDate = ed
            If Request.Form("status") IsNot Nothing Then ev.Status = Convert.ToString(Request.Form("status"))

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
            sb.Append("<div class=""form-grid2"">")
            sb.Append(Field("open", "Open time *", If(ev IsNot Nothing, ev.OpenTime, "10:00 AM")))
            sb.Append(Field("close", "Close time *", If(ev IsNot Nothing, ev.CloseTime, "9:00 PM")))
            sb.Append("</div>")
            sb.Append("<div class=""field""><label>Status</label><select name=""status"">")
            For Each s As String In {"UPCOMING", "NOW OPEN", "ENDED", "CANCELLED"}
                Dim sel As String = If(ev IsNot Nothing AndAlso String.Equals(ev.Status, s, StringComparison.OrdinalIgnoreCase), " selected", "")
                sb.Append("<option" & sel & ">" & s & "</option>")
            Next
            sb.Append("</select></div>")
            sb.Append("<div class=""field""><label>Description</label><textarea name=""description"" style=""min-height:70px"">" &
                      WebUi.Esc(If(ev IsNot Nothing, ev.Description, "")) & "</textarea></div>")
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
