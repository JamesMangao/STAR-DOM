Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class CatalogPage
        Inherits Page

        Protected Out As Literal

        Private ReadOnly _catalog As New CatalogService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                RenderPage()
            Catch ex As Exception
                STAR_DOM.Database.Db.LogError("Catalog", ex)
                Out.Text = WebUi.AlertBox("Could not load the catalog: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderPage()
            Dim catId As Integer = 0
            Integer.TryParse(Request.QueryString("cat"), catId)
            Dim q As String = Trim(CStr(Request.QueryString("q")))
            Dim categories As List(Of Category) = _catalog.ListCategories()
            Dim products As List(Of Product) = _catalog.ListProducts(catId, q)

            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Handcrafted Products & Art", "CATALOG DISCOVERY",
                                    "Browse every SKU in the atelier — search, filter by category, and add to your cart."))

            sb.Append("<div class=""row space-between mb"">")
            If q <> "" Then
                sb.Append("<div class=""sub"">Results for <b>" & WebUi.Esc(q) & "</b> — <a href=""/App/Catalog.aspx"" style=""color:var(--primary)"">clear</a></div>")
            Else
                sb.Append("<div class=""sub"">" & products.Count.ToString() & " products available</div>")
            End If
            sb.Append("</div>")

            Dim chips As New StringBuilder()
            chips.Append(WebUi.OutLink("/App/Catalog.aspx" & If(q <> "", "?q=" & Server.UrlEncode(q), ""), "ALL", catId = 0))
            For Each c As Category In categories
                chips.Append(WebUi.OutLink("/App/Catalog.aspx?cat=" & c.Id.ToString() & If(q <> "", "&q=" & Server.UrlEncode(q), ""),
                                           c.Name.ToUpperInvariant(), catId = c.Id))
            Next
            sb.Append("<div class=""chips"">" & chips.ToString() & "</div>")

            If products.Count > 0 Then
                sb.Append("<div class=""grid cards4"">")
                For Each p As Product In products
                    sb.Append(ProductCard(p))
                Next
                sb.Append("</div>")
            Else
                sb.Append(WebUi.EmptyRow("No products match — try another category or search term."))
            End If

            Out.Text = sb.ToString()
        End Sub

        Private Function ProductCard(p As Product) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""pcard"">")
            sb.Append("<div style=""position:relative"">")
            sb.Append(WebUi.ProductImg(p.PrimaryImageFile, p.Id, p.Name, "height:185px"))
            sb.Append("<div class=""badges"">")
            sb.Append(If(p.BadgeLabel <> "", "<span class=""badge warn"">" & WebUi.Esc(p.BadgeLabel) & "</span>",
                         If(p.HasDiscount, "<span class=""badge live"">" & p.DiscountPercent.ToString() & "% OFF</span>", "")))
            sb.Append("</div></div>")
            sb.Append("<div class=""pbody"">")
            sb.Append("<span class=""brand"">" & WebUi.Esc(p.BrandName) & "</span>")
            sb.Append("<a class=""pname"" href=""/App/Product.aspx?id=" & p.Id.ToString() & """ style=""color:inherit"">" & WebUi.Esc(p.Name) & "</a>")
            sb.Append("<span class=""pdesc"">" & WebUi.Esc(p.MaterialDetails) & "</span>")
            If p.RatingCount > 0 Then
                sb.Append("<span class=""stars"">" & WebUi.Stars(CInt(Math.Round(p.RatingAvg))) &
                          " <small style=""color:var(--ink-soft)"">(" & p.RatingCount.ToString() & ")</small></span>")
            End If
            sb.Append("<div class=""pfoot"">")
            sb.Append(WebUi.Money(p.EffectivePrice))
            sb.Append(WebUi.BtnHref("/App/Cart.aspx?add=" & p.Id.ToString() & "&q=1&ret=" & Server.UrlEncode(Request.RawUrl), "Add", "primary", "add_shopping_cart"))
            sb.Append("</div>")
            sb.Append(If(p.StockQuantity > 0,
                         "<span class=""stockline"">" & p.StockQuantity.ToString() & " in booth stock</span>",
                         "<span class=""stockline"" style=""color:var(--primary)"">Out of stock</span>"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

    End Class

End Namespace
