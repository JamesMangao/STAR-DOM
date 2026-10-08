Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class MerchantOrdersPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _orders As New OrderService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                ' ----- POST actions ---------------------------------------------
                ' Every row action carries its order id in the submit button's own name
                ' ("act_quote_12"), and that button is the dispatch key. The page lives
                ' inside Site.master's single shell <form>, and an HTML parser drops each
                ' row's own <form> start tag, so one click publishes EVERY row's fields at
                ' once: the same feeOrderId/shipOrderId/payOrderId name then arrives once
                ' per row and Request.Form hands back every value joined by commas. A
                ' shared hidden id picked whichever row came first in the body, so quoting
                ' a fee on one order could book J&T on a different one.
                '
                ' Payment verification is password-gated: the customer submits the
                ' e-wallet reference from their side, and the merchant re-enters
                ' their own password to confirm or decline that submission.
                Dim actKind As String = ""
                Dim actId As Integer = 0
                If Guard.IsPost() AndAlso ActionKey(actKind, actId) Then
                    Dim suffix As String = "_" & actId.ToString()
                    Select Case actKind
                        ' Quote the J&T fee and confirm the order in one step. For a
                        ' delivery order this is the ONLY way into CONFIRMED —
                        ' UpdateOrderState refuses that transition until a fee has been
                        ' quoted, so the customer never sees a total without shipping.
                        Case "quote"
                            Dim rf As ServiceResult = _orders.ConfirmWithShippingFee(actId,
                                                                Request.Form("fee" & suffix),
                                                                Request.Form("pw" & suffix))
                            Session("flash_msg") = rf.Message
                            Session("flash_ok") = rf.Success
                        ' Confirm the customer's submitted reference. There is no ref
                        ' box here on purpose: the number to verify is the one the
                        ' customer submitted, read from the payment row in the service.
                        Case "pay"
                            Dim rp As ServiceResult = _orders.ConfirmPayment(actId,
                                                                Request.Form("pw" & suffix))
                            Session("flash_msg") = rp.Message
                            Session("flash_ok") = rp.Success
                        ' Book a delivery order with J&T — the tracking number is
                        ' entered manually; it is never auto-generated. A blank
                        ' tracking number is rejected by the service layer.
                        Case "ship"
                            Dim tracking As String = Trim(Convert.ToString(Request.Form("trk" & suffix)))
                            Dim r3 As ServiceResult = _orders.UpdateOrderState(actId, "SHIPPED", tracking)
                            Session("flash_msg") = r3.Message
                            Session("flash_ok") = r3.Success
                        ' The seller declines a payment reference that does not check
                        ' out. Password-gated like confirming, and the customer is
                        ' notified with the support contact message via the service
                        ' layer (OrderService.DenyPayment).
                        Case "decline"
                            Dim r4 As ServiceResult = _orders.DenyPayment(actId,
                                                                Request.Form("pw" & suffix))
                            Session("flash_msg") = r4.Message
                            Session("flash_ok") = r4.Success
                    End Select
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If

                ' ----- row actions (all POST: these change state) -------------------
                ' The handlers require Guard.IsPost, so the old ?advance/?cancel/
                ' ?receipt links were GET navigations that never reached them. The
                ' row id now rides on the pressed button.
                If Guard.IsPost() AndAlso Request.Form("advance") IsNot Nothing Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.Form("advance"), id)
                    Dim o As Order = _orders.GetOrder(id)
                    If o IsNot Nothing Then
                        Dim nextState As String = MapNextState(o.Status)
                        If nextState <> "" Then
                            Dim r As ServiceResult = _orders.UpdateOrderState(id, nextState, "")
                            Session("flash_msg") = r.Message
                            Session("flash_ok") = r.Success
                        End If
                    End If
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If
                If Guard.IsPost() AndAlso Request.Form("cancel") IsNot Nothing Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.Form("cancel"), id)
                    If id > 0 Then
                        Dim r As ServiceResult = _orders.UpdateOrderState(id, "CANCELLED")
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
                    End If
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If
                If Guard.IsPost() AndAlso Request.Form("issueReceipt") IsNot Nothing Then
                    Dim pid As Integer = 0
                    Integer.TryParse(Request.Form("issueReceipt"), pid)
                    If pid > 0 Then
                        _orders.IssueReceiptByPaymentId(pid)
                    End If
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If
                Render()
            Catch aborted As System.Threading.ThreadAbortException
                ' Response.Redirect(url, True) raises this by design once the redirect
                ' is already committed. Let it re-raise — the redirect must stand.
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load orders: " & ex.Message)
            End Try
        End Sub

        Private Function MapNextState(st As String) As String
            Select Case st
                Case "PENDING" : Return "CONFIRMED"
                Case "CONFIRMED" : Return "PROCESSING"
                Case "PROCESSING" : Return "SHIPPED"
                Case "SHIPPED" : Return "DELIVERED"
                Case Else : Return ""
            End Select
        End Function

        Private Sub Render()
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append(WebUi.Section("Orders & Payments", "MERCHANT STUDIO / FULFILMENT",
                                    "Confirm orders, verify submitted payments, prepare them, then book J&T delivery. Payment verification requires your password."))

            Dim statusFilter As String = Convert.ToString(Request.QueryString("st"))
            Dim page As Integer = 1
            Integer.TryParse(Convert.ToString(Request.QueryString("p")), page)
            If page < 1 Then page = 1
            Const PageSize As Integer = 25

            Dim totalOrders As Integer = _orders.CountAllOrders("", statusFilter)
            Dim shown As List(Of Order) = _orders.AllOrders("", statusFilter, page, PageSize)

            sb.Append("<div class=""frow"">")
            For Each chip As String In {"", "PENDING", "CONFIRMED", "PROCESSING", "SHIPPED", "DELIVERED", "CANCELLED"}
                Dim label As String = If(chip = "", "ALL", chip)
                Dim url As String = If(chip = "", "/App/Merchant/Orders.aspx", "/App/Merchant/Orders.aspx?st=" & chip)
                sb.Append(WebUi.OutLink(url, label, String.Equals(statusFilter, chip, StringComparison.OrdinalIgnoreCase)))
            Next
            sb.Append("</div>")

            If shown.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No orders in this view."))
            Else
                sb.Append(WebUi.Pager(totalOrders, PageSize, page, If(statusFilter = "", "/App/Merchant/Orders.aspx?p={P}",
                                                                    "/App/Merchant/Orders.aspx?st=" & statusFilter & "&p={P}")))
                sb.Append("<div class=""tblwrap""><table class=""tbl tbl-orders""><colgroup>" &
                              "<col style=""width:140px""><col style=""width:120px""><col style=""width:44px"">" &
                              "<col style=""width:116px""><col style=""width:112px""><col style=""width:104px"">" &
                              "<col style=""width:116px""><col style=""width:302px""></colgroup><thead><tr>")
                For Each h As String In {"ORDER", "CUSTOMER", "ITEMS", "TOTAL", "PAYMENT", "PAY STATE", "STATUS", "ACTIONS"}
                    sb.Append("<th" & If(h = "ACTIONS", " class=""col-actions""", "") & ">" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each o As Order In shown
                    sb.Append("<tr>")
                    sb.Append("<td><span class=""ord-no"">" & WebUi.Esc(o.OrderNumber) & "</span><br>" &
                              "<span class=""ord-when"">" & WebUi.Esc(o.CreatedAt.ToString("MMM d, h:mm tt")) & "</span></td>")
                    sb.Append("<td class=""cust"">" & WebUi.Esc(o.CustomerName) & "</td>")
                    sb.Append("<td class=""num"">" & o.ItemCount.ToString() & "</td>")
                    ' A delivery order with no fee yet is showing a goods total, not the amount the
                    ' customer owes — flag it so the figure is never misread as final.
                    sb.Append("<td class=""total"">" & WebUi.Money(o.TotalAmount) &
                              If(o.HasFinalTotal, "", "<br><span class=""tbc"">+ shipping TBC</span>") & "</td>")
                    sb.Append("<td class=""pay"">" & WebUi.Esc(DisplayPay(o.PaymentMethod)) &
                              "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.PaymentStatus) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.Status) &
                              "</td>")
                    sb.Append("<td class=""col-actions"">")
                    RenderActions(sb, o)
                    sb.Append("</td></tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If

            ' payments ledger (last 500, newest first — bounded in SQL)
            Dim pays As List(Of Payment) = _orders.ListPayments("", 500)
            Dim receiptMap As Dictionary(Of Integer, Receipt) = _orders.ReceiptsForPaymentIds(pays.Select(Function(pp) pp.Id).ToList())
            sb.Append("<div class=""sec-head"" style=""margin-top:24px""><div><h3>Payments ledger (" & pays.Count.ToString() & ")</h3></div></div>")
            If pays.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No payments recorded yet."))
            Else
                sb.Append("<div class=""tblwrap""><table class=""tbl tbl-ledger""><colgroup>" &
                              "<col style=""width:74px""><col style=""width:128px""><col style=""width:120px"">" &
                              "<col><col style=""width:110px""><col style=""width:170px""><col style=""width:190px"">" &
                              "</colgroup><thead><tr>")
                For Each h As String In {"ORDER", "METHOD", "AMOUNT", "REFERENCE", "STATUS", "DATE", "RECEIPT"}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each p As Payment In pays
                    Dim orLink As String = ""
                    Dim o As Order = _orders.GetOrder(p.OrderId)
                    Dim isCancelled As Boolean = (o IsNot Nothing AndAlso o.Status = "CANCELLED")

                    Dim ptr As Receipt = Nothing
                    If receiptMap.ContainsKey(p.Id) Then ptr = receiptMap(p.Id)
                    If isCancelled Then
                        orLink = "<span class=""badge muted"">CANCELLED</span>"
                    ElseIf ptr IsNot Nothing Then
                        orLink = "<a href=""/App/Receipt.aspx?p=" & p.Id.ToString() & """><span class=""ms sm"">receipt_long</span> " &
                                 WebUi.Esc(ptr.ReceiptNumber) & "</a>"
                    ElseIf p.Status = "PAID" Then
                        orLink = "<form method=""post"" style=""display:inline"">" & STAR_DOM.Web.Csrf.HiddenField() &
                                 "<button type=""submit"" name=""issueReceipt"" value=""" & p.Id.ToString() &
                                 """ style=""background:none;border:0;padding:0;font:inherit;color:var(--primary);cursor:pointer"">" &
                                 "<span class=""ms sm"">receipt_long</span> Issue OR</button></form>"
                    Else
                        orLink = "<span class=""sub"" style=""font-size:11px"">—</span>"
                    End If
                    sb.Append("<tr><td><span class=""ord-no"">#" & p.OrderId.ToString() & "</span></td>")
                    sb.Append("<td>" & WebUi.Esc(p.DisplayMethod) & "</td>")
                    sb.Append("<td class=""amt"">" & WebUi.Money(p.Amount) & "</td>")
                    sb.Append("<td class=""ref"">" & WebUi.Esc(p.ReferenceNumber) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(p.Status) & "</td>")
                    sb.Append("<td class=""when"">" & WebUi.Esc(p.CreatedAt.ToString("MMM d, yyyy h:mm tt")) & "</td>")
                    sb.Append("<td class=""rowact"">" & orLink & "</td></tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If
            Out.Text = sb.ToString()
        End Sub

        ''' <summary>Per-row actions: one panel for the row's step, then the View link.</summary>
        ''' <remarks>
        ''' The row is a strict sequence, one form at a time, and that is the point:
        ''' every step below asks the merchant to re-enter their password, so rendering
        ''' two of them side by side put two identical-looking "Your password" boxes in
        ''' one cell with nothing to tell them apart. So a row shows exactly one of:
        '''   1. the shipping quote, while the order is still unquoted;
        '''   2. the payment verification form, once the customer has submitted a
        '''      reference (confirm or decline it with your password);
        '''   3. a waiting hint, while quoted but the customer has not paid yet;
        '''   4. an advance button, once paid;
        '''   5. the J&amp;T booking form, when it is ready to go;
        '''   6. a state note, when the order has nowhere left to go.
        '''
        ''' Whatever the step, the cell now gets exactly one .act-fields panel followed
        ''' by the same one-line View / Payment Details link. It used to be a ~176px
        ''' quote form on one row, a ~119px tracking box on the next and a bare link on
        ''' the third, so the ACTIONS column was a staircase and no two rows lined up.
        ''' </remarks>
        Private Sub RenderActions(sb As StringBuilder, o As Order)
            Dim nextState As String = MapNextState(o.Status)
            Dim paid As Boolean = o.PaymentStatus = "PAID"
            Dim needsQuote As Boolean = NeedsShippingQuote(o)
            ' True when the panel is built around a real <form>. Advance and cancel
            ' then ride inside it - one form, one token - instead of bringing their own
            ' wrapper the way they used to inside .act-links.
            Dim inForm As Boolean = needsQuote OrElse o.PaymentStatus = "SUBMITTED"

            ' The rest of the row's story (advance, the J&T box, the "not yet" hints,
            ' cancel) is collected first so the panel can be closed exactly once,
            ' whichever shape its front half takes. Two panels in one cell - or none -
            ' is what made this column ragged.
            Dim tail As New StringBuilder()
            Dim jntForm As String = ""

            ' Delivery orders: confirm -> prepare -> book J&T (with tracking) -> delivered.
            ' The CONFIRMED step is skipped when a quote is still outstanding.
            If nextState <> "" AndAlso nextState <> "SHIPPED" Then
                If nextState = "CONFIRMED" AndAlso needsQuote Then
                    ' handled below by RenderShippingQuoteForm
                ElseIf nextState = "PROCESSING" AndAlso Not paid Then
                    ' Fulfilment only starts on a confirmed payment. The service layer
                    ' refuses the transition too; this is here so the console says why
                    ' instead of showing a link that silently fails.
                    tail.Append(ActHint("Confirm the payment before moving this order to processing"))
                Else
                    tail.Append(ActButton("advance", o.Id.ToString(),
                                          "<span class=""ms sm"">arrow_forward</span> " & nextState,
                                          "Advance this order to " & nextState & "?", False, inForm))
                End If
            ElseIf nextState = "SHIPPED" Then
                ' Nothing leaves the studio until the money is in. The service layer
                ' refuses the booking too; this is here so the console says why
                ' instead of showing a button that silently fails.
                If paid Then
                    jntForm = RenderBookJntForm(o, tail.ToString())
                Else
                    tail.Append(ActHint("Confirm the payment above before booking J&amp;T"))
                End If
            End If

            ' A PENDING order can still be stopped; the button is full width and sits
            ' in the panel with the rest of the row's actions, so the View line under
            ' it stays a single line on every row.
            If o.Status = "PENDING" Then
                tail.Append(ActButton("cancel", o.Id.ToString(), "Cancel",
                                      "Cancel this order?", True, inForm))
            End If

            Dim panel As String
            If needsQuote Then
                panel = RenderShippingQuoteForm(o, tail.ToString())
            ElseIf o.PaymentStatus = "SUBMITTED" Then
                ' The customer has paid outside the app and handed over the reference;
                ' this is where the studio checks it and confirms or declines.
                panel = RenderVerifyPayForm(o, tail.ToString())
            ElseIf jntForm <> "" Then
                panel = jntForm
            ElseIf CanRecordPayment(o) OrElse tail.Length > 0 Then
                panel = ActNotePanel() &
                        If(CanRecordPayment(o), ActHint("Waiting for the customer to submit their payment reference"), "") &
                        tail.ToString() & "</div>"
            Else
                panel = RenderStatePanel(o)
            End If
            sb.Append("<div class=""act-stack"">")
            sb.Append(panel)

            ' The View link rides on every row, on its own, so the line under the panel
            ' is the same height everywhere. For orders with SUBMITTED payment status it
            ' says what it is for: this is the payment verification area, not the
            ' customer page.
            Dim viewUrl As String = "/App/OrderDetail.aspx?id=" & o.Id.ToString()
            Dim links As New StringBuilder()
            If o.PaymentStatus = "SUBMITTED" Then
                links.Append("<a href=""" & WebUi.Attr(viewUrl) & """ " &
                             "data-confirm=""" & WebUi.Attr("View this order's payment details?") & """>" &
                             WebUi.Ic("visibility", "sm") & " Payment Details</a>")
            Else
                links.Append("<a href=""" & WebUi.Attr(viewUrl) & """>" & WebUi.Ic("visibility", "sm") & " View</a>")
            End If
            sb.Append("<div class=""act-links"">" & links.ToString() & "</div>")
            sb.Append("</div>")
        End Sub

        ''' <summary>The small grey caption line inside a panel.</summary>
        Private Function ActHint(text As String) As String
            Return "<span class=""act-hint"">" & text & "</span>"
        End Function

        ''' <summary>Opens a hint-only panel: the row has no form of its own.</summary>
        Private Function ActNotePanel() As String
            Return "<div class=""act-fields act-note"">"
        End Function

        ''' <summary>
        ''' A panel-sized POST button. Inside a row form it is emitted bare - that form
        ''' already carries the CSRF token; outside one (a hint-only or state panel) it
        ''' brings its own wrapper, exactly as it did inside .act-links. The pressed
        ''' button is still the only one submitted, so names, handlers and the
        ''' data-confirm flow are unchanged.
        ''' </summary>
        Private Function ActButton(name As String, value As String, labelHtml As String,
                                   confirm As String, danger As Boolean, inForm As Boolean) As String
            Dim b As New StringBuilder()
            b.Append("<button class=""" & If(danger, "btn danger sm", "btn ghost sm") &
                     """ type=""submit"" name=""" & name & """ value=""" & WebUi.Attr(value) &
                     """ data-confirm=""" & WebUi.Attr(confirm) & """")
            If danger Then b.Append(" data-confirm-danger")
            b.Append(">").Append(labelHtml).Append("</button>")
            If inForm Then Return b.ToString()
            Return "<form method=""post"">" & STAR_DOM.Web.Csrf.HiddenField() & b.ToString() & "</form>"
        End Function

        ''' <summary>
        ''' Panel for a row with nothing left to do, so terminal orders get the same
        ''' box as every other row instead of an empty cell. The copy only states what
        ''' the status itself already says.
        ''' </summary>
        Private Function RenderStatePanel(o As Order) As String
            Dim note As String
            Select Case o.Status
                Case "DELIVERED"
                    note = "Parcel delivered &mdash; waiting for the customer to confirm receipt."
                Case "CANCELLED"
                    note = "Order cancelled &mdash; nothing left to do here."
                Case "RECEIVED"
                    note = "Customer has received this order &mdash; nothing left to do here."
                Case Else
                    note = "Order is " & WebUi.Esc(o.Status) & " &mdash; nothing left to do here."
            End Select
            Return ActNotePanel() & ActHint(note) & "</div>"
        End Function

        ''' <summary>
        ''' Picks one row action out of a POST: the pressed submit button is named
        ''' act_&lt;kind&gt;_&lt;orderId&gt;. False when the body carries no action button.
        ''' </summary>
        Private Function ActionKey(ByRef kind As String, ByRef id As Integer) As Boolean
            kind = ""
            id = 0
            For Each k As String In Request.Form.AllKeys
                If k Is Nothing OrElse Not k.StartsWith("act_", StringComparison.Ordinal) Then
                    Continue For
                End If
                Dim seg() As String = k.Split("_"c)
                Dim n As Integer = 0
                If seg.Length = 3 AndAlso seg(1).Length > 0 AndAlso
                   Integer.TryParse(seg(2), n) AndAlso n > 0 Then
                    kind = seg(1)
                    id = n
                    Return True
                End If
            Next
            Return False
        End Function

        ''' <summary>
        ''' True while a payment is still outstanding and the customer has not
        ''' submitted a reference yet: not paid, not submitted, not refunded, and
        ''' not on a cancelled order. Renders the waiting hint on the row.
        ''' </summary>
        Private Function CanRecordPayment(o As Order) As Boolean
            Return o.PaymentStatus <> "PAID" AndAlso o.PaymentStatus <> "SUBMITTED" AndAlso
                   o.PaymentStatus <> "REFUNDED" AndAlso o.Status <> "CANCELLED"
        End Function

        ''' <summary>
        ''' True when an order is waiting on its courier fee. An order that already
        ''' has one on record never needs quoting again.
        ''' </summary>
        Private Function NeedsShippingQuote(o As Order) As Boolean
            If o.ShippingFeeConfirmed Then Return False
            ' PROCESSING and beyond are deliberately absent: ConfirmWithShippingFee
            ' refuses them, because quoting writes Status = CONFIRMED and would drag
            ' an in-flight order backwards. Offering the form anyway put a dead end
            ' next to the J&T booking box on the same row.
            Return o.Status = "PENDING" OrElse o.Status = "CONFIRMED"
        End Function

''' <summary>
        ''' Inline form that quotes the J&amp;T fee and confirms the order. Password-gated
        ''' in the service layer, same as recording a payment, because it commits the
        ''' customer's final total.
        ''' </summary>
        Private Function RenderShippingQuoteForm(o As Order, tail As String) As String
            Dim sb As New StringBuilder()
            sb.Append("<form method=""post"" action=""/App/Merchant/Orders.aspx"" class=""act-form"">")
            sb.Append("<div class=""act-fields"">")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            ' Each input sits inside its own label, so the visible caption is bound to
            ' the box by the markup rather than by a for/id pair. The bare number box
            ' used to sit directly above the password box with a placeholder that
            ' vanished the moment you typed, and it pre-filled with 0, so it read as a
            ' settled value rather than the one thing this row was waiting for.
            sb.Append("<label class=""act-fld""><span class=""act-label"">Shipping fee (J&amp;T) *</span>")
            sb.Append("<input class=""i-fee"" name=""fee_" & o.Id.ToString() & """ type=""number"" min=""0"" max=""10000"" step=""1"" " &
                      "placeholder=""e.g. 145"" ")
            sb.Append("value=""" & WebUi.Attr(Fmt.Num(o.ShippingFee)) & """></label>")
            sb.Append("<label class=""act-fld""><span class=""act-label"">Your password *</span>")
            sb.Append("<input class=""i-pw"" name=""pw_" & o.Id.ToString() & """ type=""password"" " &
                      "placeholder=""Re-enter to confirm"" autocomplete=""current-password""></label>")
            ' required="" is deliberately absent: the fields of every OTHER row travel in
            ' the same submitted body, so one empty required box in a neighbouring row
            ' silently vetoes this click. Blank input is caught server-side, which is also
            ' what gets the customer the red failure note with the hotline.
            sb.Append("<button class=""btn ghost sm"" type=""submit"" name=""act_quote_" & o.Id.ToString() & """ value=""1"" " &
                      "data-confirm=""Confirm this order and send the customer the final total, including shipping?"">" &
                      "<span class=""ms sm"">sell</span>Confirm &amp; quote shipping</button>")
            ' Everything else this row has to say (the cancel button, the rare advance
            ' hint) goes inside the same panel - a second box below it is exactly what
            ' made the column's rows come out at different heights.
            sb.Append(tail)
            sb.Append("<span class=""act-hint"">Enter the J&amp;T fee to confirm this order</span>")
            sb.Append("</div>")
            sb.Append("</form>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Payment verification: the reference the customer submitted, shown read-only
        ''' so it can be checked against the studio's records, plus the password needed
        ''' to confirm or decline it. There is no reference input here on purpose —
        ''' verifying means checking THEIR number, never typing one in on their behalf.
        ''' </summary>
        Private Function RenderVerifyPayForm(o As Order, tail As String) As String
            Dim sb As New StringBuilder()
            ' An order that has not picked a channel yet carries "PENDING" as its
            ' method, and DisplayName echoes that straight back — the field was
            ' captioned "PENDING reference no.*" and the hint read "the customer's PENDING
            ' receipt". Anything that is not a real brand falls back to "payment".
            Dim brand As String = PaymentSetting.DisplayName(o.PaymentMethod)
            If brand = "" OrElse brand = "PENDING" OrElse brand = "UNPAID" Then brand = "Payment"
            Dim subRef As String = ""
            For Each p As Payment In _orders.PaymentsForOrder(o.Id)
                If p.Status = "SUBMITTED" Then
                    subRef = p.ReferenceNumber
                    Exit For
                End If
            Next
            sb.Append("<form method=""post"" action=""/App/Merchant/Orders.aspx"" class=""act-form"">")
            sb.Append("<div class=""act-fields act-vfy"">")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<label class=""act-fld""><span class=""act-label"">Reference submitted (" & WebUi.Esc(brand) & ")</span>")
            sb.Append("<input class=""i-ref"" value=""" & WebUi.Attr(subRef) & """ readonly></label>")
            sb.Append("<label class=""act-fld""><span class=""act-label"">Your password *</span>")
            sb.Append("<input class=""i-pw"" name=""pw_" & o.Id.ToString() & """ type=""password"" " &
                      "placeholder=""Re-enter to verify"" autocomplete=""current-password""></label>")
            ' Confirm and Decline sit side by side: stacked, this pair made the verify
            ' panel the tallest box in the column and every other row had to grow to
            ' match it. They stay apart visually - ghost against danger - and each
            ' keeps its own confirm dialog.
            sb.Append("<div class=""act-btnrow"">")
            sb.Append("<button class=""btn ghost sm"" type=""submit"" name=""act_pay_" & o.Id.ToString() & """ value=""1"" " +
                      "data-confirm=""Confirm this payment as received? An official receipt will be issued."">" +
                      "<span class=""ms sm"">payments</span>Confirm payment</button>")
            sb.Append("<button class=""btn danger sm"" type=""submit"" name=""act_decline_" & o.Id.ToString() & """ value=""1"" " +
                      "data-confirm=""Decline this payment? The customer will be notified."">" +
                      "<span class=""ms sm"">cancel</span>Decline</button>")
            sb.Append("</div>")
            sb.Append(tail)
            sb.Append("<span class=""act-hint"">Check the " &
                      WebUi.Esc(If(brand = "Payment", "payment", brand)) &
                      " reference against your records before confirming</span>")
            sb.Append("</div>")
            sb.Append("</form>")
            Return sb.ToString()
        End Function

        ''' <summary>The J&amp;T hand-off form: tracking number in, SHIPPED out.</summary>
        Private Function RenderBookJntForm(o As Order, tail As String) As String
            Dim sb As New StringBuilder()
            sb.Append("<form method=""post"" action=""/App/Merchant/Orders.aspx"" class=""act-form"">")
            ' Nested inside the shell form, which the browser closes at this tag — so
            ' the shell's token is not submitted with it. This one carries its own.
            sb.Append("<div class=""act-fields act-jnt"">")
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<label class=""act-fld""><span class=""act-label"">J&amp;T tracking no. *</span>")
            sb.Append("<input class=""i-track"" name=""trk_" & o.Id.ToString() & """ placeholder=""Enter J&amp;T tracking number"" required""></label>")
            sb.Append("<button class=""btn ghost sm"" type=""submit"" name=""act_ship_" & o.Id.ToString() & """ value=""1"" title=""Book with J&amp;T Express"" " +
                      "data-confirm=""Hand this parcel to J&amp;T and lock the order to SHIPPED? This cannot be undone from here."">" +
                      "<span class=""ms sm"">local_shipping</span>Book J&amp;T</button>")
            sb.Append(tail)
            sb.Append("</div>")
            sb.Append("</form>")
            Return sb.ToString()
        End Function

        Private Function DisplayPay(pm As String) As String
            Return WebUi.ChannelBrand(pm)
        End Function

    End Class

End Namespace
