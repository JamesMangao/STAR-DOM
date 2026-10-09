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
        Private ReadOnly _assets As New AssetImageRepository()
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

            p.Name = Trim(Convert.ToString(Request.Form("name")))
            p.Sku = Trim(Convert.ToString(Request.Form("sku"))).ToUpperInvariant()
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

            Try
                Dim productId As Integer
                If _editingId > 0 Then
                    productId = _editingId
                    _products.Update(p)
                Else
                    productId = _products.Create(p)
                End If
                SaveImages(productId)
                Session("flash_msg") = "Product saved."
                Session("flash_ok") = True
                Response.Redirect("/App/Merchant/Products.aspx", True)
            Catch ex As Exception
                RenderForm(ex.Message, Convert.ToString(Request.Form("name")))
            End Try
        End Sub

        ''' <summary>
        ''' Applies the image edits from one save: removals first (the hidden
        ''' delimg field carries the ticked ids), then the new uploads. Bytes go
        ''' into AssetImages so they survive a redeploy; the first image on an
        ''' otherwise-empty product becomes its primary.
        ''' </summary>
        Private Sub SaveImages(productId As Integer)
            Dim delIds As String = Convert.ToString(Request.Form("delimg"))
            If Not String.IsNullOrWhiteSpace(delIds) Then
                For Each part As String In delIds.Split(","c)
                    Dim imageId As Integer
                    If Integer.TryParse(part.Trim(), imageId) AndAlso imageId > 0 Then _products.DeleteImage(imageId)
                Next
            End If

            Dim count As Integer = _products.ListImages(productId).Count
            If Request.Files Is Nothing Then Return
            For i As Integer = 0 To Request.Files.Count - 1
                Dim f As System.Web.HttpPostedFile = Request.Files(i)
                If f Is Nothing OrElse f.ContentLength = 0 OrElse String.IsNullOrWhiteSpace(f.FileName) Then Continue For
                Dim bytes As Byte() = ReadPostedFile(f)
                Dim path As String = _assets.Save("Uploads/products", f.FileName, bytes, f.ContentType)
                If path = "" Then Continue For
                _products.AddImage(productId, path, count = 0, count)
                count += 1
            Next
        End Sub

        ''' <summary>Reads an uploaded file fully into memory so it can be written to the database.</summary>
        Private Shared Function ReadPostedFile(f As System.Web.HttpPostedFile) As Byte()
            Using ms As New System.IO.MemoryStream()
                f.InputStream.CopyTo(ms)
                Return ms.ToArray()
            End Using
        End Function

        Private Sub RenderForm(errorMsg As String, keepName As String)
            Dim p As Product = Nothing
            If _editingId > 0 Then p = _products.GetById(_editingId)

            Dim sb As New StringBuilder()
            If errorMsg <> "" Then sb.Append(WebUi.AlertBox(errorMsg))
            sb.Append("<a href=""/App/Merchant/Products.aspx"" class=""sub"">← Products</a>")
            sb.Append(WebUi.Section(If(p Is Nothing, "Add Product", "Edit Product — " & p.Name), "PRODUCT & STOCK",
                                    "Name, price, stock, photos and shelf flags all sync to the marketplace."))

            sb.Append("<form method=""post"" enctype=""multipart/form-data"" action=""/App/Merchant/ProductEdit.aspx" & If(_editingId > 0, "?id=" & _editingId.ToString(), "") & """>")
            ' Nested inside the shell form, which the browser closes at this tag — so the
            ' shell's token is not submitted with this form. Carry its own.
            sb.Append(STAR_DOM.Web.Csrf.HiddenField())
            sb.Append("<div class=""card"" style=""max-width:820px"">")
            sb.Append("<div class=""form-grid2"">")
            sb.Append(Field("name", "Product name *", If(p IsNot Nothing, p.Name, keepName), True))
            sb.Append(Field("sku", "SKU *", If(p IsNot Nothing, p.Sku, ""), True))
            sb.Append(Field("brand", "Brand / studio", If(p IsNot Nothing, p.BrandName, ""), False))
            sb.Append(Field("badge", "Badge label (e.g. NEW, LIMITED)", If(p IsNot Nothing, p.BadgeLabel, ""), False))
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

            ' ---- Product photos ----
            sb.Append("<div class=""field""><label>Product photos</label>")
            If p IsNot Nothing Then
                Dim imgs As List(Of ProductImage) = _products.ListImages(p.Id)
                If imgs.Count > 0 Then
                    sb.Append("<div style=""display:flex;gap:10px;flex-wrap:wrap;margin-bottom:10px"">")
                    For Each im In imgs
                        sb.Append("<label style=""display:block;cursor:pointer;position:relative"">")
                        sb.Append("<img src=""" & WebUi.Attr(WebUi.AssetUrl(im.ImageFile)) & """ alt="""" " &
                                  "style=""width:110px;height:110px;object-fit:cover;border:1px solid var(--line);border-radius:10px;display:block"">")
                        If im.IsPrimary Then
                            sb.Append("<span class=""badge"" style=""position:absolute;top:6px;left:6px"">Primary</span>")
                        End If
                        sb.Append("<span style=""display:flex;gap:5px;align-items:center;margin-top:5px;font-size:11.5px"">")
                        sb.Append("<input type=""checkbox"" name=""delimg"" value=""" & im.Id.ToString() & """ style=""width:auto;margin:0""> Remove")
                        sb.Append("</span></label>")
                    Next
                    sb.Append("</div>")
                End If
            End If
            sb.Append("<input type=""file"" name=""img"" accept=""image/*"" multiple>")
            sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-top:4px"">JPG, PNG, GIF or WebP · up to 6 MB each. " &
                      "The first photo becomes the primary image; tick Remove to delete one.</div>")
            sb.Append("</div>")

            sb.Append("<div class=""frow"">")
            sb.Append(Checkbox("isActive", "Active on marketplace", p Is Nothing OrElse p.IsActive))
            sb.Append(Checkbox("featured", "Featured", p IsNot Nothing AndAlso p.IsFeatured))
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
            ' No backslash before the closing quote: the old "& "\""" emitted
            ' value="Sticker\", so every field submitted with a trailing "\" and the
            ' saved product carried it. Same bug as EventEdit's Field().
            Return "<div class=""field""><label for=""" & name & """>" & WebUi.Esc(label) & "</label>" &
                   "<input id=""" & name & """ name=""" & name & """ value=""" & WebUi.Attr(value) & """" &
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
