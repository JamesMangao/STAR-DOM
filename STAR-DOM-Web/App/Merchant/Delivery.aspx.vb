Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Per-order delivery page for the studio: record what the courier said,
    ''' attach the proof of delivery, close the order on the customer's behalf, or
    ''' resolve a dispute.
    ''' </summary>
    ''' <remarks>
    ''' This is a page of its own rather than more panels on Orders.aspx for one
    ''' reason: the proof-of-delivery upload needs enctype="multipart/form-data",
    ''' and Orders.aspx lives inside Site.master's single shell form, which an HTML
    ''' parser closes at the first nested form tag. A file input inside that shell
    ''' uploads nothing. The orders list links here for any order that has reached
    ''' DELIVERED, or that is under dispute.
    '''
    ''' Every action on this page writes to OrderStatusHistory, so the answers to
    ''' "who closed this order and why" live with the order rather than in a
    ''' support inbox.
    ''' </remarks>
    Public Class MerchantDeliveryPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _orders As New OrderService()
        ''' <summary>Read-only here: this page shows where the order's earning stands.</summary>
        Private ReadOnly _ledger As New CommissionLedgerService()
        Private _orderId As Integer = 0
        Private _error As String = ""
        Private _notice As String = ""

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                Integer.TryParse(Request.QueryString("id"), _orderId)
                Dim o As Order = If(_orderId > 0, _orders.GetOrder(_orderId), Nothing)
                If o Is Nothing Then
                    Out.Text = WebUi.AlertBox("Order not found.") &
                               "<p class=""sub"" style=""margin-top:10px""><a href=""/App/Merchant/Orders.aspx"">Back to Orders</a></p>"
                    Return
                End If

                If Guard.IsPost() Then
                    Act(o)
                    ' Re-read rather than re-render from the stale copy: every action
                    ' here changes the order, and the page shows the new state.
                    o = _orders.GetOrder(_orderId)
                End If

                Render(o)
            Catch aborted As System.Threading.ThreadAbortException
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the delivery page: " & ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Runs one posted action. Each branch posts back to this page with its own
        ''' discriminator, because the shell form publishes every form on the page at
        ''' once -- the same trap the orders list documents in detail.
        ''' </summary>
        Private Sub Act(o As Order)
            Select Case Convert.ToString(Request.Form("dact"))
                Case "pod"
                    Dim img As Byte() = FirstUploadedImage()
                    Dim saved As ServiceResult = _orders.SaveProofOfDelivery(
                        o.Id, img, Convert.ToString(Request.Form("podMime")),
                        Convert.ToString(Request.Form("podRef")),
                        Convert.ToString(Request.Form("courierStatus")))
                    If saved.Success Then
                        _notice = "Proof of delivery saved. The order is not closed by this -- the customer still " &
                                  "confirms it, unless you record a dispute outcome below."
                    Else
                        _error = saved.Message
                    End If

                Case "received"
                    Dim r As ServiceResult = _orders.StudioMarkReceived(o.Id,
                                                                       Convert.ToString(Request.Form("studioReason")),
                                                                       Convert.ToString(Request.Form("studioIgRef")),
                                                                       Convert.ToString(Request.Form("studioPassword")))
                    If r.Success Then
                        _notice = r.Message & " The reason is kept on the order and is visible to the customer."
                    Else
                        _error = r.Message
                    End If

                Case "resolve"
                    ' A RESHIP goes back out as SHIPPED and needs a NEW waybill, so the
                    ' tracking box travels with the outcome buttons. The service refuses
                    ' a reship with no number; an empty box on "it did arrive" or
                    ' "refund it" is simply ignored.
                    Dim rr As ServiceResult = _orders.ResolveDispute(o.Id,
                                                                     Convert.ToString(Request.Form("resolution")),
                                                                     Convert.ToString(Request.Form("resolveNote")),
                                                                     Convert.ToString(Request.Form("resolveIgRef")),
                                                                     Convert.ToString(Request.Form("resolveTracking")))
                    If rr.Success Then
                        _notice = rr.Message
                    Else
                        _error = rr.Message
                    End If

                Case "refund"
                    Dim rr2 As ServiceResult = _orders.RecordRefund(o.Id,
                                                                    Convert.ToString(Request.Form("refundReason")),
                                                                    Convert.ToString(Request.Form("refundIgRef")),
                                                                    Convert.ToString(Request.Form("refundPassword")))
                    If rr2.Success Then
                        _notice = rr2.Message
                    Else
                        _error = rr2.Message
                    End If
            End Select
        End Sub

        ''' <summary>
        ''' The first image actually posted to the POD field, or Nothing.
        ''' </summary>
        ''' <remarks>
        ''' Returning Nothing rather than an empty array matters: the service treats
        ''' those differently, and an empty array would read as "an image was
        ''' attached but is blank".
        ''' </remarks>
        Private Function FirstUploadedImage() As Byte()
            If Request.Files Is Nothing Then Return Nothing
            For i As Integer = 0 To Request.Files.Count - 1
                Dim f As System.Web.HttpPostedFile = Request.Files(i)
                If f Is Nothing OrElse f.ContentLength = 0 OrElse String.IsNullOrWhiteSpace(f.FileName) Then Continue For
                Using ms As New System.IO.MemoryStream()
                    f.InputStream.CopyTo(ms)
                    Return ms.ToArray()
                End Using
            Next
            Return Nothing
        End Function

        Private Sub Render(o As Order)
            Dim sb As New StringBuilder()
            sb.Append("<a href=""/App/Merchant/Orders.aspx"" class=""sub"">&larr; Orders</a>")
            sb.Append(WebUi.Section("Delivery — " & o.OrderNumber, "MERCHANT STUDIO / DELIVERY",
                                    "Check the courier's own tracking page and record what it says. There is no J&T " &
                                    "integration, so nothing here is inferred — it is all typed in from the courier's site."))

            If _error <> "" Then sb.Append(WebUi.AlertBox(_error))
            If _notice <> "" Then sb.Append(WebUi.AlertBox(_notice, "ok"))

            sb.Append("<div class=""kv card mb"">")
            sb.Append("<dt>Status</dt><dd>" & WebUi.Badge(o.Status) & "</dd>")
            sb.Append("<dt>Customer</dt><dd>" & WebUi.Esc(o.CustomerName) & "</dd>")
            If Not String.IsNullOrWhiteSpace(o.TrackingNumber) Then
                sb.Append("<dt>J&amp;T tracking</dt><dd><a href=""" & WebUi.Attr(o.TrackingUrl) & """ target=""_blank"" rel=""noopener"">" &
                          WebUi.Esc(o.TrackingNumber) & "</a></dd>")
            End If
            If Not String.IsNullOrWhiteSpace(o.CourierStatus) Then
                sb.Append("<dt>Courier says</dt><dd>" & WebUi.Esc(o.CourierStatus) & "</dd>")
            End If
            If Not String.IsNullOrWhiteSpace(o.PodReference) Then
                sb.Append("<dt>POD reference</dt><dd>" & WebUi.Esc(o.PodReference) & "</dd>")
            End If
            If o.PodCapturedAt.HasValue Then
                sb.Append("<dt>POD captured</dt><dd>" & Fmt.DateTimeF(o.PodCapturedAt.Value) &
                          If(Not String.IsNullOrWhiteSpace(o.PodCapturedBy), " by " & WebUi.Esc(o.PodCapturedBy), "") & "</dd>")
            End If
            sb.Append("</div>")

            If o.IsHeld Then sb.Append(HeldPanel(o))
            sb.Append(PodPanel(o))
            sb.Append(EarningsPanel(o))

            ' The stored image is fetched only here, on the one request that shows it.
            If o.HasPod Then
                Dim bytes As Byte() = _orders.PodImage(o.Id)
                If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                    Dim mime As String = If(String.IsNullOrWhiteSpace(o.PodMime), "image/png", o.PodMime)
                    sb.Append("<div class=""card mb""><b style=""display:block;margin-bottom:6px"">Proof on file</b>")
                    sb.Append("<img src=""data:" & WebUi.Attr(mime) & ";base64," & Convert.ToBase64String(bytes) &
                              """ alt=""Proof of delivery"" style=""max-width:100%;border-radius:10px;border:1px solid var(--line)"">")
                    sb.Append("</div>")
                End If
            End If

            If o.Status = OrderStatuses.Delivered Then sb.Append(ClosePanel(o))
            ' A refund is offered while there is money to give back -- not, as it was,
            ' only on an order that has ALREADY been refunded (where RecordRefund
            ' refuses it and the panel was therefore unreachable for ever).
            If CanRefund(o) Then sb.Append(RefundPanel(o))
            sb.Append(TrailPanel(o))

            Out.Text = sb.ToString()
        End Sub

        ''' <summary>
        ''' The dispute panel. Only rendered while the order is actually held, so
        ''' the studio never sees "resolve" on an order with nothing to resolve.
        ''' </summary>
        Private Function HeldPanel(o As Order) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card mb"" style=""border-color:#eec200;background:var(--yellow-soft)"">")
            sb.Append("<b style=""display:block;margin-bottom:4px"">The customer disputes this delivery</b>")
            If Not String.IsNullOrWhiteSpace(o.HoldReason) Then
                sb.Append("<p class=""sub"" style=""margin:0 0 8px;color:var(--ink)"">Their words: <b>" &
                          WebUi.Esc(o.HoldReason) & "</b></p>")
            End If
            sb.Append("<p class=""sub"" style=""margin:0 0 10px;color:var(--ink)"">Check with J&amp;T Express first, then " &
                      "record what you found. The customer is notified of the outcome.</p>")

            sb.Append("<form method=""post"" action=""/App/Merchant/Delivery.aspx?id=" & o.Id.ToString() & """>")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<input type=""hidden"" name=""dact"" value=""resolve"">")
            sb.Append("<div style=""display:flex;gap:8px;flex-wrap:wrap;margin-bottom:8px"">")
            sb.Append("<button class=""btn primary"" type=""submit"" name=""resolution"" value=""RECEIVED"">It did arrive</button>")
            sb.Append("<button class=""btn"" type=""submit"" name=""resolution"" value=""RESHIP"">Send it again</button>")
            sb.Append("<button class=""btn danger"" type=""submit"" name=""resolution"" value=""REFUNDED"">Refund it</button>")
            sb.Append("</div>")
            sb.Append("<textarea name=""resolveNote"" rows=""2"" " &
                      "style=""width:100%;padding:10px;border-radius:10px;border:1px solid var(--line);font:inherit"" " &
                      "placeholder=""What did J&amp;T say?""></textarea>")
            ' Only a reship needs this, so it is not `required`: a required box in the
            ' same form would veto "it did arrive" and "refund it" with a browser
            ' validation error the seller could not clear by reading the page. The
            ' service refuses a reship with an empty number, which is the real gate.
            sb.Append(Input("resolveTracking", "New J&amp;T tracking number (only if sending it again)", "", False))
            sb.Append(Input("resolveIgRef", "IG reference (DM/message link) *", "", True))
            sb.Append("</form></div>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' True when a refund is still available on this order.
        ''' </summary>
        ''' <remarks>
        ''' Three conditions, each answering a different version of "is there anything
        ''' to refund": the money was actually taken (PAID -- a refund of a PENDING or
        ''' FAILED payment is a cancellation, not a refund), it has not already been
        ''' refunded, and it was not cancelled. HELD is excluded because the dispute
        ''' panel already carries the "Refund it" outcome, and two refund buttons on
        ''' one page invite the wrong one.
        ''' </remarks>
        Private Shared Function CanRefund(o As Order) As Boolean
            Return o IsNot Nothing AndAlso
                   o.PaymentStatus = "PAID" AndAlso
                   o.Status <> OrderStatuses.Refunded AndAlso
                   o.Status <> OrderStatuses.Cancelled AndAlso
                   o.Status <> OrderStatuses.Held
        End Function

        ''' <summary>
        ''' Where this order's earning stands. Read-only on purpose: the ledger moves
        ''' with the order (hold on delivery, release on receipt, void on refund), so
        ''' there is nothing to press here.
        ''' </summary>
        Private Function EarningsPanel(o As Order) As String
            Dim row As OrderCommission = _ledger.GetForOrder(o.Id)
            If row Is Nothing Then Return ""
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card mb""><b style=""display:block;margin-bottom:6px"">Studio earning</b>")
            sb.Append("<div class=""kv"">")
            sb.Append("<dt>Amount</dt><dd>" & WebUi.Money(row.Amount) & "</dd>")
            sb.Append("<dt>State</dt><dd>" & WebUi.Badge(row.StatusLabel) & "</dd>")
            sb.Append("<dt>Held since</dt><dd>" & Fmt.DateTimeF(row.HeldAt) & "</dd>")
            If row.ReleasedAt.HasValue Then
                sb.Append("<dt>Released</dt><dd>" & Fmt.DateTimeF(row.ReleasedAt.Value) & "</dd>")
            End If
            If row.VoidedAt.HasValue Then
                sb.Append("<dt>Voided</dt><dd>" & Fmt.DateTimeF(row.VoidedAt.Value) & "</dd>")
            End If
            If Not String.IsNullOrWhiteSpace(row.Reason) Then
                sb.Append("<dt>Note</dt><dd>" & WebUi.Esc(row.Reason) & "</dd>")
            End If
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Courier status and proof of delivery. Saving here records evidence only:
        ''' it never closes the order.
        ''' </summary>
        Private Function PodPanel(o As Order) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card mb"">")
            sb.Append("<b style=""display:block;margin-bottom:4px"">Courier outcome &amp; proof of delivery</b>")
            sb.Append("<p class=""sub"" style=""margin:0 0 10px;color:var(--ink)"">Read it off the J&amp;T tracking " &
                      "page and type it here. Saving this does <b>not</b> close the order — it is evidence, and the " &
                      "customer is the one who normally confirms receipt.</p>")
            sb.Append("<form method=""post"" enctype=""multipart/form-data"" action=""/App/Merchant/Delivery.aspx?id=" & o.Id.ToString() & """>")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<input type=""hidden"" name=""dact"" value=""pod"">")
            sb.Append("<div style=""display:grid;gap:10px;max-width:620px"">")
            sb.Append(Input("courierStatus", "Courier status (e.g. Delivered - left with neighbour)", o.CourierStatus, False))
            sb.Append(Input("podRef", "Courier reference / waybill *", o.PodReference, True))
            sb.Append("<label style=""display:block""><span class=""sub"">Proof image (screenshot) *</span>")
            sb.Append("<input type=""file"" name=""podImage"" accept=""image/*"" required " &
                      "style=""width:100%;padding:8px;border-radius:10px;border:1px solid var(--line);background:#fff"">")
            sb.Append("</label>")
            sb.Append("<input type=""hidden"" name=""podMime"" value=""image/png"">")
            sb.Append("<div><button class=""btn primary"" type=""submit""><span class=""ic ms"">photo_camera</span>" +
                      "<span>Save proof of delivery</span></button></div>")
            sb.Append("</div></form></div>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Close an order on the customer's behalf. The reason box is required by
        ''' the service, not just the markup: a studio close with no explanation is
        ''' the thing the audit trail exists to prevent.
        ''' </summary>
        Private Function ClosePanel(o As Order) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card mb"" style=""border-color:var(--line)"">")
            sb.Append("<b style=""display:block;margin-bottom:4px"">Already confirmed with the customer?</b>")
            sb.Append("<p class=""sub"" style=""margin:0 0 10px;color:var(--ink)"">If they told you over the phone " &
                      "that it arrived but never pressed the button, record it here. The reason is kept on the order " &
                      "and shown to them — it is not optional.</p>")
            sb.Append("<form method=""post"" action=""/App/Merchant/Delivery.aspx?id=" & o.Id.ToString() & """>")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<input type=""hidden"" name=""dact"" value=""received"">")
            sb.Append(Input("studioReason", "How do you know it arrived? *", "", True))
            sb.Append(Input("studioIgRef", "IG reference (DM/message link) *", "", True))
            sb.Append(Input("studioPassword", "Your password *", "", True, "password"))
            sb.Append("<div style=""margin-top:8px""><button class=""btn primary"" type=""submit"">" +
                      "<span class=""ic ms"">task_alt</span><span>Mark as received for the customer</span></button></div>")
            sb.Append("</form></div>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Record a refund for an order. Admin only, requires reason, IG reference,
        ''' and password. The order status becomes REFUNDED (not CANCELLED).
        ''' </summary>
        Private Function RefundPanel(o As Order) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card mb"" style=""border-color:var(--line)"">")
            sb.Append("<b style=""display:block;margin-bottom:4px"">Record a refund for this order?</b>")
            sb.Append("<p class=""sub"" style=""margin:0 0 10px;color:var(--ink)"">The order status will become REFUNDED. " &
                      "The IG reference and reason are kept on the order and shown to the customer.</p>")
            sb.Append("<form method=""post"" action=""/App/Merchant/Delivery.aspx?id=" & o.Id.ToString() & """>")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<input type=""hidden"" name=""dact"" value=""refund"">")
            sb.Append(Input("refundReason", "Reason for refund *", "", True))
            sb.Append(Input("refundIgRef", "IG reference (DM/message link) *", "", True))
            sb.Append(Input("refundPassword", "Your password *", "", True, "password"))
            sb.Append("<div style=""margin-top:8px""><button class=""btn danger"" type=""submit"">" +
                      "<span class=""ic ms"">money_off</span><span>Record refund</span></button></div>")
            sb.Append("</form></div>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' The same timeline the customer sees, rendered from the same formatter, so
        ''' the two sides of a support conversation are never reading different words
        ''' about the same order.
        ''' </summary>
        ''' <remarks>
        ''' Dates are dropped here on purpose: every line already says who acted and on
        ''' what grounds, and a date in front of "RECEIVED &middot; courier POD on file"
        ''' only makes the sentence harder to scan. The exact timestamps remain in
        ''' OrderStatusHistory for anyone who needs them.
        ''' </remarks>
        Private Function TrailPanel(o As Order) As String
            Dim trail As List(Of String) = _orders.DeliveryTimeline(o)
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card""><b style=""display:block;margin-bottom:6px"">Delivery timeline</b>")
            If trail.Count = 0 Then
                sb.Append("<p class=""sub"" style=""margin:0"">Nothing recorded yet.</p>")
            Else
                sb.Append("<ul class=""sub"" style=""margin:0;padding-left:18px;list-style:disc"">")
                For Each line As String In trail
                    sb.Append("<li style=""margin-bottom:3px"">" & WebUi.Esc(line) & "</li>")
                Next
                sb.Append("</ul>")
            End If
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function Input(name As String, label As String, value As String, required As Boolean,
                              Optional type As String = "text") As String
            Dim req As String = If(required, " required", "")
            Dim mark As String = If(required, " *", "")
            Return "<label style=""display:block""><span class=""sub"">" & WebUi.Esc(label & mark) & "</span>" &
                   "<input type=""" & WebUi.Attr(type) & """ name=""" & WebUi.Attr(name) & """ value=""" & WebUi.Attr(value) & """" & req &
                   " style=""width:100%;padding:10px;border-radius:10px;border:1px solid var(--line);font:inherit"">" &
                   "</label>"
        End Function

    End Class

End Namespace