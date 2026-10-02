Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class OrderDetailPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _orders As New OrderService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                Dim o As Order = ResolveOrder()
                If o Is Nothing Then
                    Out.Text = WebUi.AlertBox("Order not found.") &
                               "<p class=""sub"" style=""margin-top:10px""><a href=""/App/Orders.aspx"">Back to My Orders</a></p>"
                    Return
                End If
                Dim isOwner As Boolean = o.UserId = STAR_DOM.Helpers.Session.CurrentUser.Id
                If Not isOwner AndAlso Not STAR_DOM.Helpers.Session.CanManageStore Then
                    Out.Text = WebUi.AlertBox("You don't have access to this order.")
                    Return
                End If

                ' Payment confirmation is a POST with password re-entry (plus the
                ' e-wallet reference for GCash/GOtyme) — never a plain GET link.
                If Guard.IsPost() AndAlso Request.Form("payOrder") = o.Id.ToString() Then
                    Dim r As ServiceResult = _orders.ConfirmPayment(o.OrderNumber, Request.Form("payRef"), Request.Form("payPassword"))
                    Session("flash_msg") = r.Message
                    Session("flash_ok") = r.Success
                    Response.Redirect("/App/OrderDetail.aspx?id=" & o.Id.ToString(), True)
                End If
                ' Customer side of the pick-up claim: "I have the order in hand".
                If Guard.IsPost() AndAlso Request.Form("pickupOrder") = o.Id.ToString() Then
                    Dim r2 As ServiceResult = _orders.ConfirmPickup(o.Id, True)
                    Session("flash_msg") = r2.Message
                    Session("flash_ok") = r2.Success
                    Response.Redirect("/App/OrderDetail.aspx?id=" & o.Id.ToString(), True)
                End If
                If Request.QueryString("cancel") = "1" Then
                    Dim r3 As ServiceResult = _orders.UpdateOrderState(o.Id, "CANCELLED")
                    Session("flash_msg") = r3.Message
                    Session("flash_ok") = r3.Success
                    Response.Redirect("/App/OrderDetail.aspx?id=" & o.Id.ToString(), True)
                End If

                Render(o, isOwner)
            Catch aborted As System.Threading.ThreadAbortException
                ' Response.Redirect(url, True) raises this by design once the redirect
                ' is already committed. Let it re-raise — the redirect must stand.
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the order: " & ex.Message)
            End Try
        End Sub

        Private Function ResolveOrder() As Order
            Dim idParam As String = Trim(CStr(Request.QueryString("id")))
            If idParam = "" Then idParam = Trim(CStr(Request.Form("id")))
            If idParam.StartsWith("SD-", StringComparison.OrdinalIgnoreCase) Then
                Return _orders.GetOrderByNumber(idParam)
            End If
            Dim id As Integer = 0
            Integer.TryParse(idParam, id)
            If id > 0 Then Return _orders.GetOrder(id)
            Return _orders.GetOrderByNumber(idParam)
        End Function

        Private Sub Render(o As Order, isOwner As Boolean)
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append("<a href=""/App/Orders.aspx"" class=""sub"" style=""display:inline-flex;align-items:center;gap:6px"">" & WebUi.Ic("arrow_back", "sm") & " Back to My Orders</a>")
            sb.Append(WebUi.Section(o.OrderNumber, "ORDER DETAIL",
                                    "Placed " & o.CreatedAt.ToString("MMMM d, yyyy h:mm tt") &
                                    " · Status " & o.Status.Replace("_", " ") & " · " &
                                    WebUi.Esc(DisplayPay(o.PaymentMethod)) & " payment " &
                                    o.PaymentStatus.Replace("_", " ")))

            sb.Append("<div class=""row"" style=""align-items:flex-start;gap:24px"">")

            ' tracking timeline
            sb.Append("<div class=""card"" style=""flex:1;min-width:300px"">")
            sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("local_shipping", "sm") & " Tracking</h3>")

            ' Courier status sentence — STAR:DOM ships J&T only. The site states where
            ' the parcel is; the live tracking happens on the J&T website.
            sb.Append("<div class=""card"" style=""background:var(--surface-low);margin-bottom:10px""><b>" & WebUi.Esc(o.DeliveryStatusLine) & "</b>")
            If o.TrackingUrl <> "" Then
                sb.Append(" <a href=""" & WebUi.Attr(o.TrackingUrl) & """ target=""_blank"" rel=""noopener"" style=""white-space:nowrap"">Track on J&T Express <span class=""ms sm"" style=""vertical-align:-3px"">open_in_new</span></a>")
            End If
            sb.Append("</div>")

            If o.IsPickup AndAlso o.PickupEventName <> "" Then
                sb.Append("<div class=""card"" style=""background:var(--surface-low);margin-bottom:10px"">")
                sb.Append("<span class=""ms sm"" style=""vertical-align:-3px;color:var(--primary)"">storefront</span> Claim at <b>" & WebUi.Esc(o.PickupEventName) & "</b>")
                If o.PickupHoursText <> "" Then sb.Append(" <span class=""sub"">· open " & WebUi.Esc(o.PickupHoursText) & "</span>")
                sb.Append("<div class=""sub"" style=""margin-top:6px"">Claim confirmations — Stall team: " &
                          If(o.PickupMerchantConfirmed, "<span class=""badge live"">confirmed</span>", "<span class=""badge warn"">waiting</span>") &
                          " &nbsp; You: " &
                          If(o.PickupCustomerConfirmed, "<span class=""badge live"">confirmed</span>", "<span class=""badge warn"">waiting</span>") &
                          "</div>")
                sb.Append("</div>")
            End If

            sb.Append("<ul class=""timeline"">")
            Dim steps As String()() = o.StatusTimeline
            For Each st As String() In steps
                Dim state As String = st(2)
                Dim cls As String = If(state = "DONE", " done", If(state = "NOW", " now", ""))
                sb.Append("<li class=""" & cls.Trim() & """><b>" & WebUi.Esc(st(0)) & "</b> — " & WebUi.Esc(st(1)) & "</li>")
            Next
            sb.Append("</ul>")
            If o.Status = "SHIPPED" AndAlso Not o.IsPickup Then
                sb.Append("<div class=""card"" style=""background:var(--surface-low);margin-top:8px""><b>Courier:</b> handed to J&T Express on " &
                          WebUi.Esc(o.UpdatedAt.ToString("MMM d, yyyy")) & " · expected within 3–5 business days.</div>")
            End If

            ' actions
            Dim method As String = If(o.PaymentMethod, "").Trim().ToUpperInvariant()
            Dim isEWallet As Boolean = PaymentSetting.IsEWallet(method)
            Dim canPay As Boolean = (o.PaymentStatus <> "PAID" AndAlso o.PaymentStatus <> "REFUNDED" AndAlso
                                     o.Status <> "CANCELLED" AndAlso isEWallet)
            Dim canConfirmPickup As Boolean = (isOwner AndAlso o.IsPickup AndAlso Not o.PickupCustomerConfirmed AndAlso
                                               o.Status <> "CANCELLED" AndAlso o.Status <> "DELIVERED")
            Dim canCancel As Boolean = (o.Status = "PENDING")
            If canPay OrElse canConfirmPickup OrElse canCancel Then
                sb.Append("<div style=""margin-top:16px;display:flex;flex-direction:column;gap:12px"">")
                If canPay Then
                    Dim brandName As String = WebUi.ChannelBrand(method)
                    Dim brandTitle As String = brandName & " Payment Verification"
                    Dim brandColor As String = WebUi.ChannelColor(method)
                    Dim phRef As String = If(method = "GCASH", "e.g. 1002 9482 1192", "e.g. GTYME-9482-1192")

                    sb.Append("<div class=""card"" style=""background:linear-gradient(180deg,#ffffff 0%,var(--surface-low) 100%);border:1.5px solid var(--line);border-radius:14px;padding:16px;box-shadow:var(--sh-1)"">")
                    sb.Append("<div style=""display:flex;align-items:center;justify-content:space-between;margin-bottom:12px"">")
                    sb.Append("<div style=""display:flex;align-items:center;gap:8px"">" & WebUi.PayLogo(method, 30) & "<b style=""font-size:13.5px;color:var(--ink)"">" & brandTitle & "</b></div>")
                    sb.Append("<span class=""badge warn"" style=""font-size:10px"">Payment Pending</span>")
                    sb.Append("</div>")
                    sb.Append("<p class=""sub"" style=""margin:0 0 14px;font-size:12px;line-height:1.4"">Enter the official reference number from your " & WebUi.Esc(brandName) & " receipt and your account password to confirm payment.</p>")
                    ' Reopen the Scan to Pay popup — the order is already placed here, so
                    ' the customer may still need the QR or the account number.
                    sb.Append("<button type=""button"" class=""btn ghost sm"" onclick=""window.openQrPayModal && window.openQrPayModal()"" style=""margin-bottom:14px""><span class=""ms sm"">qr_code_2</span><span>Scan to Pay / show QR again</span></button>")

                    sb.Append("<form method=""post"" action=""/App/OrderDetail.aspx"" style=""display:flex;flex-direction:column;gap:12px"">")
                    sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                    sb.Append("<input type=""hidden"" name=""id"" value=""" & o.Id.ToString() & """>")
                    sb.Append("<input type=""hidden"" name=""payOrder"" value=""" & o.Id.ToString() & """>")

                    sb.Append("<div style=""display:grid;grid-template-columns:repeat(auto-fit, minmax(200px, 1fr));gap:10px"">")
                    ' Reference Input
                    sb.Append("<div><label style=""display:block;font-size:11px;font-weight:700;color:var(--ink-soft);text-transform:uppercase;letter-spacing:.05em;margin-bottom:4px"">" & WebUi.Esc(brandName) & " Reference No. *</label>")
                    sb.Append("<div style=""position:relative""><input name=""payRef"" placeholder=""" & phRef & """ required style=""width:100%;box-sizing:border-box;padding:10px 12px 10px 34px;border:1.5px solid var(--line);border-radius:10px;font-size:13px;font-weight:600;background:var(--surface);color:var(--ink);outline:none""><span class=""ms sm"" style=""position:absolute;left:10px;top:50%;transform:translateY(-50%);color:var(--ink-soft);font-size:16px"">receipt</span></div></div>")

                    ' Password Input
                    sb.Append("<div><label style=""display:block;font-size:11px;font-weight:700;color:var(--ink-soft);text-transform:uppercase;letter-spacing:.05em;margin-bottom:4px"">Account Password *</label>")
                    sb.Append("<div style=""position:relative""><input type=""password"" name=""payPassword"" placeholder=""Re-enter password"" required autocomplete=""current-password"" style=""width:100%;box-sizing:border-box;padding:10px 12px 10px 34px;border:1.5px solid var(--line);border-radius:10px;font-size:13px;font-weight:600;background:var(--surface);color:var(--ink);outline:none""><span class=""ms sm"" style=""position:absolute;left:10px;top:50%;transform:translateY(-50%);color:var(--ink-soft);font-size:16px"">lock</span></div></div>")
                    sb.Append("</div>")

                    sb.Append("<div style=""display:flex;align-items:center;justify-content:space-between;margin-top:4px;gap:8px;flex-wrap:wrap"">")
                    sb.Append("<span style=""font-size:11.5px;color:var(--ink-soft)"">Security verification for fast merchant approval</span>")
                    sb.Append("<button class=""btn primary"" type=""submit"" style=""padding:10px 20px;border-radius:10px;font-weight:700;display:inline-flex;align-items:center;gap:8px;box-shadow:var(--sh-1)""><span class=""ic ms"">check_circle</span><span>Confirm &amp; I've Paid</span></button>")
                    sb.Append("</div>")
                    sb.Append("</form></div>")
                End If

                If canConfirmPickup Then
                    sb.Append("<form method=""post"" action=""/App/OrderDetail.aspx"" style=""display:inline-flex"">")
                    sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                    sb.Append("<input type=""hidden"" name=""id"" value=""" & o.Id.ToString() & """>")
                    sb.Append("<input type=""hidden"" name=""pickupOrder"" value=""" & o.Id.ToString() & """>")
                    sb.Append("<button class=""btn primary"" type=""submit"" data-confirm=""Confirm you have received this order at the stall?""><span class=""ic ms"">task_alt</span><span>Confirm order received</span></button>")
                    sb.Append("</form>")
                End If

                If canCancel Then
                    sb.Append("<div><a class=""btn danger"" href=""/App/OrderDetail.aspx?id=" & o.Id.ToString() &
                              "&cancel=1"" data-confirm=""Cancel this order?"" data-confirm-danger"" style=""border-radius:10px;padding:8px 16px""><span class=""ic ms"">cancel</span><span>Cancel Order</span></a></div>")
                End If
                sb.Append("</div>")
            End If
            sb.Append("</div>")

            ' details + items
            sb.Append("<div style=""flex:1.6;min-width:320px"">")
            sb.Append("<div class=""card mb""><div class=""kv"">")
            If o.IsPickup Then
                sb.Append("<dt>Claim at</dt><dd>" & WebUi.Esc(o.PickupEventName) &
                          If(o.PickupHoursText <> "", " <span class=""sub"">· open " & WebUi.Esc(o.PickupHoursText) & "</span>", "") & "</dd>")
            Else
                sb.Append("<dt>Ship to</dt><dd>" & WebUi.Esc(o.ShippingAddress) & "</dd>")
            End If
            sb.Append("<dt>Phone</dt><dd>" & WebUi.Esc(o.ContactPhone) & "</dd>")
            sb.Append("<dt>Payment</dt><dd>" & WebUi.Esc(DisplayPay(o.PaymentMethod)) & "</dd>")
            sb.Append("<dt>Pay status</dt><dd>" & WebUi.Badge(o.PaymentStatus) & "</dd>")
            sb.Append("<dt>Delivery</dt><dd>" & If(o.IsPickup, "Pick-up at stall", "J&T Express delivery") & "</dd>")
            If o.DiscountAmount > 0D Then
                sb.Append("<dt>Bundle savings</dt><dd style=""color:#15803d;font-weight:700"">−" & WebUi.Money(o.DiscountAmount) & "</dd>")
            End If
            ' Before the fee is quoted, say so rather than silently omitting the line: a total
            ' that excludes shipping must not look like the final amount.
            If o.IsPickup Then
                sb.Append("<dt>Shipping fee</dt><dd>None — collected at the stall</dd>")
            ElseIf o.ShippingFeeConfirmed Then
                If o.ShippingFee > 0D Then
                    sb.Append("<dt>Shipping fee</dt><dd>" & WebUi.Money(o.ShippingFee) & "</dd>")
                Else
                    sb.Append("<dt>Shipping fee</dt><dd>Free</dd>")
                End If
            Else
                sb.Append("<dt>Shipping fee</dt><dd><span class=""badge warn"">to be quoted</span> " &
                          "<span class=""sub"" style=""font-size:11.5px"">we'll send your final total when we confirm the order</span></dd>")
            End If
            If o.Notes <> "" Then sb.Append("<dt>Notes</dt><dd>" & WebUi.Esc(o.Notes) & "</dd>")
            sb.Append("</div></div>")

            Dim items As List(Of OrderItem) = _orders.GetOrderItems(o.Id)
            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"PRODUCT", "PRICE", "QTY", "TOTAL"}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            For Each it As OrderItem In items
                sb.Append("<tr>")
                sb.Append("<td><div style=""display:flex;gap:10px;align-items:center"">" &
                          WebUi.ProductImg(it.ImageFile, it.ProductId, it.ProductName, "width:44px;height:44px;border-radius:9px;flex-shrink:0") &
                          "<a href=""/App/Product.aspx?id=" & it.ProductId.ToString() & """ style=""font-weight:700"">" &
                          WebUi.Esc(it.ProductName) & "</a></div></td>")
                sb.Append("<td>" & WebUi.Money(it.UnitPrice) & "</td>")
                sb.Append("<td>" & it.Quantity.ToString() & "</td>")
                sb.Append("<td>" & WebUi.Money(it.LineTotal) & "</td>")
                sb.Append("</tr>")
            Next
            sb.Append("</tbody></table></div>")
            ' Label the figure by what it actually is: a confirmed total includes shipping, an
            ' unquoted one does not.
            Dim totalLabel As String = If(o.HasFinalTotal, "Order total: ", "Goods total (shipping to be quoted): ")
            sb.Append("<div style=""text-align:right;padding:6px 14px""><b>" & totalLabel & "</b>" & WebUi.Money(o.TotalAmount) & "</div>")

            ' payments ledger
            Dim pays As List(Of Payment) = _orders.PaymentsForOrder(o.Id)
            If pays.Count > 0 Then
                sb.Append("<h3 style=""margin:14px 0 6px"">" & WebUi.Ic("receipt_long", "sm") & " Payments</h3>")
                For Each p As Payment In pays
                    Dim ptr As Receipt = If(o.Status <> "CANCELLED", _orders.ReceiptForPaymentId(p.Id), Nothing)
                    sb.Append("<div class=""card"" style=""margin-bottom:8px"">" & WebUi.Esc(p.DisplayMethod) & " · " &
                              WebUi.Money(p.Amount) & " · " & WebUi.Esc(p.Status) & " · Ref " & WebUi.Esc(p.ReferenceNumber) &
                              If(p.PaidAt.HasValue, " · " & p.PaidAt.Value.ToString("MMM d, yyyy h:mm tt"), "") &
                              If(ptr IsNot Nothing, " &nbsp;<a href=""/App/Receipt.aspx?r=" & WebUi.Esc(ptr.ReceiptNumber) & """><span class=""ms sm"">receipt_long</span> Official Receipt</a>", "") & "</div>")
                Next
            End If
            ' bottom actions with Back Button
            sb.Append("<div style=""margin-top:20px;padding-top:16px;border-top:1px solid var(--line);display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px"">")
            sb.Append("<a href=""/App/Orders.aspx"" class=""btn ghost"" style=""border-radius:10px;padding:8px 18px"">" & WebUi.Ic("arrow_back", "sm") & " Back to My Orders</a>")
            sb.Append("<a href=""/App/Marketplace.aspx"" class=""btn ghost"" style=""border-radius:10px;padding:8px 18px"">" & WebUi.Ic("storefront", "sm") & " Continue Shopping</a>")
            sb.Append("</div>")
            sb.Append("</div></div>")

            ' Scan to Pay popup for an e-wallet order. Shared with Checkout via
            ' WebUi so both look identical; the order already exists here, so the
            ' primary button only closes the popup and focuses the reference field.
            ' The customer reopens it with the "Scan to Pay" button in the card above.
            If isEWallet AndAlso o.PaymentStatus <> "PAID" AndAlso o.PaymentStatus <> "REFUNDED" AndAlso o.Status <> "CANCELLED" Then
                Dim ps As PaymentSetting = New PaymentSettingRepository().GetByChannel(method)
                sb.Append(WebUi.QrPaymentModal(ps, method, o.TotalAmount, False, False))
            End If

            Out.Text = sb.ToString()
        End Sub


        Private Function DisplayPay(pm As String) As String
            Return WebUi.ChannelBrand(pm)
        End Function

    End Class

End Namespace
