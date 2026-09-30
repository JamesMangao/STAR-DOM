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
                If Request.QueryString("advance") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("advance"), id)
                    Dim o As Order = _orders.GetOrder(id)
                    If o IsNot Nothing Then
                        Dim nextState As String = MapNextState(o.Status)
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
                If Request.QueryString("pay") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("pay"), id)
                    Dim o As Order = _orders.GetOrder(id)
                    If o IsNot Nothing Then
                        Dim r As ServiceResult = _orders.ConfirmPayment(o.OrderNumber, "POS-" & Date.Now.ToString("yyMMddHHmmss"))
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
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
                                    "Process orders end-to-end: confirm, prepare, ship, deliver, record payments."))

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
                    sb.Append("<td>" & WebUi.Esc(DisplayPay(o.PaymentMethod)) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.PaymentStatus) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.Status) & "</td>")
                    sb.Append("<td class=""rowact"">")
                    Dim nextState As String = MapNextState(o.Status)
                    If nextState <> "" Then
                        sb.Append("<a href=""/App/Merchant/Orders.aspx?advance=" & o.Id.ToString() & """>" &
                                  If(nextState = "SHIPPED", "<span class=""ms sm"">local_shipping</span> Ship", "<span class=""ms sm"">arrow_forward</span> " & nextState) & "</a>")
                    End If
                    If o.PaymentStatus <> "PAID" AndAlso Not String.Equals(o.PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase) Then
                        sb.Append("<a href=""/App/Merchant/Orders.aspx?pay=" & o.Id.ToString() & """>Record payment</a>")
                    End If
                    If o.Status = "PENDING" Then
                        sb.Append("<a href=""/App/Merchant/Orders.aspx?cancel=" & o.Id.ToString() & """ data-confirm=""Cancel this order?"" data-confirm-danger"">Cancel</a>")
                    End If
                    sb.Append("<a href=""/App/OrderDetail.aspx?id=" & o.Id.ToString() & """>View</a>")
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
