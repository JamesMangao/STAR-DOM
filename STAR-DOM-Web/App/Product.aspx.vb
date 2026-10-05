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
            ' The art frame crops to fill by default (background-size: cover), which is
            ' right for a square card and wrong for the one place a shopper decides
            ' whether to buy: here the whole picture has to be visible, letterboxed
            ' rather than cut. Clicking it opens the original at full size.
            Dim fullPic As String = WebUi.AssetUrl(p.PrimaryImageFile)
            sb.Append("<a href=""" & WebUi.Attr(fullPic) & """ target=""_blank"" rel=""noopener"" " &
                      "style=""display:block;border-radius:16px"" title=""View the full picture"">")
            sb.Append(WebUi.ProductImg(p.PrimaryImageFile, p.Id, p.Name,
                                        "height:360px;border-radius:16px;background-size:contain"))
            sb.Append("</a>")
            If fullPic <> "" Then
                sb.Append("<div class=""sub"" style=""font-size:11px;margin-top:6px;text-align:center"">" &
                          WebUi.Ic("zoom_out_map", "sm") & " Tap the picture to view it full size</div>")
            End If
            ' No "bazaar price" banner: the displayed price is always BasePrice. Any
            ' discount is a bundle group that CartService applies once the items are
            ' in the cart, so there is nothing to advertise here.
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

            ' No bundle price callout: the discount is a cart-time effect, so advertising
            ' the struck-through group total here would contradict the price shown
            ' just below. The BUNDLE badge on the catalog card is enough of a hint.

            Dim stockText As String = If(p.StockQuantity > 0,
                                         If(p.StockQuantity <= p.LowStockThreshold,
                                            "<span style=""color:var(--primary);font-weight:700"">Only " & p.StockQuantity.ToString() & " remaining!</span>",
                                            p.StockQuantity.ToString() & " in booth stock"),
                                         "<span style=""color:var(--primary);font-weight:700"">Out of stock</span>")
            sb.Append("<div class=""frow"">")
            sb.Append("<div class=""money"" style=""font-size:26px"">" & WebUi.Money(p.BasePrice) & "</div>")
            sb.Append("<div class=""stockline"">" & stockText & "</div>")
            sb.Append("</div>")

            sb.Append("<div class=""frow"">")
            If p.InStock Then
                sb.Append(WebUi.AddCartButton(p, Server.UrlEncode("/App/Product.aspx?id=" & p.Id.ToString()), "Add to Cart"))
            End If
            Dim wlText As String = If(_cart.InWishlist(p.Id), "Remove from Wishlist", "Add to Wishlist")
            sb.Append(WebUi.BtnHref("/App/Cart.aspx?wl=" & p.Id.ToString() & "&ret=" &
                                    Server.UrlEncode("/App/Product.aspx?id=" & p.Id.ToString()), wlText, "ghost", "favorite",
                                    WebUi.AuthGateAttrs(p.Name, p.PrimaryImageFile, p.Id, p.BasePrice)))
            sb.Append("</div>")
            sb.Append("</div></div>")

            ' reviews
            Dim reviews As List(Of Review) = _orders.ReviewsForProduct(p.Id)
            sb.Append("<div class=""sec-head"" style=""margin-top:36px""><div>")
            sb.Append("<div class=""eyebrow"">REVIEWS &amp; RATINGS</div>")
            sb.Append("<h2>Customer Reviews</h2></div>")
            sb.Append("<div class=""sub"">" & reviews.Count.ToString() & " verified review(s)</div></div>")

            If reviews.Count > 0 Then
                sb.Append("<div class=""grid"" style=""gap:14px;grid-template-columns:repeat(auto-fill, minmax(320px, 1fr))"">")
                For Each r As Review In reviews
                    sb.Append("<div class=""card"" style=""border-radius:14px;padding:16px 18px;background:var(--surface);border:1px solid var(--line);box-shadow:var(--sh-1);display:flex;flex-direction:column;justify-content:space-between"">")
                    sb.Append("<div>")
                    sb.Append("<div class=""row space-between"" style=""align-items:center;margin-bottom:8px"">")
                    sb.Append("<div style=""display:flex;align-items:center;gap:8px""><div style=""width:30px;height:30px;border-radius:50%;background:var(--surface-mid);color:var(--primary);display:flex;align-items:center;justify-content:center;font-weight:800;font-size:12px"">" & WebUi.Esc(If(r.CustomerName.Length > 0, r.CustomerName.Substring(0, 1).ToUpperInvariant(), "U")) & "</div><b style=""font-size:13.5px"">" & WebUi.Esc(r.CustomerName) & "</b></div>")
                    sb.Append("<span class=""badge live"" style=""font-size:10px;padding:2px 8px"">" & WebUi.Ic("verified", "sm") & " Verified Buyer</span>")
                    sb.Append("</div>")
                    sb.Append("<div style=""color:#f59e0b;font-size:15px;margin-bottom:8px"">" & WebUi.Stars(r.Rating) & "</div>")
                    sb.Append("<p style=""margin:0;font-size:13.5px;line-height:1.5;color:var(--ink)"">" & WebUi.Esc(r.Comment) & "</p>")
                    sb.Append("</div>")
                    sb.Append("<div class=""sub"" style=""font-size:11px;margin-top:14px;border-top:1px solid var(--line);padding-top:8px"">" & WebUi.Esc(r.CreatedAt.ToString("MMMM d, yyyy")) & "</div>")
                    sb.Append("</div>")
                Next
                sb.Append("</div>")
            Else
                sb.Append(WebUi.EmptyRow("No reviews yet — be the first to share your experience with this item."))
            End If

            ' The review box opens once this shopper has confirmed the parcel arrived, not
            ' merely because they once bought the item: a review of goods still in
            ' transit says nothing about the goods.
            Dim receivedOrder As Integer? = _orders.PurchaseOrderId(p.Id)
            Dim canReview As Boolean = receivedOrder.HasValue AndAlso Not _orders.HasReviewed(p.Id)
            If canReview Then
                sb.Append("<div class=""card"" id=""review"" style=""margin-top:24px;max-width:680px;border-radius:16px;padding:24px;border:1.5px solid var(--line);box-shadow:var(--sh-2);background:linear-gradient(180deg, #ffffff 0%, var(--surface-low) 100%)"">")
                sb.Append("<div style=""display:flex;align-items:center;gap:10px;margin-bottom:14px"">")
                sb.Append("<div class=""ph-ic"" style=""width:36px;height:36px;font-size:18px;background:var(--yellow);color:var(--on-yellow);border-radius:10px"">" & WebUi.Ic("rate_review", "sm") & "</div>")
                sb.Append("<div><h3 style=""margin:0;font-size:17px;font-weight:800;letter-spacing:-.3px"">Write a Review</h3><p class=""sub"" style=""margin:2px 0 0;font-size:12px"">Share your genuine feedback with other shoppers and creators</p></div>")
                sb.Append("</div>")

                sb.Append("<form method=""post"" action=""/App/Product.aspx?id=" & p.Id.ToString() & """>")
                sb.Append("<div class=""field"" style=""margin-bottom:16px""><label style=""font-weight:700;font-size:12.5px;color:var(--ink-soft);text-transform:uppercase;letter-spacing:.05em"">Rating</label>")
                sb.Append("<div style=""display:flex;gap:12px;align-items:center;margin-top:4px"">")
                sb.Append("<select name=""rating"" id=""ratingSelect"" style=""padding:10px 14px;border-radius:10px;border:1.5px solid var(--line);font-size:14px;font-weight:700;background:var(--surface);color:var(--ink);cursor:pointer;outline:none;min-width:160px"">")
                sb.Append("<option value=""5"" selected>★★★★★ (5 - Excellent)</option>")
                sb.Append("<option value=""4"">★★★★☆ (4 - Very Good)</option>")
                sb.Append("<option value=""3"">★★★☆☆ (3 - Good)</option>")
                sb.Append("<option value=""2"">★★☆☆☆ (2 - Fair)</option>")
                sb.Append("<option value=""1"">★☆☆☆☆ (1 - Poor)</option>")
                sb.Append("</select>")
                sb.Append("<span style=""font-size:12px;color:var(--ink-soft)"">Your honest star rating</span>")
                sb.Append("</div></div>")

                sb.Append("<div class=""field"" style=""margin-bottom:18px""><label style=""font-weight:700;font-size:12.5px;color:var(--ink-soft);text-transform:uppercase;letter-spacing:.05em"">Your Review &amp; Experience</label>")
                sb.Append("<textarea name=""comment"" required placeholder=""What did you like about this product? How is the quality, packaging, and design?"" style=""width:100%;min-height:100px;border-radius:12px;border:1.5px solid var(--line);padding:12px 14px;font-size:13.5px;font-family:inherit;box-sizing:border-box;margin-top:4px;outline:none;line-height:1.5;resize:vertical""></textarea></div>")

                sb.Append("<div style=""display:flex;justify-content:flex-end;align-items:center;gap:12px"">")
                sb.Append("<button class=""btn primary"" type=""submit"" style=""padding:10px 22px;border-radius:10px;font-weight:700;display:inline-flex;align-items:center;gap:8px;box-shadow:var(--sh-1)""><span class=""ic ms"">send</span><span>Submit Review</span></button>")
                sb.Append("</div>")
                sb.Append("</form></div>")
            ElseIf _orders.HasReviewed(p.Id) Then
                sb.Append("<div class=""card"" style=""margin-top:18px;max-width:680px;border-radius:12px;padding:14px 18px;background:var(--surface-low);border:1px solid var(--line);display:flex;align-items:center;gap:10px"">")
                sb.Append("<span class=""ms sm"" style=""color:var(--green);font-size:20px"">task_alt</span>")
                sb.Append("<span style=""font-size:13px;color:var(--ink-soft)"">You have already submitted a review for this product. Thank you for your feedback!</span></div>")
            Else
                sb.Append("<div class=""card"" style=""margin-top:18px;max-width:680px;border-radius:12px;padding:14px 18px;background:var(--surface-low);border:1px solid var(--line);display:flex;align-items:center;gap:10px"">")
                sb.Append("<span class=""ms sm"" style=""color:var(--ink-soft);font-size:20px"">verified_user</span>")
                sb.Append("<span style=""font-size:13px;color:var(--ink-soft)"">Only verified buyers can review this item, and only after you confirm you received it from <a href=""/App/Orders.aspx"" style=""color:var(--primary);font-weight:700"">My Orders</a>.</span></div>")
            End If

            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
