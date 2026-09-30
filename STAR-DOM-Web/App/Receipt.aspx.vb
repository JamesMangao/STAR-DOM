Imports System.Configuration
Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class ReceiptPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _orders As New OrderService()
        Private _backId As Integer = 0

        Public ReadOnly Property BackOrderId As Integer
            Get
                Return _backId
            End Get
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                Dim r As Receipt = ResolveReceipt()
                If r Is Nothing Then
                    Out.Text = WebUi.AlertBox("Receipt not found.")
                    Return
                End If
                Dim o As Order = _orders.GetOrder(r.OrderId)
                If o Is Nothing Then
                    Out.Text = WebUi.AlertBox("Order not found.")
                    Return
                End If
                If o.UserId <> STAR_DOM.Helpers.Session.CurrentUser.Id AndAlso Not STAR_DOM.Helpers.Session.CanManageStore Then
                    Out.Text = WebUi.AlertBox("You don't have access to this receipt.")
                    Return
                End If
                _backId = r.OrderId
                Render(r, o)
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load the receipt: " & ex.Message)
            End Try
        End Sub

        Private Function ResolveReceipt() As Receipt
            Dim r As String = Request.QueryString("r")
            If r <> "" Then
                Dim byNumber As Receipt = _orders.ReceiptByNumber(r)
                If byNumber IsNot Nothing Then Return byNumber
            End If
            Dim p As String = Request.QueryString("p")
            If p <> "" Then
                Dim pid As Integer = 0
                Integer.TryParse(p, pid)
                Dim byPayment As Receipt = _orders.ReceiptForPaymentId(pid)
                If byPayment IsNot Nothing Then Return byPayment
            End If
            Return Nothing
        End Function

        Private Sub Render(r As Receipt, o As Order)
            Dim sb As New StringBuilder()
            Dim vat = r.VatAmount

            sb.Append("<div class=""rp-paper"">")

            ' header band
            sb.Append("<div class=""rp-head"">")
            sb.Append("<div class=""rp-brand""><span class=""rp-star""></span><div><div class=""rp-name"">STAR:DOM</div>" &
                      "<div class=""rp-sub"">ARTISAN MARKETPLACE &amp; POP-UP TOUR</div></div></div>")
            sb.Append("<div class=""rp-id""><div class=""rp-ortype"">OFFICIAL RECEIPT</div>" &
                      "<div class=""rp-or"">" & WebUi.Esc(r.ReceiptNumber) & "</div>" &
                      "<div class=""rp-accred"">" & WebUi.Esc(r.IssuerAccreditation) & " &middot; BIR REGISTERED</div></div>")
            sb.Append("</div>")

            ' sold to
            sb.Append("<div class=""rp-kv"">")
            sb.Append("<div><span class=""rp-klabel"">SOLD TO</span><div class=""rp-kval"">" & WebUi.Esc(r.SoldToName) & "</div></div>")
            sb.Append("<div><span class=""rp-klabel"">DELIVER TO</span><div class=""rp-kval"">" & WebUi.Esc(r.SoldToAddress) & "</div></div>")
            sb.Append("<div><span class=""rp-klabel"">PAYMENT</span><div class=""rp-kval"">" & WebUi.Esc(DisplayPay(r.PaymentMethod)) & "</div></div>")
            sb.Append("<div><span class=""rp-klabel"">DATE</span><div class=""rp-kval"">" & WebUi.Esc(r.IssuedAt.ToString("MMMM d, yyyy  h:mm tt")) & "</div></div>")
            sb.Append("</div>")

            ' items table
            sb.Append("<table class=""rp-items""><thead><tr><th>QTY</th><th>DESCRIPTION</th><th>UNIT PRICE</th><th>AMOUNT</th></tr></thead><tbody>")
            Dim line As Integer = 0
            For Each itemLine As String In CsvLines(r.ItemsSnapshot)
                Dim qty As String = "1"
                Dim desc As String = itemLine
                Dim unit As Decimal = 0D
                Dim amt As Decimal = 0D
                ParseLine(itemLine, qty, desc, unit, amt)
                line += 1
                sb.Append("<tr class=""rp-row rp-i" & line.ToString() & """><td>" & WebUi.Esc(qty) & "</td>" &
                          "<td>" & WebUi.Esc(desc) & "</td><td>" & Money(unit) & "</td><td>" & Money(amt) & "</td></tr>")
            Next
            sb.Append("</tbody></table>")

            ' totals + BIR breakdown
            sb.Append("<div class=""rp-foot"">")
            sb.Append("<div class=""rp-sums"">")
            sb.Append("<div class=""rp-srow""><span>Subtotal</span><b>" & Money(r.Subtotal) & "</b></div>")
            If r.DiscountAmount > 0D Then
                sb.Append("<div class=""rp-srow""><span>Discount</span><b>– " & Money(r.DiscountAmount) & "</b></div>")
            End If
            If r.ShippingFee > 0D Then
                sb.Append("<div class=""rp-srow""><span>Shipping</span><b>" & Money(r.ShippingFee) & "</b></div>")
            End If
            sb.Append("<div class=""rp-srow""><span>VATable Sales</span><b>" & Money(r.VatableAmount) & "</b></div>")
            sb.Append("<div class=""rp-srow""><span>VAT (12%)</span><b>" & Money(vat) & "</b></div>")
            sb.Append("<div class=""rp-srow rp-total""><span>Total Sales</span><b>" & Money(r.TotalAmount) & "</b></div>")
            sb.Append("</div>")
            sb.Append("<div class=""rp-stamp"">OFFICIAL<br/>RECEIPT<br/><span class=""rp-check"">✓</span></div>")
            sb.Append("</div>")

            ' issuer footer
            sb.Append("<div class=""rp-issuer"">")
            sb.Append("<div class=""rp-iss-name"">" & WebUi.Esc(r.IssuerName) & "</div>")
            sb.Append("<div class=""rp-iss-at"">TIN: " & WebUi.Esc(r.IssuerTin) & " &middot; " & WebUi.Esc(r.IssuerAddress) & "</div>")
            sb.Append("<div class=""rp-iss-at"">" & WebUi.Esc(IssuerLine()) & " &middot; This receipt is not valid for claiming input VAT.</div>")
            sb.Append("</div>")

            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function IssuerLine() As String
            Dim v As String = ConfigurationManager.AppSettings("Receipt.LineOfBusiness")
            If String.IsNullOrWhiteSpace(v) Then v = "On-line selling of artisan goods &amp; commission services"
            Return HttpUtility.HtmlDecode(v)
        End Function

        Private Function CsvLines(snapshot As String) As String()
            If String.IsNullOrWhiteSpace(snapshot) Then Return New String() {}
            Return snapshot.Replace(Constants.vbLf, "|").Replace(Constants.vbCr, "").Split("|"c)
        End Function

        Private Sub ParseLine(raw As String, ByRef qty As String, ByRef desc As String, ByRef unit As Decimal, ByRef amt As Decimal)
            ' snapshot format: "2 x Ceramic Mug @ 250.00 = 500.00"
            Dim at As Integer = raw.IndexOf(" x ", StringComparison.Ordinal)
            Dim u As Integer = raw.LastIndexOf("@ ", StringComparison.Ordinal)
            Dim eq As Integer = raw.IndexOf(" = ", StringComparison.Ordinal)
            If at > 0 AndAlso u > at AndAlso eq > u Then
                qty = raw.Substring(0, at).Trim()
                desc = raw.Substring(at + 3, u - (at + 3)).Trim()
                Dim un As Decimal = 0D
                Decimal.TryParse(raw.Substring(u + 2, eq - (u + 2)).Trim(), un)
                unit = un
                Dim am As Decimal = 0D
                Decimal.TryParse(raw.Substring(eq + 3).Trim(), am)
                amt = am
            Else
                desc = raw
                unit = 0D
                amt = 0D
            End If
        End Sub

        Private Function Money(v As Decimal) As String
            Return "<b>&#8369;</b> " & v.ToString("N2")
        End Function

        Private Function DisplayPay(pm As String) As String
            Select Case pm.ToUpperInvariant()
                Case "GCASH" : Return "GCash"
                Case "MAYA" : Return "Maya"
                Case "CARD" : Return "Card / e-Wallet"
                Case "COD" : Return "Cash on Delivery"
                Case Else : Return pm
            End Select
        End Function

    End Class

End Namespace