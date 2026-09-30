Imports System.IO
Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class CommissionRequestPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _catalog As New CatalogService()
        Private ReadOnly _svc As New CommissionService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                If Guard.IsPost() Then
                    Submit()
                    Return
                End If
                RenderForm("", "")
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

            ' file references
            Dim refs As New List(Of (file As String, name As String, kb As Integer))()
            If Request.Files IsNot Nothing AndAlso Request.Files.Count > 0 Then
                Dim allowed As String() = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf"}
                For i As Integer = 0 To Request.Files.Count - 1
                    Dim f As HttpPostedFile = Request.Files(i)
                    If f Is Nothing OrElse f.ContentLength = 0 Then Continue For
                    Dim ext As String = Path.GetExtension(f.FileName).ToLowerInvariant()
                    If Array.IndexOf(allowed, ext) < 0 Then Continue For
                    If f.ContentLength > 25 * 1024 * 1024 Then Continue For
                    Dim dirPath As String = Server.MapPath("~/Uploads/comm")
                    Directory.CreateDirectory(dirPath)
                    Dim stored As String = Guid.NewGuid().ToString("N") & ext
                    f.SaveAs(Path.Combine(dirPath, stored))
                    refs.Add(("Uploads/comm/" & stored, Path.GetFileName(f.FileName), f.ContentLength \ 1024))
                Next
            End If

            Dim result As ServiceResult = _svc.Submit(merchantId, catId, title, description, qty, size,
                                                      deadline, budgetMin, budgetMax, notes, refs)
            If result.Success Then
                Dim fresh As Commission = _svc.ListMyCommissions().OrderByDescending(Function(c) c.Id).FirstOrDefault()
                Session("flash_msg") = result.Message
                Session("flash_ok") = True
                If fresh IsNot Nothing Then
                    Response.Redirect("/App/CommissionDetail.aspx?id=" & fresh.Id.ToString(), True)
                Else
                    Response.Redirect("/App/CommissionHub.aspx", True)
                End If
            Else
                RenderForm(result.Message, description)
            End If
        End Sub

        ' ---------------- GET: the five-step form ----------------

        Private Sub RenderForm(errorMsg As String, keepDescription As String)
            Dim merchantId As Integer = 0
            Integer.TryParse(Request.QueryString("m"), merchantId)
            If merchantId <= 0 Then merchantId = _svc.PrimaryMerchantId()

            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Custom Commercial Commission Request",
                                    "STAR:DOM ATELIER · BESPOKE COMMISSIONS",
                                    "Request custom physical merchandise, illustrations, or bespoke digital artwork directly from the STAR:DOM artist. " &
                                    "No rigid packages — describe what you envision and we will review and quote your project."))

            ' step rail
            sb.Append("<div class=""steps"">")
            For Each s As String In {"1. Select Category", "2. What to Create", "3. References &amp; Assets", "4. Specifications", "5. Review &amp; Submit"}
                sb.Append("<span class=""step"">" & s & "</span>")
            Next
            sb.Append("</div>")

            If errorMsg <> "" Then sb.Append(WebUi.AlertBox(errorMsg))

            sb.Append("<form method=""post"" action=""/App/CommissionRequest.aspx"" enctype=""multipart/form-data"">")
            sb.Append("<input type=""hidden"" name=""m"" value=""" & merchantId.ToString() & """>")

            ' 01 category
            Dim cats As List(Of Category) = _catalog.ListCategories()
            sb.Append("<div class=""card mb"">")
            sb.Append("<div class=""row space-between""><h3>01 · Category Selection</h3><span class=""sub"" style=""font-size:11px"">" & WebUi.Ic("sync", "sm") & " DB: SYNC ACTIVE</span></div>")
            sb.Append("<p class=""sub"">Choose the base merchandise substrate or bespoke format.</p>")
            sb.Append("<div class=""grid cards"" style=""grid-template-columns:repeat(auto-fill,minmax(190px,1fr));margin-top:10px"">")
            For Each c As Category In cats
                sb.Append("<label class=""card"" style=""cursor:pointer;display:flex;flex-direction:column;gap:2px;margin:0"">")
                sb.Append("<input type=""radio"" name=""cat"" value=""" & c.Id.ToString() & """ required>")
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
            sb.Append("<div class=""field""><label for=""tt"">Request title *</label><input id=""tt"" name=""title"" required placeholder=""e.g. 150pc holographic vinyl sticker batch""></div>")
            sb.Append("<div class=""field""><label for=""dd"">Full description *</label><textarea id=""dd"" name=""description"" required style=""min-height:160px"">" &
                      WebUi.Esc(keepDescription) & "</textarea></div>")
            sb.Append("</div>")

            ' 03 references
            sb.Append("<div class=""card mb"">")
            sb.Append("<div class=""row space-between""><h3>03 · Reference Images &amp; Moodboard</h3><span class=""sub"" style=""font-size:11px"">PNG, JPG, GIF, WEBP, PDF · max 25 MB each</span></div>")
            sb.Append("<p class=""sub"">Attach artwork, colour swatches, or previous merch references (up to 3 files).</p>")
            For i As Integer = 0 To 2
                sb.Append("<div class=""field""><label for=""rf" & i.ToString() & """>Reference " & (i + 1).ToString() & "</label>" &
                          "<input id=""rf" & i.ToString() & """ type=""file"" name=""ref" & i.ToString() & """></div>")
            Next
            sb.Append("</div>")

            ' 04 specs
            sb.Append("<div class=""card mb"">")
            sb.Append("<h3>04 · Production Specifications</h3>")
            sb.Append("<p class=""sub"">Help us estimate accurate labour, stock and finishing costs.</p>")
            sb.Append("<div class=""form-grid2"">")
            sb.Append("<div class=""field""><label for=""q"">Production quantity *</label><input id=""q"" name=""qty"" type=""number"" min=""1"" value=""1"" required></div>")
            sb.Append("<div class=""field""><label for=""sz"">Preferred dimensions / size *</label><input id=""sz"" name=""size"" placeholder=""e.g. 3.5 x 3.5 in die-cut""></div>")
            sb.Append("<div class=""field""><label for=""dl"">Target deadline</label><input id=""dl"" name=""deadline"" type=""date""></div>")
            sb.Append("<div class=""field""><label for=""b1"">Budget range (₱)</label><div class=""row""><input id=""b1"" name=""budgetMin"" type=""number"" step=""0.01"" placeholder=""min"" style=""width:130px""> – <input name=""budgetMax"" type=""number"" step=""0.01"" placeholder=""max"" style=""width:130px""></div></div>")
            sb.Append("</div>")
            sb.Append("<div class=""field""><label for=""nt"">Additional notes / bleed requests</label><textarea id=""nt"" name=""notes"" style=""min-height:80px""></textarea></div>")
            sb.Append("</div>")

            ' 05 submit
            sb.Append("<div class=""card"" style=""border-color:var(--yellow)"">")
            sb.Append("<div class=""row space-between"">")
            sb.Append("<div><b>No upfront payment required today</b><br><span class=""sub"">Your request is reviewed by the artist before final pricing.</span></div>")
            sb.Append("<button class=""btn primary"" type=""submit"" style=""font-size:15px;padding:12px 26px""><span class=""ic ms"">send</span><span>Submit Commission Request</span></button>")
            sb.Append("</div></div>")
            sb.Append("</form>")
            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
