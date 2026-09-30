Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Web

    Public Class MerchantProductsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _products As New ProductRepository()
        Private ReadOnly _cats As New CategoryRepository()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                If Request.QueryString("del") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("del"), id)
                    If id > 0 Then
                        Dim err As String = _products.Delete(id)
                        Session("flash_msg") = If(err Is Nothing, "Product deleted.", err)
                        Session("flash_ok") = err Is Nothing
                    End If
                    Response.Redirect("/App/Merchant/Products.aspx", True)
                End If
                If Request.QueryString("toggle") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("toggle"), id)
                    Dim p As Product = _products.GetById(id)
                    If p IsNot Nothing Then
                        p.IsActive = Not p.IsActive
                        _products.Update(p)
                    End If
                    Response.Redirect("/App/Merchant/Products.aspx", True)
                End If
                If Request.QueryString("delcat") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("delcat"), id)
                    Dim err As String = _cats.Delete(id)
                    Session("flash_msg") = If(err Is Nothing, "Category deleted.", err)
                    Session("flash_ok") = err Is Nothing
                    Response.Redirect("/App/Merchant/Products.aspx", True)
                End If
                If Guard.IsPost() AndAlso Request.Form("catName") IsNot Nothing Then
                    Dim name As String = Convert.ToString(Request.Form("catName"))
                    If name.Trim() <> "" Then _cats.Create(name.Trim(), Convert.ToString(Request.Form("catDesc")), 0, True)
                    Session("flash_msg") = "Category added."
                    Session("flash_ok") = True
                    Response.Redirect("/App/Merchant/Products.aspx", True)
                End If
                If Guard.IsPost() AndAlso Request.Form("stockId") IsNot Nothing Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.Form("stockId"), id)
                    Dim q As Integer = 0
                    Integer.TryParse(Request.Form("stockQty"), q)
                    If id > 0 Then _products.SetStock(id, q)
                    Session("flash_msg") = "Stock updated."
                    Session("flash_ok") = True
                    Response.Redirect("/App/Merchant/Products.aspx", True)
                End If
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the product manager: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append(WebUi.Section("Product & Stock POS", "MERCHANT STUDIO / PRODUCTS",
                                    "Manage your catalog, categories, stock levels and sale flags."))

            Dim mine As List(Of Product) = _products.ListByMerchant(STAR_DOM.Helpers.Session.CurrentUser.Id)
            Dim low As List(Of Product) = _products.LowStock(STAR_DOM.Helpers.Session.CurrentUser.Id)

            sb.Append("<div class=""sec-head""><div><h3>My products (" & mine.Count.ToString() & ")</h3></div>" &
                      WebUi.BtnHref("/App/Merchant/ProductEdit.aspx", "+ Add Product", "primary", "add") & "</div>")
            If low.Count > 0 Then
                sb.Append(WebUi.AlertBox(low.Count.ToString() & " product(s) at or below low-stock threshold.", "info"))
            End If

            If mine.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No products yet — add your first product."))
            Else
                sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
                For Each h As String In {"SKU", "PRODUCT", "CATEGORY", "PRICE", "STOCK", "FLAGS", "ACTIONS"}
                    sb.Append("<th>" & h & "</th>")
                Next
                sb.Append("</tr></thead><tbody>")
                For Each p As Product In mine
                    Dim lowFlag As Boolean = p.StockQuantity <= p.LowStockThreshold
                    sb.Append("<tr>")
                    sb.Append("<td>" & WebUi.Esc(p.Sku) & "</td>")
                    sb.Append("<td><b>" & WebUi.Esc(p.Name) & "</b><br><span class=""sub"" style=""font-size:11px"">" &
                              WebUi.Esc(p.BrandName) & "</span></td>")
                    sb.Append("<td>" & WebUi.Esc(p.CategoryName) & "</td>")
                    sb.Append("<td>" & WebUi.Money(p.EffectivePrice) & "</td>")
                    sb.Append("<td><form method=""post"" style=""display:flex;gap:6px;align-items:center"">" &
                              "<input type=""hidden"" name=""stockId"" value=""" & p.Id.ToString() & """>" &
                              "<input name=""stockQty"" type=""number"" value=""" & p.StockQuantity.ToString() & """ style=""width:64px;padding:5px;border:1px solid var(--line);border-radius:7px"">" &
                              "<button class=""btn ghost sm"" type=""submit""><span class=""ic ms"">save</span><span>Save</span></button></form>" &
                              If(lowFlag, "<span style=""color:var(--primary);font-size:11px;font-weight:700"">LOW</span>", "") & "</td>")
                    sb.Append("<td>")
                    Dim flags As New List(Of String)()
                    If p.IsBoothExclusive Then flags.Add("BOOTH")
                    If p.IsEventExclusive Then flags.Add("EVENT")
                    If p.IsFeatured Then flags.Add("FEATURED")
                    If p.HasDiscount Then flags.Add(p.DiscountPercent.ToString() & "%")
                    If p.BadgeLabel <> "" Then flags.Add(p.BadgeLabel)
                    sb.Append(If(flags.Count = 0, "—", String.Join(" ", flags.Select(Function(f) WebUi.Pill(f, "yellow")))))
                    sb.Append("</td>")
                    sb.Append("<td class=""rowact""><a href=""/App/Merchant/ProductEdit.aspx?id=" & p.Id.ToString() & """>Edit</a>" &
                              "<a href=""/App/Merchant/Products.aspx?toggle=" & p.Id.ToString() & """ data-confirm=""Toggle product availability?"">" &
                              If(p.IsActive, "Hide", "Show") & "</a>" &
                              "<a href=""/App/Merchant/Products.aspx?del=" & p.Id.ToString() & """ data-confirm=""Delete this product?"" data-confirm-danger"">Delete</a></td>")
                    sb.Append("</tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If

            ' categories
            Dim cats As List(Of Category) = _cats.ListAll()
            sb.Append("<div class=""sec-head"" style=""margin-top:26px""><div><h3>Categories (" & cats.Count.ToString() & ")</h3></div></div>")
            sb.Append("<form method=""post"" class=""card"" style=""margin-bottom:10px;display:flex;gap:10px;align-items:flex-end;flex-wrap:wrap"">")
            sb.Append("<div class=""field"" style=""flex:1;min-width:180px;margin:0""><label>New category</label><input name=""catName"" required></div>")
            sb.Append("<div class=""field"" style=""flex:2;min-width:220px;margin:0""><label>Description</label><input name=""catDesc""></div>")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">add</span><span>Add Category</span></button></form>")
            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"CATEGORY", "DESCRIPTION", "STATUS", ""}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            For Each c As Category In cats
                sb.Append("<tr><td><b>" & WebUi.Esc(c.Name) & "</b></td><td>" & WebUi.Esc(c.Description) & "</td>")
                sb.Append("<td>" & If(c.IsActive, WebUi.Badge("ACTIVE"), WebUi.Badge("HIDDEN")) & "</td>")
                sb.Append("<td class=""rowact""><a href=""/App/Merchant/Products.aspx?delcat=" & c.Id.ToString() &
                          """ data-confirm=""Delete this category? Products keep their category ref until reassigned."" data-confirm-danger"">Delete</a></td></tr>")
            Next
            sb.Append("</tbody></table></div>")
            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
