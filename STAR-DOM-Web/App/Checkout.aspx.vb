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

            Dim pm As String = Convert.ToString(Request.Form("pm"))
            If pm = "" Then pm = "COD"
            Dim addr As String = Convert.ToString(Request.Form("address"))
            Dim phone As String = Convert.ToString(Request.Form("phone"))
            Dim notes As String = Convert.ToString(Request.Form("notes"))

            Dim result As ServiceResult = _orders.Checkout(pm, addr, phone, notes)
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
                                    "Choose how to pay. Payments are simulated for this demo — no real charge is made."))

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
            sb.Append("<div class=""row space-between""><b>Total</b><b style=""color:var(--primary);font-size:18px"">" &
                      WebUi.Money(_cart.Subtotal()) & "</b></div>")
            sb.Append("</div>")

            ' shipping details
            sb.Append("<div class=""card mb""><h3 style=""margin-bottom:10px"">Delivery details</h3>")
            sb.Append("<div class=""field""><label for=""ad"">Shipping address</label><input id=""ad"" name=""address"" required placeholder=""House / street, city, province""></div>")
            sb.Append("<div class=""field""><label for=""ph"">Contact phone</label><input id=""ph"" name=""phone"" required placeholder=""09xx xxx xxxx""></div>")
            sb.Append("<div class=""field""><label for=""nt"">Order notes (optional)</label><textarea id=""nt"" name=""notes"" style=""min-height:70px""></textarea></div>")
            sb.Append("</div>")

            ' payment
            sb.Append("<div class=""card""><h3 style=""margin-bottom:10px"">" & WebUi.Ic("payments", "sm") & " Payment method</h3>")
            sb.Append(PayOption("GCASH", "GCash", "Pay instantly via the GCash app QR", "qr_code_2"))
            sb.Append(PayOption("MAYA", "Maya", "Pay with Maya wallet / card", "account_balance_wallet"))
            sb.Append(PayOption("CARD", "Card", "Credit or debit card (demo gateway)", "credit_card"))
            sb.Append(PayOption("COD", "Cash on Delivery", "Pay when your order arrives", "local_shipping"))
            sb.Append("<div class=""frow"">")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">shopping_bag_checkout</span><span>Place Order</span></button>")
            sb.Append(WebUi.BtnHref("/App/Cart.aspx", "Back to Cart", "ghost", "arrow_back"))
            sb.Append("</div>")
            sb.Append("</div>")
            sb.Append("</form>")
            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function PayOption(value As String, title As String, hint As String, icon As String) As String
            Dim checkedAttr As String = If(value = "COD", " checked", "")
            Return "<label class=""card"" style=""display:flex;gap:12px;align-items:flex-start;margin-bottom:8px;cursor:pointer"">" &
                   "<input type=""radio"" name=""pm"" value=""" & value & "\""" & checkedAttr & " style=""margin-top:3px"">" &
                   "<span class=""ms"" style=""color:var(--primary)"">" & WebUi.Esc(icon) & "</span>" &
                   "<span><b>" & WebUi.Esc(title) & "</b><br><span class=""sub"" style=""font-size:12px"">" & WebUi.Esc(hint) & "</span></span></label>"
        End Function

    End Class

End Namespace
