Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class CheckoutPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _cart As New CartService()
        Private ReadOnly _orders As New OrderService()

        ' The orders table stores one ShippingAddress string, so the split inputs are
        ' joined back together by ComposeAddress on the way in.
        Private Const AddressMax As Integer = 255

        ''' <summary>One POST's worth of checkout fields, kept so a validation failure can
        ''' redisplay what the buyer typed instead of making them retype six address
        ''' fields. Nothing is read back out of the session, so it needs no serialization.
        ''' </summary>
        Private Class CheckoutDraft
            Public Street As String = ""
            Public Barangay As String = ""
            Public City As String = ""
            Public Province As String = ""
            Public Zip As String = ""
            Public Landmark As String = ""
            Public Phone As String = ""
            Public Notes As String = ""
            Public PaymentMethod As String = "COD"
            Public Fulfillment As String = "DELIVERY"
            Public PickupEventId As Integer = 0
        End Class

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                If Guard.IsPost() Then
                    Dim d As CheckoutDraft = ReadPost()
                    Dim problem As String = CheckDraft(d)
                    If problem IsNot Nothing Then
                        RenderForm(d, problem)
                        Return
                    End If
                    PlaceOrder(d)
                    Return
                End If
                RenderForm(Nothing, Nothing)
            Catch aborted As System.Threading.ThreadAbortException
                ' Response.Redirect(url, True) raises this by design once the redirect
                ' is already committed. Let it re-raise — the redirect must stand.
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Checkout failed: " & ex.Message)
            End Try
        End Sub

        Private Function ReadPost() As CheckoutDraft
            Dim d As New CheckoutDraft()
            ' Convert.ToString(String) hands back Nothing for a missing field — and the
            ' delivery inputs are never posted while pick-up is selected. Coerce
            ' everything to "" so nothing downstream sees a null.
            d.Street = Posted("addrStreet")
            d.Barangay = Posted("addrBarangay")
            d.City = Posted("addrCity")
            d.Province = Posted("addrProvince")
            d.Zip = Posted("addrZip")
            d.Landmark = Posted("addrLandmark")
            d.Phone = Posted("phone")
            d.Notes = Posted("notes")
            d.PaymentMethod = Posted("pm")
            If d.PaymentMethod = "" Then d.PaymentMethod = "COD"
            d.Fulfillment = Posted("fulfillment")
            If d.Fulfillment = "" Then d.Fulfillment = "DELIVERY"
            Dim stallId As Integer
            ' Through a local, not the property directly: TryParse writes its result
            ' ByRef, and VB binds a property argument through a hidden copy, so the
            ' draft would silently keep its default.
            Integer.TryParse(Posted("pickupEvent"), stallId)
            d.PickupEventId = stallId
            Return d
        End Function

        ''' <summary>One posted value, trimmed, or "" when the field was not sent.</summary>
        ''' <remarks>
        ''' Convert.ToString hands back Nothing for a missing field, and a disabled
        ''' input (the whole address block while pick-up is selected) is never posted
        ''' at all — so the value has to be coalesced before it is trimmed, not after.
        ''' Named Posted rather than Form: Page already has a Form property, and
        ''' shadowing it would be a trap for anyone editing this page later.
        ''' </remarks>
        Private Function Posted(name As String) As String
            Dim raw As String = Convert.ToString(Request.Form(name))
            Return If(raw, "").Trim()
        End Function

        ' Named CheckDraft rather than Validate for the same reason against Page.Validate.
        ''' <summary>Returns the first problem, or Nothing when the draft can be ordered.</summary>
        Private Function CheckDraft(d As CheckoutDraft) As String
            If d.Fulfillment <> "DELIVERY" AndAlso d.Fulfillment <> "PICKUP" Then
                Return "Please choose delivery or stall pick-up."
            End If

            ' Fully qualified: this page imports System.Web.UI, which brings its own
            ' Validation.Validators into scope and would win the bare name.
            Dim problem As String

            If d.Fulfillment = "DELIVERY" Then
                ' There is no map pin to fall back on, so every part of the address is
                ' required: the courier has to work from the text alone.
                problem = STAR_DOM.Helpers.Validators.Required(d.Street, "House number and street")
                If problem IsNot Nothing Then Return problem
                problem = STAR_DOM.Helpers.Validators.Required(d.Barangay, "Barangay")
                If problem IsNot Nothing Then Return problem
                problem = STAR_DOM.Helpers.Validators.Required(d.City, "City / Municipality")
                If problem IsNot Nothing Then Return problem
                problem = STAR_DOM.Helpers.Validators.Required(d.Province, "Province")
                If problem IsNot Nothing Then Return problem
                problem = STAR_DOM.Helpers.Validators.Required(d.Zip, "Postal code")
                If problem IsNot Nothing Then Return problem
                problem = STAR_DOM.Helpers.Validators.PostalCode(d.Zip)
                If problem IsNot Nothing Then Return problem
                problem = STAR_DOM.Helpers.Validators.Required(d.Landmark, "Nearest landmark")
                If problem IsNot Nothing Then Return problem
                If ComposeAddress(d).Length > AddressMax Then
                    Return "That address is too long — please shorten the street or the landmark."
                End If
            Else
                ' Caught here rather than left to the service: a missing stall would
                ' otherwise come back as "that stall is not open", which reads as if a
                ' stall had been picked and then closed.
                If d.PickupEventId <= 0 Then Return "Please choose a pop-up stall for pick-up."
            End If

            problem = STAR_DOM.Helpers.Validators.Required(d.Phone, "Contact phone")
            If problem IsNot Nothing Then Return problem
            Return STAR_DOM.Helpers.Validators.Phone(d.Phone)
        End Function

        ''' <summary>
        ''' Joins the split address inputs into the single line the orders table stores.
        ''' </summary>
        Private Function ComposeAddress(d As CheckoutDraft) As String
            Dim sb As New StringBuilder()
            sb.Append(d.Street)
            If d.Barangay <> "" Then sb.Append(", Brgy. " & d.Barangay)
            If d.City <> "" Then sb.Append(", " & d.City)
            If d.Province <> "" Then sb.Append(", " & d.Province)
            If d.Zip <> "" Then sb.Append(" " & d.Zip)
            If d.Landmark <> "" Then sb.Append(" · landmark: " & d.Landmark)
            Return sb.ToString()
        End Function

        Private Sub PlaceOrder(d As CheckoutDraft)
            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then
                Session("flash_msg") = "Your cart is empty."
                Session("flash_ok") = False
                Response.Redirect("/App/Cart.aspx", True)
            End If

            Dim addr As String = If(d.Fulfillment = "DELIVERY", ComposeAddress(d), "")
            Dim result As ServiceResult = _orders.Checkout(d.PaymentMethod, addr, d.Phone, d.Notes, Nothing, "PENDING",
                                                           d.Fulfillment,
                                                           If(d.PickupEventId > 0, d.PickupEventId, Nothing))
            If result.Success Then
                ' find the freshest order to deep-link into
                Dim fresh As Order = _orders.ListMyOrders().OrderByDescending(Function(o) o.Id).FirstOrDefault()
                Session("flash_msg") = result.Message
                Session("flash_ok") = True
                If fresh IsNot Nothing Then
                    Response.Redirect("/App/OrderDetail.aspx?id=" & fresh.Id.ToString(), True)
                Else
                    Response.Redirect("/App/Orders.aspx", True)
                End If
            Else
                ' The service can still refuse (out of stock, stall closed). Show the
                ' message on the form with the buyer's input intact — a redirect would
                ' throw away six address fields they just filled in.
                RenderForm(d, result.Message)
            End If
        End Sub

        Private Sub RenderForm(d As CheckoutDraft, problem As String)
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))
            If problem <> "" Then sb.Append(WebUi.AlertBox(problem, "err"))

            ' Redisplay whatever the buyer already typed.
            Dim street As String = ""
            Dim brgy As String = ""
            Dim city As String = ""
            Dim prov As String = ""
            Dim zip As String = ""
            Dim land As String = ""
            Dim phone As String = ""
            Dim notes As String = ""
            Dim pmSel As String = "COD"
            Dim fulSel As String = "DELIVERY"
            Dim pkSel As Integer = 0
            If d IsNot Nothing Then
                street = d.Street
                brgy = d.Barangay
                city = d.City
                prov = d.Province
                zip = d.Zip
                land = d.Landmark
                phone = d.Phone
                notes = d.Notes
                pmSel = d.PaymentMethod
                fulSel = d.Fulfillment
                pkSel = d.PickupEventId
            End If

            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then
                sb.Append("<div class=""empty"">Nothing to check out. <a href=""/App/Catalog.aspx"" style=""color:var(--primary);font-weight:700;display:inline-flex;align-items:center;gap:4px"">Browse products <span class=""ms sm"">arrow_forward</span></a></div>")
                Out.Text = sb.ToString()
                Return
            End If

            sb.Append(WebUi.Section("Checkout", "SECURE ORDER",
                                    "Choose delivery or stall pick-up, then how to pay. Payments are simulated for this demo — no real charge is made."))

            Dim subtotal As Decimal = items.Sum(Function(i) i.LineTotal)
            Dim bundleDisc As Decimal = _cart.BundleDiscount(items)
            ' Delivery is free from ₱1,500; pick-up never carries a fee.
            Dim deliveryFee As Decimal = If(subtotal >= 1500D, 0D, 80D)
            Dim bundleNote As String = _cart.BundleNote()

            sb.Append("<div class=""row"" style=""align-items:flex-start;gap:24px"">")
            sb.Append("<form method=""post"" action=""/App/Checkout.aspx"" style=""flex:1.5;min-width:320px"">")
            ' Global.asax rejects any POST without the token, so it has to be in every form.
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())

            ' summary
            sb.Append("<div class=""card mb""><h3 style=""margin-bottom:10px"">Order summary</h3>")
            For Each it As CartItem In items
                sb.Append("<div class=""row space-between"" style=""padding:6px 0"">")
                sb.Append("<div>" & WebUi.Esc(it.ProductName) & " <span class=""sub"">× " & it.Quantity.ToString() & "</span></div>")
                sb.Append("<div>" & WebUi.Money(it.LineTotal) & "</div>")
                sb.Append("</div>")
            Next
            sb.Append("<hr style=""border:0;border-top:1px solid var(--line);margin:8px 0"">")
            sb.Append("<div class=""row space-between""><b>Subtotal</b><b>" & WebUi.Money(subtotal) & "</b></div>")
            If bundleDisc > 0D Then
                sb.Append("<div class=""row space-between"" style=""color:#15803d""><b>Bundle savings</b><b>−" & WebUi.Money(bundleDisc) & "</b></div>")
            End If
            sb.Append("<div class=""row space-between""><b>Shipping</b><span id=""shipCell"" class=""sub"">—" & "</span></div>")
            sb.Append("<div class=""row space-between"" style=""margin-top:6px""><b>Total</b><b id=""totalCell"" style=""color:var(--primary);font-size:18px"">" & WebUi.Money(subtotal - bundleDisc + deliveryFee) & "</b></div>")
            If bundleNote <> "" Then
                sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-top:8px""><span class=""ms sm"" style=""vertical-align:-3px;color:var(--primary)"">sell</span> " & WebUi.Esc(bundleNote) & "</div>")
            End If
            sb.Append("</div>")

            ' fulfillment: delivery vs stall pick-up
            sb.Append("<div class=""card mb""><h3 style=""margin-bottom:10px"">" & WebUi.Ic("local_shipping", "sm") & " Fulfillment</h3>")
            sb.Append("<label class=""card"" style=""display:flex;gap:12px;align-items:flex-start;margin-bottom:8px;cursor:pointer"">")
            sb.Append("<input type=""radio"" name=""fulfillment"" value=""DELIVERY""" & If(fulSel = "DELIVERY", " checked", "") & " style=""margin-top:3px"">")
            sb.Append("<span class=""ms"" style=""color:var(--primary)"">local_shipping</span>")
            sb.Append("<span><b>Delivery</b><br><span class=""sub"" style=""font-size:12px"">Ships nationwide via J&T Express · " &
                      If(deliveryFee > 0D, WebUi.Esc(Fmt.PHP(deliveryFee)) & " shipping (free over ₱1,500)", "Free shipping") & "</span></span></label>")
            sb.Append("<label class=""card"" style=""display:flex;gap:12px;align-items:flex-start;margin-bottom:8px;cursor:pointer"">")
            sb.Append("<input type=""radio"" name=""fulfillment"" value=""PICKUP""" & If(fulSel = "PICKUP", " checked", "") & " style=""margin-top:3px"">")
            sb.Append("<span class=""ms"" style=""color:var(--primary)"">storefront</span>")
            sb.Append("<span><b>Pick-up at a pop-up stall</b><br><span class=""sub"" style=""font-size:12px"">No shipping fee · claim in person during the stall's open hours</span></span></label>")

            ' delivery-only fields
            sb.Append("<div id=""deliveryFields"">")
            sb.Append("<div class=""field""><label for=""as1"">House number and street <span class=""sub"">*</span></label>")
            sb.Append("<input id=""as1"" name=""addrStreet"" required autocomplete=""address-line1"" placeholder=""Blk 1 Lot 2, Sample St."" value=""" & WebUi.Attr(street) & """></div>")
            sb.Append("<div class=""field""><label for=""as2"">Barangay <span class=""sub"">*</span></label>")
            sb.Append("<input id=""as2"" name=""addrBarangay"" required placeholder=""Barangay San Isidro"" value=""" & WebUi.Attr(brgy) & """></div>")
            sb.Append("<div class=""form-grid2"">")
            sb.Append("<div class=""field""><label for=""as3"">City / Municipality <span class=""sub"">*</span></label>")
            sb.Append("<input id=""as3"" name=""addrCity"" required autocomplete=""address-level2"" placeholder=""Quezon City"" value=""" & WebUi.Attr(city) & """></div>")
            sb.Append("<div class=""field""><label for=""as4"">Province <span class=""sub"">*</span></label>")
            sb.Append("<input id=""as4"" name=""addrProvince"" required autocomplete=""address-level1"" placeholder=""Metro Manila"" value=""" & WebUi.Attr(prov) & """></div>")
            sb.Append("</div>")
            sb.Append("<div class=""form-grid2"">")
            sb.Append("<div class=""field""><label for=""as5"">Postal code <span class=""sub"">*</span></label>")
            sb.Append("<input id=""as5"" name=""addrZip"" required inputmode=""numeric"" pattern=""[0-9]{4}"" autocomplete=""postal-code"" placeholder=""1101"" value=""" & WebUi.Attr(zip) & """></div>")
            sb.Append("<div class=""field""><label for=""as6"">Nearest landmark <span class=""sub"">*</span></label>")
            sb.Append("<input id=""as6"" name=""addrLandmark"" required placeholder=""Near SM, beside bakery, etc."" value=""" & WebUi.Attr(land) & """></div>")
            sb.Append("</div>")
            sb.Append("<div class=""sub"" style=""font-size:11.5px;margin:-4px 0 4px"">All fields are required because there's no map pin for this delivery address.</div>")
            sb.Append("</div>")

            ' pick-up-only fields
            sb.Append("<div id=""pickupFields"" style=""display:none"">")
            sb.Append("<div class=""field""><label for=""pk"">Choose a stall (open now or upcoming)</label>")
            sb.Append("<select id=""pk"" name=""pickupEvent"" style=""width:100%;padding:10px;border:1px solid var(--line);border-radius:10px;background:#fff"">")
            sb.Append("<option value="""">Choose a stall…</option>")
            For Each ev As PopUpEvent In New EventService().ListUpcoming()
                Dim whenText As String
                If ev.Status = "NOW OPEN" Then
                    whenText = "OPEN NOW · " & ev.OpenTime & "–" & ev.CloseTime
                Else
                    Dim span As String = ev.StartDate.ToString("MMM d")
                    If ev.EndDate.Date <> ev.StartDate.Date Then span &= "–" & ev.EndDate.ToString("MMM d")
                    whenText = span & " · " & ev.OpenTime & "–" & ev.CloseTime
                End If
                sb.Append("<option value=""" & ev.Id.ToString() & """" & If(ev.Id = pkSel, " selected", "") & ">" & WebUi.Attr(ev.Name & " — " & whenText) & "</option>")
            Next
            sb.Append("</select>")
            sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-top:6px"">Hours are shown per stall. Your claim is final when <b>both you and the stall team</b> confirm the hand-over.</div>")
            sb.Append("</div></div>")

            ' shared fields
            sb.Append("<div class=""field""><label for=""ph"">Contact phone</label><input id=""ph"" name=""phone"" required placeholder=""09xx xxx xxxx"" value=""" & WebUi.Attr(phone) & """></div>")
            sb.Append("<div class=""field""><label for=""nt"">Order notes (optional)</label><textarea id=""nt"" name=""notes"" style=""min-height:70px"">" & WebUi.Esc(notes) & "</textarea></div>")
            sb.Append("</div>")

            ' payment
            sb.Append("<div class=""card""><h3 style=""margin-bottom:10px"">" & WebUi.Ic("payments", "sm") & " Payment method</h3>")
            sb.Append(PayOption("GCASH", "GCash", "Pay instantly via the GCash app QR", "qr_code_2", pmSel))
            sb.Append(PayOption("MAYA", "Maya", "Pay with the Maya app", "account_balance_wallet", pmSel))
            sb.Append(PayOption("COD", "Cash on Delivery / Claim", "Pay cash when your order arrives or at pick-up", "local_shipping", pmSel))
            sb.Append("<div class=""frow"">")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">lock</span><span>Place Order</span></button>")
            sb.Append(WebUi.BtnHref("/App/Cart.aspx", "Back to Cart", "ghost", "arrow_back"))
            sb.Append("</div>")
            sb.Append("</div>")
            sb.Append("</form>")
            sb.Append("</div>")

            ' Live total recalculation: switching fulfillment swaps the shipping fee
            ' (₱0 at a stall) and shows/hides the address vs stall-picker fields.
            sb.Append("<script>")
            sb.Append("(function(){")
            sb.Append("var sub=" & subtotal.ToString(System.Globalization.CultureInfo.InvariantCulture) & ";")
            sb.Append("var disc=" & bundleDisc.ToString(System.Globalization.CultureInfo.InvariantCulture) & ";")
            sb.Append("var fee=" & deliveryFee.ToString(System.Globalization.CultureInfo.InvariantCulture) & ";")
            sb.Append("var rads=document.getElementsByName('fulfillment');")
            sb.Append("var del=document.getElementById('deliveryFields');")
            sb.Append("var pick=document.getElementById('pickupFields');")
            sb.Append("var ship=document.getElementById('shipCell');")
            sb.Append("var tot=document.getElementById('totalCell');")
            sb.Append("function money(n){return '₱'+n.toLocaleString('en-PH',{minimumFractionDigits:2,maximumFractionDigits:2});}")
            sb.Append("function isPickup(){for(var i=0;i<rads.length;i++){if(rads[i].checked&&rads[i].value==='PICKUP')return true;}return false;}")
            sb.Append("function refresh(){var p=isPickup();")
            sb.Append("del.style.display=p?'none':'';pick.style.display=p?'':'none';")
            ' The address is now six separate inputs, so toggle every one of them
            ' rather than a single id. Disabling is what stops the browser from
            ' demanding a delivery address on a pick-up order.
            sb.Append("var as=del.getElementsByTagName('input');")
            sb.Append("for(var i=0;i<as.length;i++){as[i].disabled=p;as[i].required=!p;}")
            sb.Append("var f=p?0:fee;")
            sb.Append("ship.innerHTML=f>0?money(f):'<span style=""color:#15803d;font-weight:700"">FREE</span>';")
            sb.Append("tot.innerHTML=money(Math.max(sub-disc+f,0));}")
            sb.Append("for(var i=0;i<rads.length;i++){rads[i].addEventListener('change',refresh);}")
            sb.Append("refresh();})();")
            sb.Append("</" & "script>")

            Out.Text = sb.ToString()
        End Sub

        Private Function PayOption(value As String, title As String, hint As String, icon As String,
                                        selected As String) As String
            Dim checkedAttr As String = If(value = selected, " checked", "")
            Return "<label class=""card"" style=""display:flex;gap:12px;align-items:flex-start;margin-bottom:8px;cursor:pointer"">" &
                   "<input type=""radio"" name=""pm"" value=""" & value & """" & checkedAttr & " style=""margin-top:3px"">" &
                   "<span class=""ms"" style=""color:var(--primary)"">" & WebUi.Esc(icon) & "</span>" &
                   "<span><b>" & WebUi.Esc(title) & "</b><br><span class=""sub"" style=""font-size:12px"">" & WebUi.Esc(hint) & "</span></span></label>"
        End Function

    End Class

End Namespace
