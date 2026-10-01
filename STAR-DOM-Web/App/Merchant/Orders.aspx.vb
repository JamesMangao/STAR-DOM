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
                ' Payment recording is password-gated: merchant/admin re-enters their
                ' own password; GCash/Maya also require the reference number.
                If Guard.IsPost() AndAlso Request.Form("payOrderId") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.Form("payOrderId"), id)
                    Dim o As Order = _orders.GetOrder(id)
                    If o IsNot Nothing Then
                        Dim r As ServiceResult = _orders.ConfirmPayment(o.OrderNumber, Request.Form("payRef"), Request.Form("payPassword"))
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
                    End If
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If
                ' Merchant side of the pick-up claim: "handed to the customer".
                If Guard.IsPost() AndAlso Request.Form("pickupOrderId") <> "" Then
                    Dim pid As Integer = 0
                    Integer.TryParse(Request.Form("pickupOrderId"), pid)
                    Dim r2 As ServiceResult = _orders.ConfirmPickup(pid, False)
                    Session("flash_msg") = r2.Message
                    Session("flash_ok") = r2.Success
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If
                ' Book a delivery order with J&T — merchant enters the real tracking
                ' number; when left blank a placeholder booking number is generated.
                If Guard.IsPost() AndAlso Request.Form("shipOrderId") <> "" Then
                    Dim sid As Integer = 0
                    Integer.TryParse(Request.Form("shipOrderId"), sid)
                    Dim tracking As String = Trim(Convert.ToString(Request.Form("tracking")))
                    If tracking = "" Then tracking = "JT" & Date.Now.ToString("yyMMddHHmm")
                    Dim r3 As ServiceResult = _orders.UpdateOrderState(sid, "SHIPPED", tracking)
                    Session("flash_msg") = r3.Message
                    Session("flash_ok") = r3.Success
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If

                ' ----- GET actions ----------------------------------------------
                If Request.QueryString("advance") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("advance"), id)
                    Dim o As Order = _orders.GetOrder(id)
                    If o IsNot Nothing Then
                        Dim nextState As String = MapNextState(o.Status)
                        If o.IsPickup AndAlso (nextState = "SHIPPED" OrElse nextState = "DELIVERED") Then nextState = ""
                        If nextState <> "" Then
                            Dim tracking As String = ""
                            If nextState = "SHIPPED" Then tracking = "JT" & Date.Now.ToString("yyMMddHHmm")
                            Dim r As ServiceResult = _orders.UpdateOrderState(id, nextState, tracking)
                            Session("flash_msg") = r.Message
                            Session("flash_ok") = r.Success
                        End If
                    End If
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If
                If Request.QueryString("cancel") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("cancel"), id)
                    If id > 0 Then
                        Dim r As ServiceResult = _orders.UpdateOrderState(id, "CANCELLED")
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
                    End If
                    Response.Redirect("/App/Merchant/Orders.aspx", True)
                End If
                If Request.QueryString("receipt") <> "" Then
                    Dim pid As Integer = 0
                    Integer.TryParse(Request.QueryString("receipt"), pid)
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
                                    "Confirm orders, prepare them, then book J&T delivery or hand over at the stall. Recording a payment requires your password."))

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
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"ORDER", "CUSTOMER", "ITEMS", "TOTAL", "PAYMENT", "PAY STATE", "STATUS", "ACTIONS"}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each o As Order In shown
                    sb.Append("<tr>")
                    sb.Append("<td><b>" & WebUi.Esc(o.OrderNumber) & "</b><br><span class=""sub"" style=""font-size:11px"">" &
                              WebUi.Esc(o.CreatedAt.ToString("MMM d, h:mm tt")) & "</span></td>")
                    sb.Append("<td>" & WebUi.Esc(o.CustomerName) & "</td>")
                    sb.Append("<td>" & o.ItemCount.ToString() & "</td>")
                    sb.Append("<td>" & WebUi.Money(o.TotalAmount) & "</td>")
                    sb.Append("<td>" & WebUi.Esc(DisplayPay(o.PaymentMethod)) &
                              If(o.IsPickup, "<br><span class=""sub"" style=""font-size:10.5px"">PICK-UP @ stall</span>", "") & "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.PaymentStatus) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.Status) &
                              If(o.IsPickup AndAlso o.PickupEventName <> "",
                                 "<br><span class=""sub"" style=""font-size:10.5px"">" & WebUi.Esc(o.PickupEventName) & "</span>", "") & "</td>")
                    sb.Append("<td class=""rowact"">")
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
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"ORDER", "METHOD", "AMOUNT", "REFERENCE", "STATUS", "DATE", "RECEIPT"}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each p As Payment In pays
                    Dim orLink As String = ""
                    Dim ptr As Receipt = Nothing
                    If receiptMap.ContainsKey(p.Id) Then ptr = receiptMap(p.Id)
                    If ptr IsNot Nothing Then
                        orLink = "<a href=""/App/Receipt.aspx?p=" & p.Id.ToString() & """><span class=""ms sm"">receipt_long</span> " &
                                 WebUi.Esc(ptr.ReceiptNumber) & "</a>"
                    ElseIf p.Status = "PAID" Then
                        orLink = "<a href=""/App/Merchant/Orders.aspx?receipt=" & p.Id.ToString() & """><span class=""ms sm"">receipt_long</span> Issue OR</a>"
                    Else
                        orLink = "<span class=""sub"" style=""font-size:11px"">—</span>"
                    End If
                    sb.Append("<tr><td><b>#" & p.OrderId.ToString() & "</b></td>")
                    sb.Append("<td>" & WebUi.Esc(p.DisplayMethod) & "</td>")
                    sb.Append("<td>" & WebUi.Money(p.Amount) & "</td>")
                    sb.Append("<td>" & WebUi.Esc(p.ReferenceNumber) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(p.Status) & "</td>")
                    sb.Append("<td>" & WebUi.Esc(p.CreatedAt.ToString("MMM d, yyyy h:mm tt")) & "</td>")
                    sb.Append("<td class=""rowact"">" & orLink & "</td></tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If
            Out.Text = sb.ToString()
        End Sub

        ''' <summary>Per-row actions: advance status, book J&amp;T, confirm hand-over, record payment.</summary>
        Private Sub RenderActions(sb As StringBuilder, o As Order)
            Dim nextState As String = MapNextState(o.Status)
            If o.IsPickup AndAlso (nextState = "SHIPPED" OrElse nextState = "DELIVERED") Then nextState = ""

            ' Delivery orders: confirm → prepare → book J&T (with tracking) → delivered.
            If nextState <> "" AndAlso nextState <> "SHIPPED" Then
                sb.Append("<a href=""/App/Merchant/Orders.aspx?advance=" & o.Id.ToString() & """>" &
                          "<span class=""ms sm"">arrow_forward</span> " & nextState & "</a>")
            ElseIf nextState = "SHIPPED" Then
                sb.Append("<form method=""post"" action=""/App/Merchant/Orders.aspx"" style=""display:flex;gap:4px;margin:2px 0;flex-wrap:wrap"">")
                ' Each of the per-order forms below is nested inside the shell form, which
                ' the browser closes at this tag — so the shell's token is not submitted with
                ' them. They each carry their own.
                sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                sb.Append("<input type=""hidden"" name=""shipOrderId"" value=""" & o.Id.ToString() & """>")
                sb.Append("<input name=""tracking"" placeholder=""J&T tracking no."" style=""width:112px;padding:4px 6px;border:1px solid var(--line);border-radius:6px;font-size:11px"">")
                sb.Append("<button class=""btn ghost sm"" type=""submit"" title=""Book with J&T Express""><span class=""ms sm"">local_shipping</span>Book J&T</button>")
                sb.Append("</form>")
            End If

            ' Pick-up orders: the stall confirms the hand-over; the customer confirms
            ' receipt. When both sides have confirmed, the order closes as DELIVERED.
            If o.IsPickup AndAlso o.Status <> "CANCELLED" AndAlso o.Status <> "DELIVERED" Then
                If Not o.PickupMerchantConfirmed Then
                    sb.Append("<form method=""post"" action=""/App/Merchant/Orders.aspx"" style=""display:inline-flex;margin:2px 0"">")
                    sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                    sb.Append("<input type=""hidden"" name=""pickupOrderId"" value=""" & o.Id.ToString() & """>")
                    sb.Append("<button class=""btn ghost sm"" type=""submit"" data-confirm=""Confirm the customer has claimed this order at the stall?""><span class=""ms sm"">task_alt</span>Confirm hand-over</button>")
                    sb.Append("</form>")
                ElseIf Not o.PickupCustomerConfirmed Then
                    sb.Append("<span class=""sub"" style=""font-size:10.5px"">waiting for customer…</span>")
                End If
            End If

            ' Payment recording — password always; e-wallet reference for GCash/Maya.
            ' COD / pay-on-claim payments are recorded the same way when the cash comes in.
            If o.PaymentStatus <> "PAID" AndAlso o.PaymentStatus <> "REFUNDED" AndAlso o.Status <> "CANCELLED" Then
                Dim isEWallet As Boolean = o.PaymentMethod = "GCASH" OrElse o.PaymentMethod = "MAYA"
                sb.Append("<form method=""post"" action=""/App/Merchant/Orders.aspx"" style=""display:flex;gap:4px;margin:2px 0;flex-wrap:wrap"">")
                sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                sb.Append("<input type=""hidden"" name=""payOrderId"" value=""" & o.Id.ToString() & """>")
                If isEWallet Then
                    sb.Append("<input name=""payRef"" placeholder=""Ref no."" required style=""width:86px;padding:4px 6px;border:1px solid var(--line);border-radius:6px;font-size:11px"">")
                End If
                sb.Append("<input type=""password"" name=""payPassword"" placeholder=""Your password"" required autocomplete=""current-password"" style=""width:104px;padding:4px 6px;border:1px solid var(--line);border-radius:6px;font-size:11px"">")
                sb.Append("<button class=""btn ghost sm"" type=""submit"" title=""Record this payment""><span class=""ms sm"">payments</span>Confirm pay</button>")
                sb.Append("</form>")
            End If

            If o.Status = "PENDING" Then
                sb.Append("<a href=""/App/Merchant/Orders.aspx?cancel=" & o.Id.ToString() & """ data-confirm=""Cancel this order?"" data-confirm-danger"">Cancel</a>")
            End If
            sb.Append("<a href=""/App/OrderDetail.aspx?id=" & o.Id.ToString() & """>View</a>")
        End Sub

        Private Function DisplayPay(pm As String) As String
            Select Case pm.ToUpperInvariant()
                Case "GCASH" : Return "GCash"
                Case "MAYA" : Return "Maya"
                Case "CARD" : Return "Card"
                Case "COD" : Return "Cash on Delivery"
                Case Else : Return pm
            End Select
        End Function

    End Class

End Namespace
