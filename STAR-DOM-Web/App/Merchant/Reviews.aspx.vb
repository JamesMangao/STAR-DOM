Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Read-only view of what customers have written about the catalogue.
    '''
    ''' The approve / hide / delete controls are gone. Moderation existed to guard a
    ''' review that any account could post about a product it had never bought; that
    ''' hole is closed at the source instead — a review is only accepted from a
    ''' customer who confirmed the parcel arrived, so it is published on submit and
    ''' the studio has nothing left to arbitrate.
    ''' </summary>
    Public Class ReviewsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _orders As New OrderService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireMerchant()
            Try
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load reviews: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim reviews As List(Of Review) = _orders.ListAllReviews("").OrderByDescending(Function(r) r.Id).ToList()
            Dim avg As Double = If(reviews.Count > 0, reviews.Average(Function(r) r.Rating), 0D)

            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Customer Reviews & Ratings", "MERCHANT STUDIO / REVIEWS",
                                    "Everything customers have written, in the order it arrived."))
            If reviews.Count > 0 Then
                sb.Append("<p class=""sub"">" & reviews.Count.ToString() & " review(s) · average " &
                          avg.ToString("0.0") & " / 5</p>")
            End If

            If reviews.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No reviews yet."))
                Out.Text = sb.ToString()
                Return
            End If

            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"PRODUCT", "CUSTOMER", "RATING", "COMMENT", "DATE"}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            For Each r In reviews
                sb.Append("<tr><td><b>" & WebUi.Esc(r.ProductName) & "</b></td>")
                sb.Append("<td>" & WebUi.Esc(r.CustomerName) & "</td>")
                sb.Append("<td>" & WebUi.Stars(r.Rating) & "</td>")
                sb.Append("<td style=""max-width:420px"">" & WebUi.Esc(r.Comment) & "</td>")
                sb.Append("<td>" & WebUi.Esc(r.CreatedAt.ToString("MMM d, yyyy")) & "</td></tr>")
            Next
            sb.Append("</tbody></table></div>")
            Out.Text = sb.ToString()
        End Sub

    End Class

End Namespace