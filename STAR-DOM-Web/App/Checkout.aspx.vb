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

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                If Guard.IsPost() Then
                    PlaceOrder()
                    Return
                End If
                RenderForm()
            Catch aborted As System.Threading.ThreadAbortException
                ' Response.Redirect(url, True) raises this by design once the redirect
                ' is already committed. Let it re-raise — the redirect must stand.
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Checkout failed: " & ex.Message)
            End Try
        End Sub

        Private Sub PlaceOrder()
            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then
                Session("flash_msg") = "Your cart is empty."
                Session("flash_ok") = False
                Response.Redirect("/App/Cart.aspx", True)
            End If

            ' Convert.ToString(String) hands back Nothing for a missing field — and a
            ' disabled input (the address while pick-up is selected) is never posted.
            ' Coerce everything to "" so the insert never sees DBNull.
            Dim pm As String = If(Request.Form("pm"), "")
            If pm = "" Then pm = "COD"
            Dim addr As String = If(Request.Form("address"), "")
            Dim phone As String = If(Request.Form("phone"), "")
            Dim notes As String = If(Request.Form("notes"), "")
            Dim fulfillment As String = If(Request.Form("fulfillment"), "")
            If fulfillment = "" Then fulfillment = "DELIVERY"
            Dim pickupId As Integer = 0
            Integer.TryParse(Convert.ToString(Request.Form("pickupEvent")), pickupId)

            Dim result As ServiceResult = _orders.Checkout(pm, addr, phone, notes, Nothing, "PENDING", fulfillment,
                                                           If(pickupId > 0, pickupId, Nothing))
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
                Session("flash_msg") = result.Message
                Session("flash_ok") = False
                Response.Redirect("/App/Checkout.aspx", True)
            End If
        End Sub

        Private Sub RenderForm()
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

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
            sb.Append("<input type=""radio"" name=""fulfillment"" value=""DELIVERY"" checked style=""margin-top:3px"">")
            sb.Append("<span class=""ms"" style=""color:var(--primary)"">local_shipping</span>")
            sb.Append("<span><b>Delivery</b><br><span class=""sub"" style=""font-size:12px"">Ships nationwide via J&T Express · " &
                      If(deliveryFee > 0D, WebUi.Esc(Fmt.PHP(deliveryFee)) & " shipping (free over ₱1,500)", "Free shipping") & "</span></span></label>")
            sb.Append("<label class=""card"" style=""display:flex;gap:12px;align-items:flex-start;margin-bottom:8px;cursor:pointer"">")
            sb.Append("<input type=""radio"" name=""fulfillment"" value=""PICKUP"" style=""margin-top:3px"">")
            sb.Append("<span class=""ms"" style=""color:var(--primary)"">storefront</span>")
            sb.Append("<span><b>Pick-up at a pop-up stall</b><br><span class=""sub"" style=""font-size:12px"">No shipping fee · claim in person during the stall's open hours</span></span></label>")

            ' delivery-only fields
            sb.Append("<div id=""deliveryFields"">")
            sb.Append("<div class=""field""><label for=""ad"">Shipping address</label><input id=""ad"" name=""address"" required placeholder=""House / street, city, province""></div>")
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
                sb.Append("<option value=""" & ev.Id.ToString() & """>" & WebUi.Attr(ev.Name & " — " & whenText) & "</option>")
            Next
            sb.Append("</select>")
            sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-top:6px"">Hours are shown per stall. Your claim is final when <b>both you and the stall team</b> confirm the hand-over.</div>")
            sb.Append("</div></div>")

            ' shared fields
            sb.Append("<div class=""field""><label for=""ph"">Contact phone</label><input id=""ph"" name=""phone"" required placeholder=""09xx xxx xxxx""></div>")
            sb.Append("<div class=""field""><label for=""nt"">Order notes (optional)</label><textarea id=""nt"" name=""notes"" style=""min-height:70px""></textarea></div>")
            sb.Append("</div>")

            ' payment
            sb.Append("<div class=""card""><h3 style=""margin-bottom:10px"">" & WebUi.Ic("payments", "sm") & " Payment method</h3>")
            sb.Append(PayOption("GCASH", "GCash", "Pay instantly via the GCash app QR", "qr_code_2"))
            sb.Append(PayOption("MAYA", "Maya", "Pay with the Maya app", "account_balance_wallet"))
            sb.Append(PayOption("COD", "Cash on Delivery / Claim", "Pay cash when your order arrives or at pick-up", "local_shipping"))
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
            sb.Append("var addr=document.getElementById('ad');")
            sb.Append("var ship=document.getElementById('shipCell');")
            sb.Append("var tot=document.getElementById('totalCell');")
            sb.Append("function money(n){return '₱'+n.toLocaleString('en-PH',{minimumFractionDigits:2,maximumFractionDigits:2});}")
            sb.Append("function isPickup(){for(var i=0;i<rads.length;i++){if(rads[i].checked&&rads[i].value==='PICKUP')return true;}return false;}")
            sb.Append("function refresh(){var p=isPickup();")
            sb.Append("del.style.display=p?'none':'';pick.style.display=p?'':'none';")
            sb.Append("addr.disabled=p;addr.required=!p;")
            sb.Append("var f=p?0:fee;")
            sb.Append("ship.innerHTML=f>0?money(f):'<span style=""color:#15803d;font-weight:700"">FREE</span>';")
            sb.Append("tot.innerHTML=money(Math.max(sub-disc+f,0));}")
            sb.Append("for(var i=0;i<rads.length;i++){rads[i].addEventListener('change',refresh);}")
            sb.Append("refresh();})();")
            sb.Append("</" & "script>")

            Out.Text = sb.ToString()
        End Sub

        Private Function PayOption(value As String, title As String, hint As String, icon As String) As String
            Dim checkedAttr As String = If(value = "COD", " checked", "")
            Return "<label class=""card"" style=""display:flex;gap:12px;align-items:flex-start;margin-bottom:8px;cursor:pointer"">" &
                   "<input type=""radio"" name=""pm"" value=""" & value & """" & checkedAttr & " style=""margin-top:3px"">" &
                   "<span class=""ms"" style=""color:var(--primary)"">" & WebUi.Esc(icon) & "</span>" &
                   "<span><b>" & WebUi.Esc(title) & "</b><br><span class=""sub"" style=""font-size:12px"">" & WebUi.Esc(hint) & "</span></span></label>"
        End Function

    End Class

End Namespace
