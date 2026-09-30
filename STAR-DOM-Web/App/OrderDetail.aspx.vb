Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
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
                If o.UserId <> STAR_DOM.Helpers.Session.CurrentUser.Id AndAlso Not STAR_DOM.Helpers.Session.CanManageStore Then
                    Out.Text = WebUi.AlertBox("You don't have access to this order.")
                    Return
                End If

                If Request.QueryString("pay") = "1" Then
                    Dim ref As String = Request.QueryString("ref")
                    Dim r As ServiceResult = _orders.ConfirmPayment(o.OrderNumber, ref)
                    Session("flash_msg") = r.Message
                    Session("flash_ok") = r.Success
                    Response.Redirect("/App/OrderDetail.aspx?id=" & o.Id.ToString(), True)
                End If
                If Request.QueryString("cancel") = "1" Then
                    Dim r As ServiceResult = _orders.UpdateOrderState(o.Id, "CANCELLED")
                    Session("flash_msg") = r.Message
                    Session("flash_ok") = r.Success
                    Response.Redirect("/App/OrderDetail.aspx?id=" & o.Id.ToString(), True)
                End If

                Render(o)
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the order: " & ex.Message)
            End Try
        End Sub

        Private Function ResolveOrder() As Order
            Dim idParam As String = Trim(CStr(Request.QueryString("id")))
            If idParam.StartsWith("SD-", StringComparison.OrdinalIgnoreCase) Then
                Return _orders.GetOrderByNumber(idParam)
            End If
            Dim id As Integer = 0
            Integer.TryParse(idParam, id)
            If id > 0 Then Return _orders.GetOrder(id)
            Return _orders.GetOrderByNumber(idParam)
        End Function

        Private Sub Render(o As Order)
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
            sb.Append("<ul class=""timeline"">")
            Dim steps As String()() = o.StatusTimeline
            For Each st As String() In steps
                Dim state As String = st(2)
                Dim cls As String = If(state = "DONE", " done", If(state = "NOW", " now", ""))
                sb.Append("<li class=""" & cls.Trim() & """><b>" & WebUi.Esc(st(0)) & "</b> — " & WebUi.Esc(st(1)) & "</li>")
            Next
            sb.Append("</ul>")
            If o.Status = "SHIPPED" Then
                sb.Append("<div class=""card"" style=""background:var(--surface-low);margin-top:8px""><b>Courier:</b> handed to courier on " &
                          WebUi.Esc(o.UpdatedAt.ToString("MMM d, yyyy")) & " · expected within 3–5 business days.</div>")
            End If

            ' actions
            Dim canPay As Boolean = (o.PaymentStatus <> "PAID" AndAlso o.PaymentStatus <> "REFUNDED" AndAlso
                                     o.Status <> "CANCELLED" AndAlso Not String.Equals(o.PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase))
            Dim canCancel As Boolean = (o.Status = "PENDING")
            If canPay OrElse canCancel Then
                sb.Append("<div class=""frow"">")
                If canPay Then
                    sb.Append("<form method=""get"" action=""/App/OrderDetail.aspx"" style=""display:inline-flex;gap:8px"">")
                    sb.Append("<input type=""hidden"" name=""id"" value=""" & o.Id.ToString() & """>")
                    sb.Append("<input type=""hidden"" name=""pay"" value=""1"">")
                    sb.Append("<input name=""ref"" placeholder=""Payment reference (optional)"" style=""padding:8px;border:1px solid var(--line);border-radius:8px;width:220px"">")
                    sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">payments</span><span>Pay Now</span></button>")
                    sb.Append("</form>")
                End If
                If canCancel Then
                    sb.Append("<a class=""btn danger"" href=""/App/OrderDetail.aspx?id=" & o.Id.ToString() &
                              "&cancel=1"" data-confirm=""Cancel this order?"" data-confirm-danger""><span class=""ic ms"">cancel</span><span>Cancel Order</span></a>")
                End If
                sb.Append("</div>")
            End If
            sb.Append("</div>")

            ' details + items
            sb.Append("<div style=""flex:1.6;min-width:320px"">")
            sb.Append("<div class=""card mb""><div class=""kv"">")
            sb.Append("<dt>Ship to</dt><dd>" & WebUi.Esc(o.ShippingAddress) & "</dd>")
            sb.Append("<dt>Phone</dt><dd>" & WebUi.Esc(o.ContactPhone) & "</dd>")
            sb.Append("<dt>Payment</dt><dd>" & WebUi.Esc(DisplayPay(o.PaymentMethod)) & "</dd>")
            sb.Append("<dt>Pay status</dt><dd>" & WebUi.Badge(o.PaymentStatus) & "</dd>")
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
            sb.Append("<div style=""text-align:right;padding:6px 14px""><b>Order total: </b>" & WebUi.Money(o.TotalAmount) & "</div>")

            ' payments ledger
            Dim pays As List(Of Payment) = _orders.PaymentsForOrder(o.Id)
            If pays.Count > 0 Then
                sb.Append("<h3 style=""margin:14px 0 6px"">" & WebUi.Ic("receipt_long", "sm") & " Payments</h3>")
                For Each p As Payment In pays
                    Dim ptr As Receipt = _orders.ReceiptForPaymentId(p.Id)
                    sb.Append("<div class=""card"" style=""margin-bottom:8px"">" & WebUi.Esc(p.DisplayMethod) & " · " &
                              WebUi.Money(p.Amount) & " · " & WebUi.Esc(p.Status) & " · Ref " & WebUi.Esc(p.ReferenceNumber) &
                              If(p.PaidAt.HasValue, " · " & p.PaidAt.Value.ToString("MMM d, yyyy h:mm tt"), "") &
                              If(ptr IsNot Nothing, " &nbsp;<a href=""/App/Receipt.aspx?r=" & WebUi.Esc(ptr.ReceiptNumber) & """><span class=""ms sm"">receipt_long</span> Official Receipt</a>", "") & "</div>")
                Next
            End If
            sb.Append("</div></div>")

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
