Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class OrdersPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _orders As New OrderService()
        Private ReadOnly _cart As New CartService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                ' This page is a list, not a control panel: cancelling and reviewing
                ' both happen on the order's own page, which is the one place that has
                ' the order's state in front of it. So there is no mutating handler
                ' here at all -- every row's only action is "View / Track".
                RenderOrders()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load your orders: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderOrders()
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append(WebUi.Section("My Orders & Wishlist", "ORDER HISTORY",
                                    "Track every order from placement to delivery."))

            Dim page As Integer = 1
            Integer.TryParse(Convert.ToString(Request.QueryString("p")), page)
            If page < 1 Then page = 1
            Const PageSize As Integer = 20

            Dim totalOrders As Integer = _orders.CountMyOrders()
            Dim orders As List(Of Order) = _orders.ListMyOrders(page, PageSize)
            If orders.Count = 0 AndAlso totalOrders > 0 Then
                page = CInt(Math.Ceiling(totalOrders / PageSize))
                orders = _orders.ListMyOrders(page, PageSize)
            End If
            If orders.Count = 0 Then
                sb.Append("<div class=""empty"">You haven't placed any orders yet. <a href=""/App/Catalog.aspx"" style=""color:var(--primary);font-weight:700;display:inline-flex;align-items:center;gap:4px"">Start shopping <span class=""ms sm"">arrow_forward</span></a></div>")
            Else
                sb.Append(WebUi.Pager(totalOrders, PageSize, page, "/App/Orders.aspx?p={P}"))
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"ORDER #", "DATE", "ITEMS", "TOTAL", "PAYMENT", "PAY STATE", "STATUS", ""}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each o As Order In orders
                    sb.Append("<tr>")
                    sb.Append("<td><b>" & WebUi.Esc(o.OrderNumber) & "</b></td>")
                    sb.Append("<td>" & WebUi.Esc(o.CreatedAt.ToString("MMM d, yyyy")) & "</td>")
                    sb.Append("<td>" & o.ItemCount.ToString() & "</td>")
                    sb.Append("<td>" & WebUi.Money(o.TotalAmount) & "</td>")
                    sb.Append("<td>" & WebUi.Esc(DisplayPay(o.PaymentMethod)) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.PaymentStatus) & "</td>")
                    sb.Append("<td>" & WebUi.Badge(o.Status) & "</td>")
                    ' Cancel Order and the review queue both live inside View / Track,
                    ' so the row does not repeat them: an order's own page is the one
                    ' place that can see what state the order is in, and two entry
                    ' points to the same state change is one too many to keep in step.
                    sb.Append("<td class=""rowact""><a href=""/App/OrderDetail.aspx?id=" & o.Id.ToString() & """>View / Track</a></td>")
                Next
                sb.Append("</tbody></table></div>")
            End If

            ' Wishlist lives alongside orders so saved products are not hidden on a
            ' separate page. Move/remove reuse the same Cart.aspx actions.
            Dim wish As List(Of WishlistItem) = _cart.ListWishlist()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px""><div>")
            sb.Append("<div class=""eyebrow"">SAVED FOR LATER</div>")
            sb.Append("<h2>Wishlist</h2></div>")
            sb.Append("<div class=""sub"">" & wish.Count.ToString() & " item(s) saved</div></div>")
            If wish.Count > 0 Then
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"PRODUCT", "PRICE", "AVAILABILITY", ""}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each w As WishlistItem In wish
                    sb.Append("<tr>")
                    sb.Append("<td><div style=""display:flex;gap:10px;align-items:center"">" &
                              WebUi.ProductImg(w.ImageFile, w.ProductId, w.ProductName, "width:44px;height:44px;border-radius:9px;flex-shrink:0") &
                              "<a href=""/App/Product.aspx?id=" & w.ProductId.ToString() & """ style=""font-weight:700"">" &
                              WebUi.Esc(w.ProductName) & "</a></div></td>")
                    sb.Append("<td>" & WebUi.Money(w.UnitPrice) & "</td>")
                    sb.Append("<td>" & If(w.InStock, "<span class=""badge live"">In stock</span>", "<span class=""badge muted"">Out of stock</span>") & "</td>")
                    sb.Append("<td class=""rowact""><a href=""/App/Cart.aspx?move=" & w.ProductId.ToString() & """>Move to cart</a>" &
                              "<a href=""/App/Cart.aspx?rw=" & w.ProductId.ToString() & """>Remove</a></td>")
                    sb.Append("</tr>")
                Next
                sb.Append("</tbody></table></div>")
            Else
                sb.Append("<div class=""empty"">Nothing saved yet. Tap <span class=""ms sm"" style=""color:var(--primary)"">favorite</span> on any product to keep it here.</div>")
            End If

            Out.Text = sb.ToString()
        End Sub

        Private Function DisplayPay(pm As String) As String
            Return WebUi.ChannelBrand(pm)
        End Function

    End Class

End Namespace
