Imports System.IO
Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Web

    Public Class CommissionRequestPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _catalog As New CatalogService()
        Private ReadOnly _svc As New CommissionService()
        Private ReadOnly _addrRepo As New UserAddressRepository()
        Private ReadOnly _assets As New AssetImageRepository()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                If Guard.IsPost() Then
                    Submit()
                    Return
                End If
                RenderForm("")
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the request form: " & ex.Message)
            End Try
        End Sub

        ' ---------------- POST: build & submit ----------------

        Private Sub Submit()
            Dim merchantId As Integer = 0
            Integer.TryParse(Request.Form("m"), merchantId)
            If merchantId <= 0 Then merchantId = _svc.PrimaryMerchantId()

            Dim catId As Integer = 0
            Integer.TryParse(Request.Form("cat"), catId)
            Dim title As String = Convert.ToString(Request.Form("title"))
            Dim description As String = Convert.ToString(Request.Form("description"))
            Dim qty As Integer = 1
            Integer.TryParse(Request.Form("qty"), qty)
            If qty < 1 Then qty = 1
            Dim size As String = Convert.ToString(Request.Form("size"))
            Dim notes As String = Convert.ToString(Request.Form("notes"))
            ' The finished piece is delivered, so the address is collected with the
            ' request rather than chased once the art is done.
            Dim street As String = Convert.ToString(Request.Form("addrStreet")).Trim()
            Dim barangay As String = Convert.ToString(Request.Form("addrBarangay")).Trim()
            Dim city As String = Convert.ToString(Request.Form("addrCity")).Trim()
            Dim province As String = Convert.ToString(Request.Form("addrProvince")).Trim()
            Dim postalCode As String = Convert.ToString(Request.Form("addrZip")).Trim()
            Dim landmark As String = Convert.ToString(Request.Form("addrLandmark")).Trim()
            Dim address As String = ComposeAddr(street, barangay, city, province, postalCode, landmark)
            Dim phone As String = Convert.ToString(Request.Form("phone"))
            Dim savedAddrId As Integer = 0
            Integer.TryParse(Request.Form("savedAddressId"), savedAddrId)
            ' No-JS fallback: a saved row chosen without the fields being filled.
            If savedAddrId > 0 AndAlso street = "" Then
                Dim a As UserAddress = _addrRepo.GetAddress(savedAddrId, STAR_DOM.Helpers.Session.CurrentUser.Id)
                If a IsNot Nothing Then
                    address = a.Compose
                    If String.IsNullOrWhiteSpace(phone) Then phone = a.Phone
                End If
            End If

            Dim deadline As Date? = Nothing
            Dim dl As String = Convert.ToString(Request.Form("deadline"))
            If dl <> "" Then
                Dim d As Date
                If Date.TryParse(dl, d) Then deadline = d
            End If

            Dim budgetMin As Decimal? = Nothing
            Dim budgetMax As Decimal? = Nothing
            Dim b1 As String = Convert.ToString(Request.Form("budgetMin"))
            Dim b2 As String = Convert.ToString(Request.Form("budgetMax"))
            Dim tmp As Decimal
            If Decimal.TryParse(b1, tmp) Then budgetMin = tmp
            If Decimal.TryParse(b2, tmp) Then budgetMax = tmp

            ' File references. The bytes go into AssetImages (not the local
            ' Uploads folder), so they survive a redeploy the same way venue and
            ' product images do; App/AssetImg.aspx serves them back.
            Dim refs As New List(Of (file As String, name As String, kb As Integer))()
            If Request.Files IsNot Nothing AndAlso Request.Files.Count > 0 Then
                Dim allowed As String() = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf"}
                For i As Integer = 0 To Request.Files.Count - 1
                    Dim f As HttpPostedFile = Request.Files(i)
                    If f Is Nothing OrElse f.ContentLength = 0 Then Continue For
                    Dim ext As String = Path.GetExtension(f.FileName).ToLowerInvariant()
                    If Array.IndexOf(allowed, ext) < 0 Then Continue For
                    If f.ContentLength > 25 * 1024 * 1024 Then Continue For
                    Dim bytes As Byte() = ReadPostedFile(f)
                    Dim storedPath As String = _assets.Save("Uploads/comm", f.FileName, bytes, f.ContentType, allowDocuments:=True)
                    If storedPath <> "" Then refs.Add((storedPath, IO.Path.GetFileName(f.FileName), f.ContentLength \ 1024))
                Next
            End If

            Dim result As ServiceResult = _svc.Submit(merchantId, catId, title, description, qty, size,
                                                      deadline, budgetMin, budgetMax, notes, refs,
                                                      address, phone)
            If result.Success Then
                ' Opt-in save so the buyer can reuse this delivery address.
                If Request.Form("saveToProfile") IsNot Nothing AndAlso street <> "" Then
                    _addrRepo.CreateIfNew(STAR_DOM.Helpers.Session.CurrentUser.Id, "Delivery",
                                          street, barangay, city, province, postalCode, landmark, phone)
                End If
                Dim fresh As Commission = _svc.ListMyCommissions().OrderByDescending(Function(c) c.Id).FirstOrDefault()
                Session("flash_msg") = result.Message
                Session("flash_ok") = True
                If fresh IsNot Nothing Then
                    Response.Redirect("/App/CommissionDetail.aspx?id=" & fresh.Id.ToString(), True)
                Else
                    Response.Redirect("/App/CommissionHub.aspx", True)
                End If
            Else
                RenderForm(result.Message)
            End If
        End Sub

        ''' <summary>The split delivery fields joined into the single line the commissions table stores.</summary>
        Private Shared Function ComposeAddr(street As String, barangay As String, city As String,
                                            province As String, postalCode As String, landmark As String) As String
            Dim sb As New StringBuilder()
            sb.Append(If(street, "").Trim())
            If Not String.IsNullOrWhiteSpace(barangay) Then sb.Append(", Brgy. " & barangay.Trim())
            If Not String.IsNullOrWhiteSpace(city) Then sb.Append(", " & city.Trim())
            If Not String.IsNullOrWhiteSpace(province) Then sb.Append(", " & province.Trim())
            If Not String.IsNullOrWhiteSpace(postalCode) Then sb.Append(" " & postalCode.Trim())
            If Not String.IsNullOrWhiteSpace(landmark) Then sb.Append(" · landmark: " & landmark.Trim())
            Return sb.ToString()
        End Function

        ''' <summary>Reads an uploaded file fully into memory so it can be written to the database.</summary>
        Private Shared Function ReadPostedFile(f As HttpPostedFile) As Byte()
            Using ms As New MemoryStream()
                f.InputStream.CopyTo(ms)
                Return ms.ToArray()
            End Using
        End Function

        ' ---------------- GET: the five-step form ----------------

        ''' <summary>
        ''' How much of each step the buyer has filled in, read from the posted
        ''' form or the query string on a fresh GET (which counts as nothing
        ''' filled). Field names are the real ones on the form below: cat, title,
        ''' size, budgetMin/Max, deadline. Section 3 has no text field to read --
        ''' the reference upload is a file input -- so it reports done only once
        ''' the buyer has scrolled past it, via the __refsOk marker the review
        ''' block posts. Without that it would block the rail forever.
        ''' </summary>
        Private Function StepCompletion(count As Integer) As Boolean()
            Dim done() As Boolean = New Boolean(count - 1) {}

            Dim cat As String = Filled("cat")
            done(0) = cat <> ""

            done(1) = Filled("title") <> ""

            done(2) = Filled("__refsOk") <> ""

            ' Specifications is one field block: any of size, budget or deadline.
            done(3) = Filled("size") <> "" OrElse Filled("budgetMin") <> "" OrElse
                      Filled("budgetMax") <> "" OrElse Filled("deadline") <> ""

            ' Delivery: an address and a phone, since the finished piece is shipped.
            done(4) = Filled("addrStreet") <> "" AndAlso Filled("phone") <> ""

            ' The last step is the review/submit screen itself: never pre-filled.
            done(5) = False
            Return done
        End Function

        ''' <summary>
        ''' One field's value, preferring the POST body and falling back to the
        ''' query string, so the rail is correct after a validation bounce too.
        ''' Convert.ToString, not CStr: an absent field hands CStr a Nothing.
        ''' </summary>
        Private Function Filled(name As String) As String
            Dim v As String = Trim(Convert.ToString(Request.Form(name)))
            If v = "" Then v = Trim(Convert.ToString(Request.QueryString(name)))
            Return v
        End Function

        ''' <summary>One line telling the buyer what this step wants from them.</summary>
        Private Function StepHint(index As Integer) As String
            Select Case index
                Case 0 : Return "Pick the merchandise substrate or format you have in mind."
                Case 1 : Return "Name your project and describe the idea in detail."
                Case 2 : Return "Attach reference art or a moodboard if you have one."
                Case 3 : Return "Set your budget range, size and deadline."
                Case 4 : Return "Check everything over, then send your request."
                Case Else : Return ""
            End Select
        End Function

        Private Sub RenderForm(errorMsg As String)
            Dim merchantId As Integer = 0
            Integer.TryParse(Request.QueryString("m"), merchantId)
            If merchantId <= 0 Then merchantId = _svc.PrimaryMerchantId()

            ' Repopulate from whatever survived the bounce. Filled() prefers the POST
            ' body and falls back to the query string, so this is blank on a first
            ' visit and carries the buyer's work back after a validation error.
            Dim keepCat As String = Filled("cat")
            Dim keepTitle As String = Filled("title")
            Dim keepQty As String = Filled("qty")
            If keepQty = "" Then keepQty = "1"
            Dim keepSize As String = Filled("size")
            Dim keepBudgetMin As String = Filled("budgetMin")
            Dim keepBudgetMax As String = Filled("budgetMax")
            Dim keepNotes As String = Filled("notes")
            Dim keepStreet As String = Filled("addrStreet")
            Dim keepBarangay As String = Filled("addrBarangay")
            Dim keepCity As String = Filled("addrCity")
            Dim keepProvince As String = Filled("addrProvince")
            Dim keepZip As String = Filled("addrZip")
            Dim keepLandmark As String = Filled("addrLandmark")
            Dim keepPhone As String = Filled("phone")
            ' An <input type="date"> only accepts yyyy-MM-dd; echo back anything it
            ' would reject as empty rather than as a value the browser silently drops.
            Dim keepDeadline As String = ""
            Dim dlText As String = Filled("deadline")
            If dlText <> "" Then
                Dim dlDate As Date
                If Date.TryParse(dlText, dlDate) Then keepDeadline = dlDate.ToString("yyyy-MM-dd")
            End If
            Dim selectedAddrId As String = Filled("savedAddressId")
            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Custom Commercial Commission Request",
                                    "STAR:DOM ATELIER · BESPOKE COMMISSIONS",
                                    "Request custom physical merchandise, illustrations, or bespoke digital artwork directly from the STAR:DOM artist. " &
                                    "No rigid packages — describe what you envision and we will review and quote your project. " &
                                    "Commissioned products can only be claimed via delivery."))

            ' Step rail. This form is one long page, not a multi-screen wizard,
            ' so "where the user is" has to come from what has actually been filled
            ' in: the first section with nothing in it yet is the one to do next,
            ' and everything above it is done. The .step.on class was already in
            ' the stylesheet but nothing ever set it, so all five chips rendered
            ' identically and the rail told the buyer nothing.
            Dim titles As String() = {"1. Select Category", "2. What to Create",
                                      "3. References &amp; Assets", "4. Specifications",
                                      "5. Delivery", "6. Review &amp; Submit"}
            Dim stepDone As Boolean() = StepCompletion(titles.Length)
            Dim currentStep As Integer = 0
            For i As Integer = 0 To stepDone.Length - 1
                If Not stepDone(i) Then
                    currentStep = i
                    Exit For
                End If
            Next

            sb.Append("<div class=""steps"" role=""list"" aria-label=""Request progress"">")
            For i As Integer = 0 To titles.Length - 1
                Dim cls As String = "step"
                Dim mark As String = ""
                If i = currentStep Then
                    cls &= " on"
                    mark = " <span class=""step-ic"" aria-label=""current step"">&#9679;</span>"
                ElseIf stepDone(i) Then
                    cls &= " done"
                    mark = " <span class=""step-ic"" aria-label=""done"">&#10003;</span>"
                End If
                Dim cur As String = "false"
                If i = currentStep Then cur = "step"
                sb.Append("<span class=""" & cls & """ role=""listitem"" aria-current=""" & cur & """>" &
                          titles(i) & mark & "</span>")
            Next
            sb.Append("</div>")
            sb.Append("<p class=""sub"" style=""font-size:12px;margin:-6px 0 14px""><b>Step " &
                      (currentStep + 1).ToString() & " of 6</b> &middot; " &
                      WebUi.Esc(StepHint(currentStep)) & "</p>")

            If errorMsg <> "" Then sb.Append(WebUi.AlertBox(errorMsg))

            sb.Append("<form method=""post"" action=""/App/CommissionRequest.aspx"" enctype=""multipart/form-data"">")
            ' Nested inside the shell form, which the browser closes at this tag — so the
            ' shell's token is not submitted with this form. Carry its own.
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<input type=""hidden"" name=""m"" value=""" & merchantId.ToString() & """>")

            ' 01 category
            Dim cats As List(Of Category) = _catalog.ListCategories()
            sb.Append("<div class=""card mb"">")
            sb.Append("<div class=""row space-between""><h3>01 · Category Selection</h3><span class=""sub"" style=""font-size:11px"">" & WebUi.Ic("sync", "sm") & " DB: SYNC ACTIVE</span></div>")
            sb.Append("<p class=""sub"">Choose the base merchandise substrate or bespoke format.</p>")
            sb.Append("<div class=""grid cards"" style=""grid-template-columns:repeat(auto-fill,minmax(190px,1fr));margin-top:10px"">")
            For Each c As Category In cats
                sb.Append("<label class=""card"" style=""cursor:pointer;display:flex;flex-direction:column;gap:2px;margin:0"">")
                Dim isPicked As String = ""
                If keepCat = c.Id.ToString() Then isPicked = " checked"
                sb.Append("<input type=""radio"" name=""cat"" value=""" & c.Id.ToString() & """" & isPicked & " required>")
                sb.Append("<b>" & WebUi.Esc(c.Name) & "</b>")
                sb.Append("<span class=""sub"" style=""font-size:11px"">" & WebUi.Esc(c.Description) & "</span>")
                sb.Append("</label>")
            Next
            sb.Append("</div>")
            sb.Append("</div>")

            ' 02 description
            sb.Append("<div class=""card mb"">")
            sb.Append("<h3>02 · What would you like to create?</h3>")
            sb.Append("<p class=""sub"">Give the atelier a short request title, then describe your idea in detail.</p>")
            sb.Append("<div class=""field""><label for=""tt"">Request title *</label><input id=""tt"" name=""title"" required value=""" & WebUi.Attr(keepTitle) & """ placeholder=""e.g. 150pc holographic vinyl sticker batch""></div>")
            sb.Append("<div class=""field""><label for=""dd"">Full description *</label><textarea id=""dd"" name=""description"" required style=""min-height:160px"">" &
                      WebUi.Esc(Filled("description")) & "</textarea></div>")
            sb.Append("</div>")

            ' 03 references
            sb.Append("<div class=""card mb"">")
            sb.Append("<div class=""row space-between""><h3>03 · Reference Images &amp; Moodboard</h3><span class=""sub"" style=""font-size:11px"">PNG, JPG, GIF, WEBP, PDF · max 25 MB each</span></div>")
            sb.Append("<p class=""sub"">Attach artwork, colour swatches, or previous merch references (up to 3 files).</p>")
            For i As Integer = 0 To 2
                sb.Append("<div class=""field""><label for=""rf" & i.ToString() & """>Reference " & (i + 1).ToString() & "</label>" &
                          "<input id=""rf" & i.ToString() & """ type=""file"" name=""ref" & i.ToString() & """></div>")
            Next
            ' A file input posts nothing when left empty, so the step rail could
            ' never see it. This marker always travels, which lets step 3 count as
            ' reached once the buyer has actually visited that section.
            sb.Append("<input type=""hidden"" name=""__refsOk"" value=""1"">")
            sb.Append("</div>")

            ' 04 specs
            sb.Append("<div class=""card mb"">")
            sb.Append("<h3>04 · Production Specifications</h3>")
            sb.Append("<p class=""sub"">Help us estimate accurate labour, stock and finishing costs.</p>")
            sb.Append("<div class=""form-grid2"">")
            sb.Append("<div class=""field""><label for=""q"">Production quantity *</label><input id=""q"" name=""qty"" type=""number"" min=""1"" value=""" & WebUi.Attr(keepQty) & """ required></div>")
            sb.Append("<div class=""field""><label for=""sz"">Preferred dimensions / size *</label><input id=""sz"" name=""size"" value=""" & WebUi.Attr(keepSize) & """ placeholder=""e.g. 3.5 x 3.5 in die-cut""></div>")
            sb.Append("<div class=""field""><label for=""dl"">Target deadline</label><input id=""dl"" name=""deadline"" type=""date"" value=""" & WebUi.Attr(keepDeadline) & """></div>")
            sb.Append("<div class=""field""><label for=""b1"">Budget range (₱)</label><div class=""row""><input id=""b1"" name=""budgetMin"" type=""number"" step=""0.01"" value=""" & WebUi.Attr(keepBudgetMin) & """ placeholder=""min"" style=""width:130px""> – <input name=""budgetMax"" type=""number"" step=""0.01"" value=""" & WebUi.Attr(keepBudgetMax) & """ placeholder=""max"" style=""width:130px""></div></div>")
            sb.Append("</div>")
            sb.Append("<div class=""field""><label for=""nt"">Additional notes / bleed requests</label><textarea id=""nt"" name=""notes"" style=""min-height:80px"">" & WebUi.Esc(keepNotes) & "</textarea></div>")
            sb.Append("</div>")

            ' 05 delivery
            sb.Append("<div class=""card mb"">")
            sb.Append("<h3>05 · Delivery</h3>")
            sb.Append("<p class=""sub"">Your finished piece is delivered by J&amp;T Express. Shipping is free on " &
                      "commissions — this address is just where it goes.</p>")
            Dim addrs As List(Of UserAddress) = Nothing
            If STAR_DOM.Helpers.Session.CurrentUser IsNot Nothing Then
                addrs = _addrRepo.ListByUserId(STAR_DOM.Helpers.Session.CurrentUser.Id)
            End If
            If addrs IsNot Nothing AndAlso addrs.Count > 0 Then
                sb.Append("<div class='field'><label for='savedAddressId'>Use a saved address</label>")
                sb.Append("<select id='savedAddressId' name='savedAddressId' style='width:100%;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px'>")
                sb.Append("<option value=''>Enter a new address</option>")
                For Each a As UserAddress In addrs
                    Dim sel As String = If(selectedAddrId = a.Id.ToString(), " selected", "")
                    sb.Append("<option value='" & a.Id.ToString() & "'" & sel &
                              " data-street='" & WebUi.Attr(a.Address) &
                              "' data-barangay='" & WebUi.Attr(a.Barangay) &
                              "' data-city='" & WebUi.Attr(a.City) &
                              "' data-province='" & WebUi.Attr(a.Region) &
                              "' data-zip='" & WebUi.Attr(a.PostalCode) &
                              "' data-landmark='" & WebUi.Attr(a.Landmark) &
                              "' data-phone='" & WebUi.Attr(a.Phone) & "'>" &
                              WebUi.Esc(If(a.Label <> "", a.Label & ": ", "") & a.Compose) & "</option>")
                Next
                sb.Append("</select>")
                sb.Append("<div class='sub' style='font-size:11.5px;margin-top:4px'>Picking one fills the fields below — adjust anything that has changed.</div>")
                sb.Append("</div>")
            End If
            sb.Append("<div id='addrManual'>")
            sb.Append("<div class='form-grid2'>")
            sb.Append("<div class='field'><label for='ad'>House number and street *</label>")
            sb.Append("<input id='ad' name='addrStreet' required value='" & WebUi.Attr(keepStreet) & "' placeholder='Blk 1 Lot 2, Sample St.'></div>")
            sb.Append("<div class='field'><label for='ab'>Barangay</label>")
            sb.Append("<input id='ab' name='addrBarangay' value='" & WebUi.Attr(keepBarangay) & "' placeholder='Barangay San Isidro'></div>")
            sb.Append("</div>")
            sb.Append("<div class='form-grid2'>")
            sb.Append("<div class='field'><label for='ac'>City / Municipality *</label>")
            sb.Append("<input id='ac' name='addrCity' required value='" & WebUi.Attr(keepCity) & "' placeholder='Quezon City'></div>")
            sb.Append("<div class='field'><label for='ap'>Province *</label>")
            sb.Append("<input id='ap' name='addrProvince' required value='" & WebUi.Attr(keepProvince) & "' placeholder='Metro Manila'></div>")
            sb.Append("</div>")
            sb.Append("<div class='form-grid2'>")
            sb.Append("<div class='field'><label for='az'>Postal code *</label>")
            sb.Append("<input id='az' name='addrZip' required inputmode='numeric' pattern='[0-9]{4}' value='" & WebUi.Attr(keepZip) & "' placeholder='1101'></div>")
            sb.Append("<div class='field'><label for='al'>Nearest landmark</label>")
            sb.Append("<input id='al' name='addrLandmark' value='" & WebUi.Attr(keepLandmark) & "' placeholder='Near SM, beside bakery, etc.'></div>")
            sb.Append("</div>")
            sb.Append("<div class='field'><label for='ph'>Contact phone *</label>")
            sb.Append("<input id='ph' name='phone' required inputmode='tel' value='" & WebUi.Attr(keepPhone) & "' placeholder='09xx xxx xxxx'>")
            sb.Append("</div>")
            sb.Append("<label class='row' style='gap:8px;align-items:center;font-size:13px;cursor:pointer'>")
            sb.Append("<input type='checkbox' name='saveToProfile' value='1' checked style='width:auto;margin:0'>")
            sb.Append("<span>Save this address to my profile so I can reuse it next time.</span></label>")
            sb.Append("<p class='sub' style='margin:6px 0 0;font-size:11px'>Addresses saved here also appear on <a href='/App/Profile.aspx'>your profile</a>.</p>")
            sb.Append("</div>")
            sb.Append("</div>")

            ' 06 submit
            sb.Append("<div class=""card"" style=""border-color:var(--yellow)"">")
            sb.Append("<div class=""row space-between"">")
            sb.Append("<div><b>No upfront payment required today</b><br><span class=""sub"">Your request is reviewed by the artist before final pricing.</span></div>")
            sb.Append("<button class=""btn primary"" type=""submit"" style=""font-size:15px;padding:12px 26px""><span class=""ic ms"">send</span><span>Submit Commission Request</span></button>")
            sb.Append("</div></div>")
            sb.Append("</form>")

            ' Saved-address picker: only fill fields the chosen row actually carries.
            sb.Append("<script>")
            sb.Append("(function(){")
            sb.Append("var sel=document.getElementById('savedAddressId');if(!sel)return;")
            sb.Append("sel.addEventListener('change',function(){")
            sb.Append("var o=sel.options[sel.selectedIndex];if(!o||!o.value)return;")
            sb.Append("function fill(id,v){var el=document.getElementById(id);if(el&&v)el.value=v;}")
            sb.Append("fill('ad',o.getAttribute('data-street'));")
            sb.Append("fill('ab',o.getAttribute('data-barangay'));")
            sb.Append("fill('ac',o.getAttribute('data-city'));")
            sb.Append("fill('ap',o.getAttribute('data-province'));")
            sb.Append("fill('az',o.getAttribute('data-zip'));")
            sb.Append("fill('al',o.getAttribute('data-landmark'));")
            sb.Append("fill('ph',o.getAttribute('data-phone'));")
            sb.Append("});")
            sb.Append("})();")
            sb.Append("</" & "script>")
            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
