Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories
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
            ''' <summary>
            ''' Parked method. The studio prices the order and returns it with the J&amp;T
            ''' fee before any money moves, so the channel is chosen on the order page,
            ''' not here — see Checkout's "How payment works" card.
            ''' </summary>
            Public PaymentMethod As String = "PENDING"
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
                    PlaceOrder(d, Posted("scanConfirmed") <> "")
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
            ' Convert.ToString(String) hands back Nothing for a missing field, so coerce
            ' everything to "" before trimming — nothing downstream sees a null.
            d.Street = Posted("addrStreet")
            d.Barangay = Posted("addrBarangay")
            d.City = Posted("addrCity")
            d.Province = Posted("addrProvince")
            d.Zip = Posted("addrZip")
            d.Landmark = Posted("addrLandmark")
            d.Phone = Posted("phone")
            d.Notes = Posted("notes")
            d.PaymentMethod = Posted("pm")
            If d.PaymentMethod = "" Then d.PaymentMethod = "PENDING"
            Return d
        End Function

        ''' <summary>One posted value, trimmed, or "" when the field was not sent.</summary>
        ''' <remarks>
        ''' Convert.ToString hands back Nothing for a missing field, and a disabled
        ''' input is never posted
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
            ' Fully qualified: this page imports System.Web.UI, which brings its own
            ' Validation.Validators into scope and would win the bare name.
            Dim problem As String

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

        Private Sub PlaceOrder(d As CheckoutDraft, scanConfirmed As Boolean)
            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then
                Session("flash_msg") = "Your cart is empty."
                Session("flash_ok") = False
                Response.Redirect("/App/Cart.aspx", True)
            End If

            ' No QR / scan-confirm round-trip any more: checkout records the order and
            ' stops there. Payment happens later, on the order page, once the
            ' studio has returned the final price and shipping fee. PaymentMethod
            ' is parked as PENDING because the column is NOT NULL and the real
            ' channel is chosen at payment time.
            Dim addr As String = ComposeAddress(d)
            Dim result As ServiceResult = _orders.Checkout("PENDING", addr, d.Phone, d.Notes, Nothing, "PENDING")
            If result.Success Then
                ' find the freshest order to deep-link into
                Dim fresh As Order = _orders.ListMyOrders().OrderByDescending(Function(o) o.Id).FirstOrDefault()
                Session("flash_msg") = "Order placed. The studio will return it with the final price and shipping fee — " &
                                  "you can pay once that is confirmed."
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

        Private Sub RenderForm(d As CheckoutDraft, problem As String, Optional showQrModal As Boolean = False)
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
            Dim pmSel As String = "PENDING"
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
            End If

            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then
                sb.Append("<div class=""empty"">Nothing to check out. <a href=""/App/Catalog.aspx"" style=""color:var(--primary);font-weight:700;display:inline-flex;align-items:center;gap:4px"">Browse products <span class=""ms sm"">arrow_forward</span></a></div>")
                Out.Text = sb.ToString()
                Return
            End If

            sb.Append(WebUi.Section("Checkout", "SECURE ORDER",
                                    "Enter your delivery address and how to pay. Payments are simulated for this demo — no real charge is made."))

            Dim subtotal As Decimal = items.Sum(Function(i) i.LineTotal)
            Dim bundleDisc As Decimal = _cart.BundleDiscount(items)
            Dim bundleNote As String = _cart.BundleNote()

            ' The courier fee is not knowable here: J&T only quotes once the parcel is
            ' weighed. Checkout therefore shows the goods total, and the store quotes the
            ' real shipping fee when confirming the order.
            Dim goodsTotal As Decimal = Math.Max(subtotal - bundleDisc, 0D)

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
            sb.Append("<div class=""row space-between""><b>Shipping</b><span id=""shipCell"" class=""sub"">Quoted after confirmation</span></div>")
            sb.Append("<div class=""row space-between"" style=""margin-top:6px""><b>Total</b><b id=""totalCell"" style=""color:var(--primary);font-size:18px"">" & WebUi.Money(goodsTotal) & "</b></div>")
            sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-top:6px"">Shipping is not included yet — we quote the J&amp;T Express fee when we confirm your order and send you the final total before it ships.</div>")
            If bundleNote <> "" Then
                sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-top:8px""><span class=""ms sm"" style=""vertical-align:-3px;color:var(--primary)"">sell</span> " & WebUi.Esc(bundleNote) & "</div>")
            End If
            sb.Append("</div>")

            ' Delivery only. STAR:DOM ships nationwide; there is no pick-up option.
            sb.Append("<div class=""card mb""><h3 style=""margin-bottom:10px"">" & WebUi.Ic("local_shipping", "sm") & " Delivery</h3>")
            sb.Append("<div class=""sub"" style=""font-size:12px;margin:0 0 12px"">Ships nationwide via J&amp;T Express · fee quoted when we confirm your order.</div>")
            ' Zone guide, so the eventual quote is never a surprise. Indicative only — the
            ' figure actually charged follows the parcel's weight when it is quoted.
            sb.Append("<div class=""sub"" style=""font-size:11.5px;margin:2px 0 10px;padding:9px 11px;background:var(--surface-low);border-radius:8px;border-left:3px solid var(--primary)"">")
            sb.Append("<b style=""color:var(--ink)"">Typical J&amp;T zone rates:</b> Luzon ₱0–100 · Visayas ₱101–200 · Mindanao ₱201–300. " &
                      "The exact fee follows the parcel's weight and is confirmed before dispatch.")
            sb.Append("</div>")
            ' delivery address. There is no pick-up alternative any more, so these
            ' inputs are always live and always required -- no radio toggling them.
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
            ' shared fields
            sb.Append("<div class=""field""><label for=""ph"">Contact phone</label><input id=""ph"" name=""phone"" required placeholder=""09xx xxx xxxx"" value=""" & WebUi.Attr(phone) & """></div>")
            sb.Append("<div class=""field""><label for=""nt"">Order notes (optional)</label><textarea id=""nt"" name=""notes"" style=""min-height:70px"">" & WebUi.Esc(notes) & "</textarea></div>")
            sb.Append("</div>")

            ' No payment method here. The studio prices the order and returns it with
            ' the shipping fee before the customer can pay, so offering GCash /
            ' GOtyme at this point would collect a figure that is not the
            ' final one. The customer picks a channel on the order page once the
            ' studio has quoted it.
            Dim paySettings As New PaymentSettingRepository()
            sb.Append("<div class=""card""><h3 style=""margin-bottom:10px"">" & WebUi.Ic("info", "sm") & " How payment works</h3>")
            sb.Append("<p class=""sub"" style=""margin:0 0 8px"">Place the order now with your delivery address. " &
                      "The studio then returns it to you with the <b>final price and shipping fee</b>, and only " &
                      "then do the payment methods open here.</p>")
            sb.Append(WebUi.NoCancelNote("an order is placed"))
            sb.Append("<div class=""frow"">")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">lock</span><span>Place Order</span></button>")
            sb.Append("<button type=""button"" class=""btn ghost"" id=""btnCancelCheckout""><span class=""ic ms"">arrow_back</span><span>Back to Cart</span></button>")
            sb.Append("</div>")
            sb.Append("</div>")

            sb.Append("</form>")
            sb.Append("</div>")

            ' Exit Confirmation Modal
            sb.Append("<div class=""modal-backdrop"" id=""exitCheckoutModal"" aria-hidden=""true"">")
            sb.Append("<div class=""modal-card"" role=""dialog"" aria-modal=""true"" style=""max-width:440px;text-align:center"">")
            sb.Append("<div class=""modal-ic"" style=""margin:0 auto 14px;background:rgba(254,208,27,0.2);color:var(--ink)""><span class=""ms"">shopping_cart_checkout</span></div>")
            sb.Append("<h3 style=""margin-bottom:8px"">Leave Checkout?</h3>")
            sb.Append("<p class=""modal-msg"" style=""margin-bottom:20px"">Your cart items and saved choices will remain completely safe in your cart. You can come back and complete your order anytime.</p>")
            sb.Append("<div class=""modal-actions"" style=""justify-content:center;gap:12px"">")
            sb.Append("<button type=""button"" class=""btn ghost"" id=""btnStayCheckout"" style=""min-width:110px"">Stay Here</button>")
            sb.Append("<a href=""/App/Cart.aspx"" class=""btn primary"" style=""min-width:130px;background:var(--primary)"">Return to Cart</a>")
            sb.Append("</div>")
            sb.Append("</div></div>")

            ' Exit-confirmation modal
            sb.Append("<script>")
            sb.Append("(function(){")
            sb.Append("var modal=document.getElementById('exitCheckoutModal');")
            sb.Append("var btnOpen=document.getElementById('btnCancelCheckout');")
            sb.Append("var btnStay=document.getElementById('btnStayCheckout');")
            sb.Append("function openModal(e){if(e)e.preventDefault();modal.classList.add('open');modal.setAttribute('aria-hidden','false');}")
            sb.Append("function closeModal(){modal.classList.remove('open');modal.setAttribute('aria-hidden','true');}")
            sb.Append("if(btnOpen)btnOpen.addEventListener('click',openModal);")
            sb.Append("if(btnStay)btnStay.addEventListener('click',closeModal);")
            sb.Append("if(modal)modal.addEventListener('click',function(e){if(e.target===modal)closeModal();});")
            sb.Append("document.addEventListener('keydown',function(e){if(modal&&modal.classList.contains('open')&&e.key==='Escape')closeModal();});")
            sb.Append("})();")
            sb.Append("</" & "script>")

            Out.Text = sb.ToString()
        End Sub

        Private Function PayOption(value As String, title As String, hint As String, icon As String,
                                        selected As String) As String
            Dim checkedAttr As String = If(value = selected, " checked", "")
            ' The wallet's own logo when we have one, the Material icon otherwise.
            Dim mark As String = WebUi.PayLogo(value, 34)
            If mark = "" Then
                mark = "<span class=""ms"" style=""color:var(--primary);font-size:24px;line-height:1"">" & WebUi.Esc(icon) & "</span>"
            End If
            Return "<label class=""card"" style=""display:flex;gap:12px;align-items:center;margin-bottom:8px;cursor:pointer"">" &
                   "<input type=""radio"" name=""pm"" value=""" & value & """" & checkedAttr & " style=""margin-top:3px"">" &
                   mark &
                   "<span><b>" & WebUi.Esc(title) & "</b><br><span class=""sub"" style=""font-size:12px"">" & WebUi.Esc(hint) & "</span></span></label>"
        End Function

    End Class

End Namespace
