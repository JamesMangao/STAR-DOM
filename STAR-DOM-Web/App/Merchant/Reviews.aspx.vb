Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class ReviewsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _orders As New OrderService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                If Request.QueryString("approve") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("approve"), id)
                    If id > 0 Then _orders.SetReviewApproved(id, True)
                    Response.Redirect("/App/Merchant/Reviews.aspx", True)
                End If
                If Request.QueryString("hide") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("hide"), id)
                    If id > 0 Then _orders.SetReviewApproved(id, False)
                    Response.Redirect("/App/Merchant/Reviews.aspx", True)
                End If
                If Request.QueryString("del") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("del"), id)
                    If id > 0 Then _orders.DeleteReview(id)
                    Response.Redirect("/App/Merchant/Reviews.aspx", True)
                End If
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load reviews: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim reviews As List(Of Review) = _orders.ListAllReviews("").OrderByDescending(Function(r) r.Id).ToList()
            Dim pending As Integer = reviews.Where(Function(r) Not r.IsApproved).Count()
            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Customer Reviews & Ratings", "MERCHANT STUDIO / MODERATION",
                                    "Approve, hide or remove reviews. Only approved ratings appear on the marketplace."))
            sb.Append("<p class=""sub"">" & reviews.Count.ToString() & " review(s) · " & pending.ToString() & " awaiting approval</p>")

            If reviews.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No reviews yet."))
                Out.Text = sb.ToString()
                Return
            End If

            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"PRODUCT", "CUSTOMER", "RATING", "COMMENT", "STATUS", "DATE", "ACTIONS"}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            For Each r As Review In reviews
                sb.Append("<tr><td><b>" & WebUi.Esc(r.ProductName) & "</b></td>")
                sb.Append("<td>" & WebUi.Esc(r.CustomerName) & "</td>")
                sb.Append("<td>" & WebUi.Stars(r.Rating) & "</td>")
                sb.Append("<td style=""max-width:320px"">" & WebUi.Esc(r.Comment) & "</td>")
                sb.Append("<td>" & If(r.IsApproved, WebUi.Badge("APPROVED"), WebUi.Badge("PENDING")) & "</td>")
                sb.Append("<td>" & WebUi.Esc(r.CreatedAt.ToString("MMM d, yyyy")) & "</td>")
                sb.Append("<td class=""rowact"">")
                If Not r.IsApproved Then
                    sb.Append("<a href=""/App/Merchant/Reviews.aspx?approve=" & r.Id.ToString() & """><span class=""ms sm"">check_circle</span> Approve</a>")
                Else
                    sb.Append("<a href=""/App/Merchant/Reviews.aspx?hide=" & r.Id.ToString() & """><span class=""ms sm"">visibility_off</span> Hide</a>")
                End If
                sb.Append("<a href=""/App/Merchant/Reviews.aspx?del=" & r.Id.ToString() & """ data-confirm=""Delete this review permanently?"" data-confirm-danger"">Delete</a>")
                sb.Append("</td></tr>")
            Next
            sb.Append("</tbody></table></div>")
            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace
