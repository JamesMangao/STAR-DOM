Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Web

    Public Class ProductEditPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _products As New ProductRepository()
        Private ReadOnly _cats As New CategoryRepository()
        Private _editingId As Integer = 0

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                Integer.TryParse(Request.QueryString("id"), _editingId)
                If Guard.IsPost() Then
                    Save()
                    Return
                End If
                RenderForm("", "")
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the product form: " & ex.Message)
            End Try
        End Sub

        Private Sub Save()
            Dim p As Product
            If _editingId > 0 Then
                p = _products.GetById(_editingId)
                If p Is Nothing Then
                    RenderForm("Product not found.", "")
                    Return
                End If
                If p.MerchantId <> STAR_DOM.Helpers.Session.CurrentUser.Id AndAlso Not STAR_DOM.Helpers.Session.IsAdmin Then
                    RenderForm("Not your product.", "")
                    Return
                End If
            Else
                p = New Product()
                p.MerchantId = STAR_DOM.Helpers.Session.CurrentUser.Id
            End If

            p.Name = Trim(CStr(Request.Form("name")))
            p.Sku = Trim(CStr(Request.Form("sku"))).ToUpperInvariant()
            p.BrandName = Convert.ToString(Request.Form("brand"))
            p.MaterialDetails = Convert.ToString(Request.Form("material"))
            p.BadgeLabel = Convert.ToString(Request.Form("badge"))
            p.Description = Convert.ToString(Request.Form("description"))
            p.Slug = Slugify(p.Name)

            Dim catId As Integer = 0
            Integer.TryParse(Request.Form("cat"), catId)
            p.CategoryId = catId

            Dim price As Decimal = 0D
            Decimal.TryParse(Request.Form("price"), price)
            p.BasePrice = price
            Dim saleText As String = Convert.ToString(Request.Form("sale"))
            If saleText <> "" Then
                Dim sale As Decimal
                If Decimal.TryParse(saleText, sale) AndAlso sale > 0 Then p.SalePrice = sale Else p.SalePrice = Nothing
            Else
                p.SalePrice = Nothing
            End If

            Dim stock As Integer = 0
            Integer.TryParse(Request.Form("stock"), stock)
            p.StockQuantity = stock
            Dim th As Integer = 5
            Integer.TryParse(Request.Form("threshold"), th)
            p.LowStockThreshold = th

            p.IsActive = Request.Form("isActive") = "1"
            p.IsFeatured = Request.Form("featured") = "1"
            p.IsBoothExclusive = Request.Form("booth") = "1"
            p.IsEventExclusive = Request.Form("eventex") = "1"

            Try
                If _editingId > 0 Then
                    _products.Update(p)
                Else
                    _products.Create(p)
                End If
                Session("flash_msg") = "Product saved."
                Session("flash_ok") = True
                Response.Redirect("/App/Merchant/Products.aspx", True)
            Catch ex As Exception
                RenderForm(ex.Message, Convert.ToString(Request.Form("name")))
            End Try
        End Sub

        Private Sub RenderForm(errorMsg As String, keepName As String)
            Dim p As Product = Nothing
            If _editingId > 0 Then p = _products.GetById(_editingId)

            Dim sb As New StringBuilder()
            If errorMsg <> "" Then sb.Append(WebUi.AlertBox(errorMsg))
            sb.Append("<a href=""/App/Merchant/Products.aspx"" class=""sub"">← Products</a>")
            sb.Append(WebUi.Section(If(p Is Nothing, "Add Product", "Edit Product — " & p.Name), "PRODUCT & STOCK POS",
                                    "Name, price, stock and shelf flags sync to the marketplace and event booths."))

            sb.Append("<form method=""post"" action=""/App/Merchant/ProductEdit.aspx" & If(_editingId > 0, "?id=" & _editingId.ToString(), "") & """>")
            sb.Append("<div class=""card"" style=""max-width:820px"">")
            sb.Append("<div class=""form-grid2"">")
            sb.Append(Field("name", "Product name *", If(p IsNot Nothing, p.Name, keepName), True))
            sb.Append(Field("sku", "SKU *", If(p IsNot Nothing, p.Sku, ""), True))
            sb.Append(Field("brand", "Brand / studio", If(p IsNot Nothing, p.BrandName, ""), False))
            sb.Append(Field("badge", "Badge label (e.g. BOOTH EXCLUSIVE)", If(p IsNot Nothing, p.BadgeLabel, ""), False))
            sb.Append(Field("price", "Base price (₱)", If(p IsNot Nothing, p.BasePrice.ToString("0.00"), ""), True))
            sb.Append(Field("sale", "Sale price (₱, optional)", If(p IsNot Nothing AndAlso p.SalePrice.HasValue, p.SalePrice.Value.ToString("0.00"), ""), False))
            sb.Append(Field("stock", "Stock quantity", If(p IsNot Nothing, p.StockQuantity.ToString(), "0"), True))
            sb.Append(Field("threshold", "Low-stock alert at", If(p IsNot Nothing, p.LowStockThreshold.ToString(), "5"), True))
            sb.Append("</div>")
            sb.Append("<div class=""field""><label>Category</label><select name=""cat"">")
            For Each c As Category In _cats.ListActive()
                Dim sel As String = If(p IsNot Nothing AndAlso p.CategoryId = c.Id, " selected", "")
                sb.Append("<option value=""" & c.Id.ToString() & """" & sel & ">" & WebUi.Esc(c.Name) & "</option>")
            Next
            sb.Append("</select></div>")
            sb.Append(Field("material", "Materials & details", If(p IsNot Nothing, p.MaterialDetails, ""), False))
            sb.Append("<div class=""field""><label>Description</label><textarea name=""description"" style=""min-height:90px"">" &
                      WebUi.Esc(If(p IsNot Nothing, p.Description, "")) & "</textarea></div>")

            sb.Append("<div class=""frow"">")
            sb.Append(Checkbox("isActive", "Active on marketplace", p Is Nothing OrElse p.IsActive))
            sb.Append(Checkbox("featured", "Featured", p IsNot Nothing AndAlso p.IsFeatured))
            sb.Append(Checkbox("booth", "Booth exclusive", p IsNot Nothing AndAlso p.IsBoothExclusive))
            sb.Append(Checkbox("eventex", "Event exclusive", p IsNot Nothing AndAlso p.IsEventExclusive))
            sb.Append("</div>")
            sb.Append("<div class=""frow"">")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">save</span><span>Save Product</span></button>")
            sb.Append(WebUi.BtnHref("/App/Merchant/Products.aspx", "Cancel", "ghost", "close"))
            sb.Append("</div>")
            sb.Append("</div>")
            sb.Append("</form>")
            Out.Text = sb.ToString()
        End Sub

        Private Function Field(name As String, label As String, value As String, required As Boolean) As String
            Return "<div class=""field""><label for=""" & name & """>" & WebUi.Esc(label) & "</label>" &
                   "<input id=""" & name & """ name=""" & name & """ value=""" & WebUi.Attr(value) & "\""" &
                   If(required, " required", "") & "></div>"
        End Function

        Private Function Checkbox(name As String, label As String, checked As Boolean) As String
            Return "<label style=""display:flex;gap:6px;align-items:center""><input type=""checkbox"" name=""" & name &
                   """ value=""1""" & If(checked, " checked", "") & "> " & WebUi.Esc(label) & "</label>"
        End Function

        Private Function Slugify(name As String) As String
            Dim s As String = name.Trim().ToLowerInvariant()
            Dim sb As New StringBuilder()
            For Each ch As Char In s
                If Char.IsLetterOrDigit(ch) Then
                    sb.Append(ch)
                ElseIf sb.Length > 0 AndAlso sb.ToString().EndsWith("-") = False Then
                    sb.Append("-")
                End If
            Next
            Dim r As String = sb.ToString().Trim("-"c)
            If r = "" Then r = Guid.NewGuid().ToString("N").Substring(0, 8)
            Return r
        End Function

    End Class

End Namespace
