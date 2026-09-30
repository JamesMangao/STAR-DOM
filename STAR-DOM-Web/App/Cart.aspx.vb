Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class CartPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _cart As New CartService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            HandleActions()
            If Guard.IsPost() Then
                HandlePost()
                Response.Redirect("/App/Cart.aspx", True)
            End If
            RenderCart()
        End Sub

        Private Sub HandleActions()
            Dim ret As String = Request.QueryString("ret")
            If ret = "" OrElse Not ret.StartsWith("/", StringComparison.Ordinal) Then ret = "/App/Cart.aspx"

            If Request.QueryString("add") <> "" Then
                Dim pid As Integer = 0
                Integer.TryParse(Request.QueryString("add"), pid)
                Dim q As Integer = 1
                Integer.TryParse(Request.QueryString("q"), q)
                If q < 1 Then q = 1
                If pid > 0 Then
                    Dim r As ServiceResult = _cart.Add(pid, q)
                    Session("flash_msg") = r.Message
                    Session("flash_ok") = r.Success
                End If
                Response.Redirect(ret, True)
            End If
            If Request.QueryString("wl") <> "" Then
                Dim pid As Integer = 0
                Integer.TryParse(Request.QueryString("wl"), pid)
                If pid > 0 Then _cart.ToggleWishlist(pid)
                Response.Redirect(ret, True)
            End If
            If Request.QueryString("move") <> "" Then
                Dim pid As Integer = 0
                Integer.TryParse(Request.QueryString("move"), pid)
                If pid > 0 Then _cart.MoveToCart(pid)
                Response.Redirect(ret, True)
            End If
            If Request.QueryString("remove") <> "" Then
                Dim id As Integer = 0
                Integer.TryParse(Request.QueryString("remove"), id)
                If id > 0 Then _cart.Remove(id)
                Response.Redirect("/App/Cart.aspx", True)
            End If
            If Request.QueryString("rw") <> "" Then
                Dim pid As Integer = 0
                Integer.TryParse(Request.QueryString("rw"), pid)
                If pid > 0 Then _cart.RemoveWishlist(pid)
                Response.Redirect("/App/Cart.aspx", True)
            End If
            If Request.QueryString("clear") = "1" Then
                _cart.Clear()
                Response.Redirect("/App/Cart.aspx", True)
            End If
        End Sub

        Private Sub HandlePost()
            Dim keys As String() = Request.Form.AllKeys
            If keys Is Nothing Then Return
            For Each k As String In keys
                If k IsNot Nothing AndAlso k.StartsWith("qty_", StringComparison.Ordinal) Then
                    Dim id As Integer = 0
                    Integer.TryParse(k.Substring(4), id)
                    If id > 0 Then
                        Dim q As Integer = 1
                        Integer.TryParse(Request.Form(k), q)
                        If q < 1 Then q = 1
                        If q > 99 Then q = 99
                        _cart.UpdateQuantity(id, q)
                    End If
                End If
            Next
        End Sub

        Private Sub RenderCart()
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append(WebUi.Section("Shopping Cart & Wishlist", "MY BAG",
                                    "Review quantities, then continue to secure checkout."))

            Dim items As List(Of CartItem) = _cart.ListItems()
            If items.Count = 0 Then
                sb.Append("<div class=""empty"">Your cart is empty. <a href=""/App/Catalog.aspx"" style=""color:var(--primary);font-weight:700;display:inline-flex;align-items:center;gap:4px"">Browse the catalog <span class=""ms sm"">arrow_forward</span></a></div>")
            Else
                sb.Append("<form method=""post"" action=""/App/Cart.aspx"">")
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"PRODUCT", "PRICE", "QUANTITY", "TOTAL", ""}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each it As CartItem In items
                    sb.Append("<tr>")
                    sb.Append("<td><div style=""display:flex;gap:10px;align-items:center"">" &
                              WebUi.ProductImg(it.ImageFile, it.ProductId, it.ProductName, "width:52px;height:52px;border-radius:10px;flex-shrink:0") &
                              "<div><a href=""/App/Product.aspx?id=" & it.ProductId.ToString() & """ style=""font-weight:700"">" &
                              WebUi.Esc(it.ProductName) & "</a><br><span class=""sub"" style=""font-size:11px"">" &
                              WebUi.Esc(it.ProductSku) & "</span></div></div></td>")
                    sb.Append("<td>" & WebUi.Money(it.UnitPrice) & "</td>")
                    sb.Append("<td><input type=""number"" min=""1"" max=""99"" name=""qty_" & it.Id.ToString() &
                              """ value=""" & it.Quantity.ToString() & """ style=""width:70px;padding:6px;border:1px solid var(--line);border-radius:8px""></td>")
                    sb.Append("<td>" & WebUi.Money(it.LineTotal) & "</td>")
                    sb.Append("<td class=""rowact""><a href=""/App/Cart.aspx?remove=" & it.Id.ToString() & """>Remove</a></td>")
                    sb.Append("</tr>")
                Next
                sb.Append("</tbody></table></div>")
                sb.Append("<div class=""frow"">")
                sb.Append("<button class=""btn ghost"" type=""submit""><span class=""ic ms"">update</span><span>Update Quantities</span></button>")
                sb.Append(WebUi.BtnHref("/App/Cart.aspx?clear=1", "Empty Cart", "ghost", "delete_sweep"))
                sb.Append("</div></form>")
                sb.Append("<div class=""card"" style=""max-width:340px;margin-top:8px"">")
                sb.Append("<div class=""kv""><dt>Subtotal</dt><dd>" & WebUi.Money(_cart.Subtotal()) & "</dd></div>")
                sb.Append("<div class=""kv""><dt>Shipping</dt><dd class=""sub"">Calculated at checkout</dd></div>")
                sb.Append("<div class=""frow"">" & WebUi.BtnHref("/App/Checkout.aspx", "Proceed to Checkout", "primary", "lock") & "</div>")
                sb.Append("</div>")
            End If

            ' wishlist
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

    End Class

End Namespace
