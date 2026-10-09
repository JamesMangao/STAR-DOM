Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Web

    Public Class ProfilePage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _auth As New AuthService()
        Private ReadOnly _userRepo As New UserRepository()
        Private ReadOnly _addressRepo As New UserAddressRepository()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            ' The profile page is shopper-only (saved delivery addresses, password,
            ' customer order history). Studio accounts have their own area and no
            ' PROFILE nav entry any more, so a typed URL sends them to their own home
            ' instead of an account page they are not meant to have.
            If STAR_DOM.Helpers.Session.CanManageStore Then
                Response.Redirect("/App/Merchant/Dashboard.aspx", True)
            End If
            Try
                Dim currentUserId As Integer = STAR_DOM.Helpers.Session.CurrentUser.Id
                If Guard.IsPost() Then
                    ' The page lives inside Site.master's single shell <form>, so an
                    ' HTML parser drops every nested <form> start tag and one click
                    ' publishes that page's WHOLE field set. Hidden "action" inputs
                    ' from the password card and each address row therefore arrived
                    ' comma-joined ("changepassword,deleteaddress,addaddress") and no
                    ' branch ever matched — the whole card was dead. The dispatch key
                    ' now rides on the submit button itself, and only the button the
                    ' user pressed is sent.
                    If Request.Form("changepw") IsNot Nothing Then
                        Dim current As String = Convert.ToString(Request.Form("current"))
                        Dim np As String = Convert.ToString(Request.Form("np"))
                        Dim confirm As String = Convert.ToString(Request.Form("confirm"))
                        Dim r As ServiceResult = _auth.ChangePassword(currentUserId, current, np, confirm)
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
                        Response.Redirect("/App/Profile.aspx", True)
                    End If
                    ' Save (new when blank, edit when it carries an address id).
                    If Request.Form("saveaddr") IsNot Nothing Then
                        Dim a As ServiceResult = HandleSaveAddress(currentUserId, Convert.ToString(Request.Form("saveaddr")))
                        Session("flash_msg") = a.Message
                        Session("flash_ok") = a.Success
                        Response.Redirect("/App/Profile.aspx", True)
                    End If
                    If Request.Form("deladdr") IsNot Nothing Then
                        Dim id As Integer = 0
                        Integer.TryParse(Convert.ToString(Request.Form("deladdr")), id)
                        If id > 0 Then
                            Dim a As ServiceResult = _addressRepo.DeleteAddress(id, currentUserId)
                            Session("flash_msg") = a.Message
                            Session("flash_ok") = a.Success
                        End If
                        Response.Redirect("/App/Profile.aspx", True)
                    End If
                End If
                RenderProfile()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load profile: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderProfile()
            Dim u As Models.User = STAR_DOM.Helpers.Session.CurrentUser
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            ' ---- My Account card ----
            sb.Append(WebUi.Section("My Profile & Settings", "ACCOUNT",
                                    "Manage your account details, save addresses, and sign-in security."))

            sb.Append("<div class=""row"" style=""align-items:flex-start;gap:24px"">")
            sb.Append("<div class=""card"" style=""flex:1;min-width:300px"">")
            sb.Append("<div class=""row"" style=""margin-bottom:12px"">")
            sb.Append("<span class=""avatar"" style=""width:56px;height:56px;font-size:20px"">" & WebUi.Esc(Initials(u.FullName)) & "</span>")
            sb.Append("<div><h3 style=""margin:0"">" & WebUi.Esc(u.FullName) & "</h3><span class=""pill yellow"">" &
                      WebUi.Esc(u.DisplayRoleName) & "</span> " & WebUi.Badge(If(u.Status = "ACTIVE", "ACTIVE", u.Status)) & "</div>")
            sb.Append("</div>")
            sb.Append("<div class=""kv"">")
            sb.Append("<dt>Username</dt><dd>" & WebUi.Esc(u.Username) & "</dd>")
            sb.Append("<dt>Email</dt><dd>" & WebUi.Esc(u.Email) & "</dd>")
            sb.Append("<dt>Phone</dt><dd>" & WebUi.Esc(u.Phone) & "</dd>")
            sb.Append("<dt>Member since</dt><dd>" & WebUi.Esc(u.CreatedAt.ToString("MMMM d, yyyy")) & "</dd>")
            If u.LastLoginAt.HasValue Then sb.Append("<dt>Last login</dt><dd>" & WebUi.Esc(u.LastLoginAt.Value.ToString("MMM d, yyyy h:mm tt")) & "</dd>")
            sb.Append("</div>")
            sb.Append("</div>")

            ' ---- Change Password form ----
            sb.Append("<form method=""post"" action=""/App/Profile.aspx"" style=""flex:1;min-width:300px"">")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<div class=""card""><h3 style=""margin-bottom:10px"">Change password</h3>")
            sb.Append("<div class=""field""><label for=""c"">Current password</label><input id=""c"" name=""current"" type=""password"" required></div>")
            sb.Append("<div class=""field""><label for=""n"">New password (min 6 chars)</label><input id=""n"" name=""np"" type=""password"" required></div>")
            sb.Append("<div class=""field""><label for=""n2"">Confirm new password</label><input id=""n2"" name=""confirm"" type=""password"" required></div>")
            sb.Append("<button class=""btn primary"" type=""submit"" name=""changepw"" value=""1""><span class=""ic ms"">lock_reset</span><span>Update Password</span></button>")
            sb.Append("</div>")
            sb.Append("</form>")
            sb.Append("</div>")

            ' ---- Saved Addresses card ----
            sb.Append("<div class=""card"" style=""margin-top:16px"">")
            sb.Append("<h3 style=""margin-bottom:10px"">" & WebUi.Ic("home", "sm") & " Saved Delivery Addresses</h3>")
            sb.Append("<p class=""sub"" style=""margin:0 0 12px;color:var(--ink)"">" &
                      "Your saved addresses are used for deliveries and commissions. Add, edit, or remove addresses as needed." & "</p>")

            ' Address list
            Dim addresses As List(Of UserAddress) = _addressRepo.ListByUserId(u.Id)
            If addresses.Count = 0 Then
                sb.Append("<div class=""empty"" style=""padding:12px 0"">")
                sb.Append("<p class=""sub"" style=""margin:0"">You haven't saved any addresses yet. Add one below or enter a new address at checkout.</p></div>")
            Else
                sb.Append("<div style=""display:flex;flex-direction:column;gap:8px"">")
                For Each addr As UserAddress In addresses
                    sb.Append("<div class=""row"" style=""align-items:center;gap:12px;padding:10px;background:var(--surface-low);border-radius:10px;border:1px solid var(--line)"">")
                    sb.Append("<div style=""flex:1"">")
                    sb.Append("<div style=""font-weight:700;color:var(--ink)"">" & WebUi.Esc(addr.Label) & "</div>")
                    sb.Append("<div class=""sub"" style=""margin-top:2px"">" & WebUi.Esc(addr.Compose) & "</div>")
                    If addr.Phone <> "" Then
                        sb.Append("<div class=""sub"" style=""margin-top:2px"">📞 " & WebUi.Esc(addr.Phone) & "</div>")
                    End If
                    sb.Append("</div>")
                    ' Edit / Delete buttons
                    sb.Append("<div class=""frow"" style=""gap:6px"">")
                    sb.Append("<button type='button' class=""btn ghost sm"" onclick=""var f=document.getElementById('editAddrForm'); var h=document.getElementById('saveAddrBtn'); if(h){h.value='" & addr.Id.ToString() & "';} if(f){f.elements['label'].value=" & Js(addr.Label) & "; f.elements['address'].value=" & Js(addr.Address) & "; f.elements['brgy'].value=" & Js(addr.Barangay) & "; f.elements['city'].value=" & Js(addr.City) & "; f.elements['region'].value=" & Js(addr.Region) & "; f.elements['zip'].value=" & Js(addr.PostalCode) & "; f.elements['landmark'].value=" & Js(addr.Landmark) & "; f.elements['phone'].value=" & Js(addr.Phone) & ";} var p=document.getElementById('editAddrPanel'); if(p){p.scrollIntoView({behavior:'smooth',block:'center'});}"" style=""border-radius:8px;padding:6px 10px""><span class=""ms sm"">edit</span> Edit</button>")
                    sb.Append("<form method=""post"" action=""/App/Profile.aspx"" style=""display:inline"">")
                    sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                    sb.Append("<button class=""btn danger sm"" type=""submit"" name=""deladdr"" value=""" & addr.Id.ToString() & """ data-confirm=""" & WebUi.Attr("Remove this address?") & """ style=""border-radius:8px;padding:6px 10px""><span class=""ms sm"">delete</span> Remove</button>")
                    sb.Append("</form>")
                    sb.Append("</div>")
                    sb.Append("</div>")
                Next
                sb.Append("</div>")
            End If
            sb.Append("</div>")

            ' ---- Add / Edit Address form ----
            sb.Append("<div class=""card"" style=""margin-top:12px"" id=""editAddrPanel"">")
            sb.Append("<h3 style=""margin-bottom:10px"">" & WebUi.Ic("add_home", "sm") & " Add / Edit Address</h3>")
            sb.Append("<button type=""button"" id=""newAddrBtn"" class=""btn ghost sm"" style=""margin-bottom:10px"" onclick=""var f=document.getElementById('editAddrForm'); var h=document.getElementById('saveAddrBtn'); if(h){h.value='';} if(f){f.reset();}"" ><span class=""ic ms"">add</span> Add a new address</button>")
            sb.Append("<form method=""post"" action=""/App/Profile.aspx"" id=""editAddrForm"" style=""display:flex;flex-direction:column;gap:10px"">")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<div class=""field""><label for=""addrLabel"">Label (e.g., Home, Office) *</label>")
            sb.Append("<input id=""addrLabel"" name=""label"" required style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""Home""></div>")
            sb.Append("<div class=""field""><label for=""addrLine1"">House / street address *</label>")
            sb.Append("<input id=""addrLine1"" name=""address"" required style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""123 Main St, Apt 4B""></div>")
            sb.Append("<div class=""row"" style=""gap:10px"">")
            sb.Append("<div class=""field"" style=""flex:1""><label for=""addrBrgy"">Barangay</label>")
            sb.Append("<input id=""addrBrgy"" name=""brgy"" style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""Barangay Poblacion""></div>")
            sb.Append("<div class=""field"" style=""flex:1""><label for=""addrCity"">City / Municipality *</label>")
            sb.Append("<input id=""addrCity"" name=""city"" required style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""Mandaluyong""></div>")
            sb.Append("</div>")
            sb.Append("<div class=""row"" style=""gap:10px"">")
            sb.Append("<div class=""field"" style=""flex:2""><label for=""addrRegion"">Province</label>")
            sb.Append("<input id=""addrRegion"" name=""region"" style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""Metro Manila""></div>")
            sb.Append("<div class=""field"" style=""flex:1""><label for=""addrZip"">Postal code</label>")
            sb.Append("<input id=""addrZip"" name=""zip"" inputmode=""numeric"" style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""1550""></div>")
            sb.Append("</div>")
            sb.Append("<div class=""field""><label for=""addrLandmark"">Landmark (optional)</label>")
            sb.Append("<input id=""addrLandmark"" name=""landmark"" style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""Near the covered court / blue gate""></div>")
            sb.Append("<div class=""field""><label for=""addrPhone"">Contact phone (optional)</label>")
            sb.Append("<input id=""addrPhone"" name=""phone"" style=""width:100%;box-sizing:border-box;padding:10px 12px;border:1.5px solid var(--line);border-radius:10px;font-size:13px"" placeholder=""09123456789""></div>")
            sb.Append("<button class=""btn primary"" type=""submit"" id=""saveAddrBtn"" name=""saveaddr"" value=""""><span class=""ic ms"">add_home</span><span>Save Address</span></button>")
            sb.Append("</form>")
            sb.Append("</div>")
            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

        ''' <summary>
        ''' Add or edit in one handler. The id rides on the Save button's own value:
        ''' blank means "new", a number means "edit that row". Reading a shared
        ''' hidden addressId would have collected every row's id at once (the shell
        ''' form flattens the page), so the button is the only place it can live.
        ''' </summary>
        Private Function HandleSaveAddress(userId As Integer, idText As String) As ServiceResult
            Dim label As String = Trim(Convert.ToString(Request.Form("label")))
            Dim address As String = Trim(Convert.ToString(Request.Form("address")))
            Dim barangay As String = Trim(Convert.ToString(Request.Form("brgy")))
            Dim city As String = Trim(Convert.ToString(Request.Form("city")))
            Dim region As String = Trim(Convert.ToString(Request.Form("region")))
            Dim postalCode As String = Trim(Convert.ToString(Request.Form("zip")))
            Dim landmark As String = Trim(Convert.ToString(Request.Form("landmark")))
            Dim phone As String = Trim(Convert.ToString(Request.Form("phone")))
            Dim addrId As Integer = 0
            Integer.TryParse(Trim(Convert.ToString(idText)), addrId)

            If label = "" Then Return ServiceResult.Fail("Please enter a label for this address.")
            If address = "" Then Return ServiceResult.Fail("Please enter the house / street address.")
            If city = "" Then Return ServiceResult.Fail("Please enter the city / municipality.")

            If addrId > 0 Then
                ' Edit existing address
                Return _addressRepo.UpdateAddress(addrId, userId, label, address, barangay, city, region, postalCode, landmark, phone)
            End If

            ' Add new address
            Return _addressRepo.CreateAddress(userId, label, address, barangay, city, region, postalCode, landmark, phone)
        End Function

        ''' <summary>Single-quoted JavaScript string literal, safe to drop in an onclick attribute.</summary>
        Private Shared Function Js(s As String) As String
            Return "'" & Convert.ToString(s).Replace("\", "\\").Replace("'", "\'").Replace(vbCr, "").Replace(vbLf, "\n") & "'"
        End Function

        Private Function Initials(name As String) As String
            Dim parts As String() = name.Trim().Split(" "c)
            Dim s As String = ""
            For i As Integer = 0 To Math.Min(parts.Length - 1, 1)
                If parts(i).Length > 0 Then s &= Char.ToUpperInvariant(parts(i)(0))
            Next
            If s = "" Then s = "?"
            Return s
        End Function

    End Class

End Namespace
