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

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                If Request.QueryString("cancel") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("cancel"), id)
                    If id > 0 Then
                        Dim r As ServiceResult = _orders.UpdateOrderState(id, "CANCELLED")
                        Session("flash_msg") = r.Message
                        Session("flash_ok") = r.Success
                    End If
                    Response.Redirect("/App/Orders.aspx", True)
                End If
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
                    sb.Append("<td class=""rowact""><a href=""/App/OrderDetail.aspx?id=" & o.Id.ToString() & """>View / Track</a>")
                    If o.Status = "PENDING" Then
                        sb.Append("<a href=""/App/Orders.aspx?cancel=" & o.Id.ToString() & """ data-confirm=""Cancel this order?"" data-confirm-danger"">Cancel</a>")
                    End If
                    sb.Append("</td></tr>")
                Next
                sb.Append("</tbody></table></div>")
            End If
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
