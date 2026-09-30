Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class ProductPage
        Inherits Page

        Protected Out As Literal

        Private ReadOnly _catalog As New CatalogService()
        Private ReadOnly _orders As New OrderService()
        Private ReadOnly _cart As New CartService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                Dim id As Integer = 0
                Integer.TryParse(Request.QueryString("id"), id)
                Dim p As Product = _catalog.GetProduct(id)
                If p Is Nothing Then
                    Out.Text = WebUi.AlertBox("Product not found.")
                    Return
                End If

                If Guard.IsPost() Then
                    Dim rating As Integer = 0
                    Integer.TryParse(Request.Form("rating"), rating)
                    Dim comment As String = Convert.ToString(Request.Form("comment"))
                    Dim result As ServiceResult = _orders.SubmitReview(p.Id, _orders.PurchaseOrderId(p.Id), rating, comment)
                    Session("flash_msg") = result.Message
                    Session("flash_ok") = result.Success
                    Response.Redirect("/App/Product.aspx?id=" & id.ToString(), True)
                End If
                RenderProduct(p)
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the product: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderProduct(p As Product)
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append("<a href=""/App/Catalog.aspx"" class=""sub"">← Back to Catalog</a>")
            sb.Append("<div class=""row"" style=""align-items:flex-start;gap:26px;margin-top:10px"">")

            ' left: art + price panel
            sb.Append("<div style=""flex:1;min-width:300px;max-width:430px"">")
            sb.Append(WebUi.ProductImg(p.PrimaryImageFile, p.Id, p.Name, "height:360px;border-radius:16px"))
            If p.HasDiscount Then
                sb.Append("<div class=""card"" style=""background:var(--yellow);border-color:#eec200;margin-top:12px;display:flex;justify-content:space-between;align-items:center"">")
                sb.Append("<div><span class=""k-label"" style=""font-weight:800;font-size:10px;letter-spacing:.1em"">BAZAAR PRICE</span><br>" &
                          "<span style=""font-size:24px;font-weight:800;color:var(--primary)"">" & WebUi.Money(p.EffectivePrice) & "</span>" &
                          " <s class=""sub"">" & WebUi.Money(p.BasePrice) & "</s></div>")
                sb.Append("<span class=""badge live"">-" & p.DiscountPercent.ToString() & "%</span></div>")
            End If
            sb.Append("</div>")

            ' right: details
            sb.Append("<div style=""flex:1.6;min-width:320px"">")
            sb.Append("<div class=""eyebrow"">" & WebUi.Esc(p.CategoryName) & " · " & WebUi.Esc(p.BrandName) & "</div>")
            sb.Append("<h1 style=""font-size:30px;letter-spacing:-.5px"">" & WebUi.Esc(p.Name) & "</h1>")
            sb.Append(WebUi.Pill(p.Sku, "yellow"))
            If p.RatingCount > 0 Then
                sb.Append(" " & WebUi.Stars(CInt(Math.Round(p.RatingAvg))) & " <span class=""sub"">" &
                          p.RatingCount.ToString() & " review(s)</span>")
            End If
            sb.Append("<p class=""sub"" style=""margin:14px 0;max-width:640px"">" & WebUi.Esc(p.Description) & "</p>")
            If p.MaterialDetails <> "" Then
                sb.Append("<div class=""card""><b style=""font-size:12px;letter-spacing:.05em"">MATERIALS &amp; DETAILS</b><br>" &
                          "<span class=""sub"">" & WebUi.Esc(p.MaterialDetails) & "</span></div>")
            End If

            Dim stockText As String = If(p.StockQuantity > 0,
                                         If(p.StockQuantity <= p.LowStockThreshold,
                                            "<span style=""color:var(--primary);font-weight:700"">Only " & p.StockQuantity.ToString() & " remaining!</span>",
                                            p.StockQuantity.ToString() & " in booth stock"),
                                         "<span style=""color:var(--primary);font-weight:700"">Out of stock</span>")
            sb.Append("<div class=""frow"">")
            sb.Append("<div class=""money"" style=""font-size:26px"">" & WebUi.Money(p.EffectivePrice) & "</div>")
            sb.Append("<div class=""stockline"">" & stockText & "</div>")
            sb.Append("</div>")

            sb.Append("<div class=""frow"">")
            If p.InStock Then
                sb.Append(WebUi.BtnHref("/App/Cart.aspx?add=" & p.Id.ToString() & "&q=1&ret=" &
                                        Server.UrlEncode("/App/Product.aspx?id=" & p.Id.ToString()), "Add to Cart", "primary", "add_shopping_cart"))
            End If
            Dim wlText As String = If(_cart.InWishlist(p.Id), "Remove from Wishlist", "Add to Wishlist")
            sb.Append(WebUi.BtnHref("/App/Cart.aspx?wl=" & p.Id.ToString() & "&ret=" &
                                    Server.UrlEncode("/App/Product.aspx?id=" & p.Id.ToString()), wlText, "ghost", "favorite"))
            sb.Append("</div>")
            sb.Append("</div></div>")

            ' reviews
            Dim reviews As List(Of Review) = _orders.ReviewsForProduct(p.Id)
            sb.Append("<div class=""sec-head"" style=""margin-top:34px""><div>")
            sb.Append("<div class=""eyebrow"">REVIEWS &amp; RATINGS</div>")
            sb.Append("<h2>Customer Reviews</h2></div>")
            sb.Append("<div class=""sub"">" & reviews.Count.ToString() & " approved review(s)</div></div>")

            If reviews.Count > 0 Then
                sb.Append("<div class=""flex-col"">")
                For Each r As Review In reviews
                    sb.Append("<div class=""card"">")
                    sb.Append("<div class=""row space-between"">")
                    sb.Append("<div><b>" & WebUi.Esc(r.CustomerName) & "</b> " & WebUi.Stars(r.Rating) & "</div>")
                    sb.Append("<span class=""sub"" style=""font-size:12px"">" & WebUi.Esc(r.CreatedAt.ToString("MMM d, yyyy")) & "</span>")
                    sb.Append("</div>")
                    sb.Append("<p style=""margin:8px 0 0"">" & WebUi.Esc(r.Comment) & "</p>")
                    sb.Append("</div>")
                Next
                sb.Append("</div>")
            Else
                sb.Append(WebUi.EmptyRow("No reviews yet — be the first to review this product."))
            End If

            Dim purchased As Integer? = _orders.PurchaseOrderId(p.Id)
            Dim canReview As Boolean = purchased.HasValue AndAlso Not _orders.HasReviewed(p.Id)
            If canReview Then
                sb.Append("<div class=""card"" style=""margin-top:18px;max-width:640px"">")
                sb.Append("<h3 style=""margin-bottom:6px"">Write a review</h3>")
                sb.Append("<form method=""post"" action=""/App/Product.aspx?id=" & p.Id.ToString() & """>")
                sb.Append("<div class=""field""><label>Your rating</label>")
                sb.Append("<select name=""rating"">")
                For i As Integer = 5 To 1 Step -1
                    sb.Append("<option value=""" & i.ToString() & """>" & i.ToString() & " star" & If(i > 1, "s", "") & "</option>")
                Next
                sb.Append("</select></div>")
                sb.Append("<div class=""field""><label>Comment</label><textarea name=""comment"" required></textarea></div>")
                sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">rate_review</span><span>Submit Review</span></button>")
                sb.Append("</form></div>")
            ElseIf purchased.HasValue Then
                sb.Append("<p class=""sub"" style=""margin-top:12px"">You already reviewed this product. Thanks!</p>")
            Else
                sb.Append("<p class=""sub"" style=""margin-top:12px"">Only verified purchasers can review this product.</p>")
            End If

            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
