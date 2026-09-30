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

                ' inventory + sale actions on an existing event
                If Request.QueryString("addinv") <> "" Then
                    Dim pid As Integer = 0
                    Integer.TryParse(Request.QueryString("addinv"), pid)
                    Dim stock As Integer = 5
                    Integer.TryParse(Request.QueryString("stock"), stock)
                    If pid > 0 AndAlso _editingId > 0 Then
                        Dim r As ServiceResult = _events.AddInventory(_editingId, pid, stock, Request.QueryString("excl") = "1")
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
                    End If
                    Response.Redirect("/App/Merchant/EventEdit.aspx?id=" & _editingId.ToString(), True)
                End If
                If Request.QueryString("delinv") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("delinv"), id)
                    If id > 0 Then _events.RemoveInventory(id)
                    Response.Redirect("/App/Merchant/EventEdit.aspx?id=" & _editingId.ToString(), True)
                End If

                If Guard.IsPost() Then
                    If Request.Form("posSale") IsNot Nothing Then
                        RecordSale()
                        Return
                    End If
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
            End If

            ev.Name = Trim(CStr(Request.Form("name")))
            ev.VenueDetail = Convert.ToString(Request.Form("venue"))
            ev.BoothNumber = Convert.ToString(Request.Form("booth"))
            ev.OpenTime = Convert.ToString(Request.Form("open"))
            ev.CloseTime = Convert.ToString(Request.Form("close"))
            ev.FeaturedGuest = Convert.ToString(Request.Form("guest"))
            ev.LineupText = Convert.ToString(Request.Form("lineup"))
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

        Private Sub RecordSale()
            Dim evId As Integer = 0
            Integer.TryParse(Request.Form("evid"), evId)
            Dim pid As Integer = 0
            Integer.TryParse(Request.Form("posProduct"), pid)
            Dim qty As Integer = 1
            Integer.TryParse(Request.Form("posQty"), qty)
            Dim pm As String = Convert.ToString(Request.Form("posPm"))
            Dim st As String = Convert.ToString(Request.Form("posType"))
            If evId > 0 Then
                Dim r As ServiceResult = _events.RecordInPersonSale(evId, pid, qty, pm, st, "Logged from web POS")
                Session("flash_msg") = r.Message
                Session("flash_ok") = r.Success
            End If
            Response.Redirect("/App/Merchant/EventEdit.aspx?id=" & evId.ToString(), True)
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
            sb.Append("<form method=""post"" action=""/App/Merchant/EventEdit.aspx" & If(_editingId > 0, "?id=" & _editingId.ToString(), "") & """ style=""flex:1.4;min-width:340px"">")
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
            sb.Append(Field("guest", "Featured guest / artist", If(ev IsNot Nothing, ev.FeaturedGuest, "")))
            sb.Append(Field("lineup", "Lineup / creators (short)", If(ev IsNot Nothing, ev.LineupText, "")))
            sb.Append("<div class=""field""><label>Description</label><textarea name=""description"" style=""min-height:70px"">" &
                      WebUi.Esc(If(ev IsNot Nothing, ev.Description, "")) & "</textarea></div>")
            sb.Append("<div class=""frow"">")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">save</span><span>Save Event</span></button>")
            sb.Append(WebUi.BtnHref("/App/Merchant/Events.aspx", "Cancel", "ghost", "close"))
            sb.Append("</div></div></form>")

            ' right column: inventory + POS (only when editing)
            sb.Append("<div style=""flex:1.2;min-width:320px"">")
            If _editingId > 0 Then
                ' add inventory
                sb.Append("<div class=""card mb"">")
                sb.Append("<h3 style=""margin-bottom:6px"">Event inventory</h3>")
                sb.Append("<form method=""get"" action=""/App/Merchant/EventEdit.aspx"" style=""display:flex;gap:6px;flex-wrap:wrap;align-items:flex-end"">")
                sb.Append("<input type=""hidden"" name=""id"" value=""" & _editingId.ToString() & """>")
                sb.Append("<div class=""field"" style=""flex:1;min-width:150px;margin:0""><label>Product</label><select name=""addinv"">")
                For Each p As Product In _products.ListActive()
                    sb.Append("<option value=""" & p.Id.ToString() & """>" & WebUi.Esc(p.Name) & "</option>")
                Next
                sb.Append("</select></div>")
                sb.Append("<div class=""field"" style=""margin:0""><label>Stock</label><input name=""stock"" type=""number"" value=""5"" style=""width:70px""></div>")
                sb.Append("<button class=""btn primary sm"" type=""submit""><span class=""ic ms"">add</span><span>Add</span></button>")
                sb.Append("</form>")

                Dim inv As List(Of EventInventory) = _events.Inventory(_editingId)
                If inv.Count > 0 Then
                    sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                    For Each h As String In {"PRODUCT", "START", "SOLD", "LEFT", ""}
                        sb.Append("<th>" & h & "</th>")
                    Next
                    sb.Append("</tr></thead><tbody>")
                    For Each it As EventInventory In inv
                        sb.Append("<tr><td>" & WebUi.Esc(it.ProductName) & If(it.IsEventExclusive, " " & WebUi.Pill("EXCLUSIVE", "red"), "") & "</td>")
                        sb.Append("<td>" & it.StartingStock.ToString() & "</td>")
                        sb.Append("<td>" & it.SoldQuantity.ToString() & "</td>")
                        sb.Append("<td><b>" & it.RemainingStock.ToString() & "</b></td>")
                        sb.Append("<td class=""rowact""><a href=""/App/Merchant/EventEdit.aspx?id=" & _editingId.ToString() & "&delinv=" &
                                  it.Id.ToString() & """ data-confirm=""Remove this item from the event inventory?"" data-confirm-danger"">Remove</a></td></tr>")
                    Next
                    sb.Append("</tbody></table></div>")
                End If
                sb.Append("</div>")

                ' POS sale logging
                sb.Append("<div class=""card"">")
                sb.Append("<h3 style=""margin-bottom:6px"">Log in-person / QR sale</h3>")
                sb.Append("<form method=""post"" action=""/App/Merchant/EventEdit.aspx?id=" & _editingId.ToString() & """>")
                sb.Append("<input type=""hidden"" name=""posSale"" value=""1"">")
                sb.Append("<input type=""hidden"" name=""evid"" value=""" & _editingId.ToString() & """>")
                sb.Append("<div class=""field""><label>Product</label><select name=""posProduct"">")
                For Each it As EventInventory In inv
                    sb.Append("<option value=""" & it.ProductId.ToString() & """>" & WebUi.Esc(it.ProductName) & "</option>")
                Next
                sb.Append("</select></div>")
                sb.Append("<div class=""field""><label>Qty</label><input name=""posQty"" type=""number"" min=""1"" value=""1"" style=""width:90px""></div>")
                sb.Append("<div class=""field""><label>Payment</label><select name=""posPm"">")
                For Each pm As String In {"GCASH", "MAYA", "CARD", "CASH"}
                    sb.Append("<option>" & pm & "</option>")
                Next
                sb.Append("</select></div>")
                sb.Append("<div class=""field""><label>Sale type</label><select name=""posType"">")
                For Each st As String In {"IN_PERSON", "QR", "PREORDER"}
                    sb.Append("<option>" & st & "</option>")
                Next
                sb.Append("</select></div>")
                sb.Append("<button class=""btn secondary"" type=""submit""><span class=""ic ms"">point_of_sale</span><span>Record Sale</span></button>")
                sb.Append("</form></div>")
            End If
            sb.Append("</div>")
            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function Field(name As String, label As String, value As String, Optional required As Boolean = False,
                               Optional type As String = "text") As String
            Return "<div class=""field""><label for=""" & name & """>" & WebUi.Esc(label) & "</label>" &
                   "<input id=""" & name & """ name=""" & name & """ type=""" & type & """ value=""" & WebUi.Attr(value) & "\""" &
                   If(required, " required", "") & "></div>"
        End Function

    End Class

End Namespace
