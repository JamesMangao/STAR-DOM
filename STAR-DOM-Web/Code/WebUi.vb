Imports System.Text
Imports System.Web
Imports STAR_DOM.Helpers

Namespace STAR_DOM.Web

    ''' <summary>Shared server-side HTML helpers for the STAR:DOM web UI.</summary>
    Public Module WebUi

        Public Function Esc(value As Object) As String
            If value Is Nothing OrElse value Is DBNull.Value Then Return ""
            Return HttpUtility.HtmlEncode(Convert.ToString(value))
        End Function

        Public Function Attr(value As Object) As String
            If value Is Nothing OrElse value Is DBNull.Value Then Return ""
            Return HttpUtility.HtmlAttributeEncode(Convert.ToString(value))
        End Function

        Private ReadOnly _palettes As String() = {
            "#b70011", "#c2410c", "#a16207", "#15803d", "#1d4ed8",
            "#6d28d9", "#be185d", "#0e7490", "#b45309", "#4d7c0f"
        }

        ''' <summary>Deterministic placeholder artwork block (gradient + initials) for a product.</summary>
        Public Function Art(seed As Integer, name As String, Optional style As String = "") As String
            Dim c1 As String = _palettes(seed Mod _palettes.Length)
            Dim initials As String = ""
            Dim parts As String() = Convert.ToString(name).Split(" "c)
            For i As Integer = 0 To Math.Min(parts.Length - 1, 1)
                If parts(i).Length > 0 Then initials &= Char.ToUpperInvariant(parts(i)(0))
            Next
            If initials = "" Then initials = "SD"
            Dim sb As New StringBuilder()
            sb.Append("<div class=""art"" style=""background-image:linear-gradient(135deg," & c1 & ",#1e1b19);" & style & """>")
            sb.Append("<span>" & Esc(initials) & "</span>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ''' <summary>Real product image block when one is on file, else gradient placeholder art.</summary>
        Public Function ProductImg(imageFile As Object, seed As Integer, name As String, Optional style As String = "") As String
            Dim f As String = Convert.ToString(imageFile)
            If String.IsNullOrWhiteSpace(f) Then Return Art(seed, name, style)
            Return "<div class=""art"" style=""background-image:url('" & Attr(f) & "');" & style & """></div>"
        End Function

        ''' <summary>
        ''' Commission slot card, shared by the Commission Hub and the Marketplace preview so the
        ''' two renderers cannot drift apart again. Prefers the artist's real sample work, then
        ''' their avatar, and only falls back to gradient placeholder art when neither is on file.
        ''' Pass <paramref name="ctaUrl"/>/<paramref name="ctaText"/> to retarget the button (the
        ''' Marketplace uses this to funnel visitors through the Hub instead of jumping
        ''' straight to the request form).
        ''' </summary>
        Public Function CommissionSlotCard(s As CommissionSlotView,
                                           Optional startingLabel As String = "Starting",
                                           Optional ctaUrl As String = "",
                                           Optional ctaText As String = "") As String
            Dim art As String = If(Not String.IsNullOrWhiteSpace(s.SampleImage), s.SampleImage, s.MerchantAvatar)
            Dim target As String = ctaUrl
            If target = "" Then target = "/App/CommissionRequest.aspx?m=" & s.MerchantId.ToString()
            Dim label As String = If(ctaText <> "", ctaText, If(s.CtaText <> "", s.CtaText, "Request Slot"))
            Dim sb As New StringBuilder()
            sb.Append("<div class=""pcard"">")
            sb.Append("<div style=""position:relative"">")
            sb.Append(ProductImg(art, s.Seed, s.MerchantName, "height:170px"))
            sb.Append("<span class=""badge warn"" style=""position:absolute;top:8px;right:8px"">" & Esc(s.SlotsText) & "</span></div>")
            sb.Append("<div class=""pbody"">")
            sb.Append("<span class=""brand"">" & Esc(s.MerchantTagline) & "</span>")
            sb.Append("<b style=""font-size:16px"">" & Esc(s.MerchantName) & "</b>")
            ' No starting price / deposit rows: commissions are quoted per request, and
            ' finished commissioned products can only be claimed via delivery.
            sb.Append("<div class=""kv"" style=""grid-template-columns:110px 1fr"">")
            sb.Append("<dt>Turnaround</dt><dd>" & Esc(s.Turnaround) & "</dd>")
            sb.Append("<dt>Formats</dt><dd>" & Esc(s.Formats) & "</dd>")
            sb.Append("<dt>Claim via</dt><dd>Delivery only</dd>")
            sb.Append("</div>")
            sb.Append(BtnHref(target, label, "primary", "draw"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Public Function Money(value As Object) As String
            Return "<span class=""money"">" & Esc(Fmt_PHP(value)) & "</span>"
        End Function

        Private Function Fmt_PHP(value As Object) As String
            Dim d As Decimal = 0D
            If value IsNot Nothing Then
                Decimal.TryParse(Convert.ToString(value), System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, d)
            End If
            Return "₱" & d.ToString("N2")
        End Function

        ''' <summary>
        ''' Marks a link as needing a signed-in account so the premium gate popup in
        ''' Site.master intercepts it for anonymous visitors. Pass it as
        ''' <see cref="BtnHref"/>'s <c>attrs</c> argument. Returns an empty string when the
        ''' visitor is already signed in, so authenticated markup is unchanged.
        ''' </summary>
        ''' <remarks>
        ''' The anchor keeps its real href, so the plain sign-in redirect still happens
        ''' without JavaScript and if someone types the URL directly. The gate is a
        ''' courtesy layer on top of <see cref="Guard"/>, never a replacement for it.
        ''' </remarks>
        Public Function AuthGateAttrs(productName As String,
                                      Optional imageFile As Object = Nothing,
                                      Optional seed As Integer = 0,
                                      Optional price As Object = Nothing) As String
            If STAR_DOM.Helpers.Session.IsAuthenticated Then Return ""
            Dim sb As New StringBuilder()
            sb.Append(" data-auth-gate=""1""")
            If Not String.IsNullOrWhiteSpace(productName) Then
                sb.Append(" data-name=""" & Attr(productName) & """")
                sb.Append(" data-seed=""" & seed.ToString() & """")
                sb.Append(" data-img=""" & Attr(Convert.ToString(imageFile)) & """")
                sb.Append(" data-price=""" & Attr(Fmt_PHP(price)) & """")
            End If
            Return sb.ToString()
        End Function

        ''' <summary>Status pill colored per state.</summary>
        Public Function Badge(status As String) As String
            Dim s As String = Convert.ToString(status).Trim().ToUpperInvariant()
            Dim kind As String = "neutral"
            Select Case s
                Case "NOW OPEN", "ACTIVE", "ACTIVE TODAY", "APPROVED", "PAID", "DELIVERED",
                     "COMPLETED", "LIVE", "AVAILABLE", "READY", "SUCCESS"
                    kind = "live"
                Case "PENDING", "PENDING REVIEW", "PENDING APPROVAL", "SUBMITTED", "UPCOMING",
                     "CLARIFICATION REQUESTED", "PAYMENT PENDING", "HIDDEN", "PROCESSING"
                    kind = "warn"
                Case "CANCELLED", "DECLINED", "SUSPENDED", "FAILED", "LOW STOCK", "ENDED", "REFUNDED"
                    kind = "muted"
            End Select
            Return "<span class=""badge " & kind & """>" & Esc(status) & "</span>"
        End Function

        Public Function Stars(rating As Integer) As String
            rating = Math.Max(0, Math.Min(5, rating))
            Dim sb As New StringBuilder()
            For i As Integer = 1 To 5
                If i <= rating Then sb.Append("★") Else sb.Append("☆")
            Next
            Return "<span class=""stars"">" & sb.ToString() & "</span>"
        End Function

        Public Function Section(title As String, Optional eyebrow As String = "", Optional subText As String = "") As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec"">")
            If eyebrow <> "" Then sb.Append("<div class=""eyebrow"">" & Esc(eyebrow) & "</div>")
            sb.Append("<h2>" & Esc(title) & "</h2>")
            If subText <> "" Then sb.Append("<p class=""sub"">" & Esc(subText) & "</p>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Public Function Pill(text As String, Optional kind As String = "yellow") As String
            Return "<span class=""pill " & kind & """>" & Esc(text) & "</span>"
        End Function

        ''' <summary>Category/filter chip link.</summary>
        Public Function OutLink(url As String, text As String, Optional isOn As Boolean = False) As String
            Return "<a class=""chip""" & If(isOn, " on", "") & " href=""" & Attr(url) & """>" & Esc(text) & "</a>"
        End Function

        ''' <summary>
        ''' Anchor styled as a button. Pass <paramref name="attrs"/> for extra markup that
        ''' belongs inside the opening tag, e.g. <c>WebUi.AuthGateAttrs(...)</c> — appending
        ''' to the result instead would land the attributes after the closing tag.
        ''' </summary>
        Public Function BtnHref(url As String, text As String, Optional kind As String = "primary", Optional icon As String = "", Optional attrs As String = "") As String
            Dim ic As String = ""
            If icon <> "" Then
                Dim icCls As String = If(IsGlyph(icon), "ic", "ic ms")
                ic = "<span class=""" & icCls & """>" & Esc(icon) & "</span>"
            End If
            Return "<a class=""btn " & kind & """ href=""" & Attr(url) & """" & attrs & ">" & ic & "<span>" & Esc(text) & "</span></a>"
        End Function

        ''' <summary>
        ''' Add-to-cart button that opens the quantity picker instead of jumping straight
        ''' to the cart with a hardcoded q=1. Emits <c>data-addcart</c>, which the script in
        ''' Site.master intercepts, together with the product details the modal shows.
        ''' </summary>
        ''' <remarks>
        ''' The anchor keeps a real href with q=1, so the button still works with
        ''' JavaScript disabled, when the script fails, and for anonymous visitors who
        ''' are bounced through the sign-in gate first (the gate carries this href to
        ''' Login.aspx as the return target, so their single Add still lands).
        ''' <para>
        ''' <paramref name="p"/> only supplies the id and the modal's display data; the
        ''' modal's own quantity cap is a UI convenience, and the cart page plus
        ''' CartRepository still enforce real stock server-side.
        ''' </para>
        ''' </remarks>
        Public Function AddCartButton(p As STAR_DOM.Models.Product,
                                      returnUrl As String,
                                      Optional text As String = "Add",
                                      Optional kind As String = "primary",
                                      Optional icon As String = "add_shopping_cart") As String
            Dim q As Integer = 1
            If p.StockQuantity > 0 AndAlso p.StockQuantity < q Then q = p.StockQuantity
            Dim url As String = "/App/Cart.aspx?add=" & p.Id.ToString() & "&q=" & q.ToString() &
                                "&ret=" & Attr(returnUrl)
            Dim ic As String = ""
            If icon <> "" Then
                ic = "<span class=""" & If(IsGlyph(icon), "ic", "ic ms") & """>" & Esc(icon) & "</span>"
            End If
            Dim sb As New StringBuilder()
            sb.Append("<a class=""btn " & kind & """ href=""" & Attr(url) & """")
            sb.Append(" data-addcart=""" & Attr(url) & """")
            sb.Append(" data-name=""" & Attr(p.Name) & """")
            sb.Append(" data-price=""" & Attr(Fmt_PHP(p.EffectivePrice)) & """")
            sb.Append(" data-unit=""" & p.EffectivePrice.ToString(System.Globalization.CultureInfo.InvariantCulture) & """")
            sb.Append(" data-stock=""" & p.StockQuantity.ToString() & """")
            sb.Append(" data-img=""" & Attr(p.PrimaryImageFile) & """")
            sb.Append(" data-seed=""" & p.Id.ToString() & """")
            sb.Append(AuthGateAttrs(p.Name, p.PrimaryImageFile, p.Id, p.EffectivePrice))
            sb.Append(">" & ic & "<span>" & Esc(text) & "</span></a>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Prev/next pager for bounded lists. Pass a URL template containing "{P}" for the page number,
        ''' e.g. "/App/Merchant/Orders.aspx?p={P}".
        ''' </summary>
        Public Function Pager(totalItems As Integer, pageSize As Integer, page As Integer, urlTemplate As String) As String
            If pageSize <= 0 OrElse totalItems <= 0 Then Return ""
            Dim totalPages As Integer = CInt(Math.Ceiling(totalItems / pageSize))
            If totalPages <= 1 Then Return ""
            If page < 1 Then page = 1
            If page > totalPages Then page = totalPages
            Dim prevUrl As String = urlTemplate.Replace("{P}", (page - 1).ToString())
            Dim nextUrl As String = urlTemplate.Replace("{P}", (page + 1).ToString())
            Dim sb As New StringBuilder()
            sb.Append("<div class=""pager"">")
            If page > 1 Then
                sb.Append("<a class=""btn ghost"" href=""" & Attr(prevUrl) & """>" & Ic("chevron_left", "sm") & " Newer</a>")
            End If
            sb.Append("<span class=""sub"" style=""padding:0 10px"">Page " & page.ToString() & " of " & totalPages.ToString() & " (" &
                      totalItems.ToString() & " orders)</span>")
            If page < totalPages Then
                sb.Append("<a class=""btn ghost"" href=""" & Attr(nextUrl) & """>Older " & Ic("chevron_right", "sm") & "</a>")
            End If
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ''' <summary>Material Symbols Outlined icon literal. Size classes: "sm", "lg", "xl", "filled".</summary>
        Public Function Ic(name As String, Optional cls As String = "") As String
            Dim extra As String = If(String.IsNullOrWhiteSpace(cls), "", " " & cls.Trim())
            Return "<span class=""ms" & extra & """>" & Esc(name) & "</span>"
        End Function

        ''' <summary>Two-channel SVG donut for share analytics (e.g. omnichannel revenue mix).</summary>
        Public Function Donut(pctA As Double, pctB As Double, centerLabel As String, centerValue As String,
                              Optional subLabel As String = "", Optional gapRounded As Boolean = True) As String
            Dim ra As Double = Math.Max(0D, Math.Min(100D, pctA))
            Dim rb As Double = Math.Max(0D, Math.Min(100D, pctB))
            If ra + rb > 100D Then rb = Math.Max(0D, 100D - ra)
            Dim c As Double = 2 * Math.PI * 38
            Dim lenA As Double = c * ra / 100D
            Dim lenB As Double = c * rb / 100D
            Dim cap As String = If(gapRounded, "round", "butt")
            Dim sb As New StringBuilder()
            sb.Append("<div class=""donut""><div class=""donut-wrap""><svg viewBox=""0 0 100 100"" aria-hidden=""true"">")
            sb.Append("<circle cx=""50"" cy=""50"" r=""38"" fill=""none"" stroke=""#f4ece8"" stroke-width=""14""></circle>")
            sb.Append("<circle cx=""50"" cy=""50"" r=""38"" fill=""none"" stroke=""#b70011"" stroke-width=""14"" stroke-linecap=""" & cap & """ stroke-dasharray=""" & FmtN(lenA) & " " & FmtN(c) & """ stroke-dashoffset=""0""></circle>")
            sb.Append("<circle cx=""50"" cy=""50"" r=""38"" fill=""none"" stroke=""#fed01b"" stroke-width=""14"" stroke-linecap=""" & cap & """ stroke-dasharray=""" & FmtN(lenB) & " " & FmtN(c) & """ stroke-dashoffset=""" & FmtN(-lenA) & """></circle>")
            sb.Append("</svg><div class=""donut-center""><span class=""dc-label"">" & Esc(centerLabel) & "</span><span class=""dc-value"">" & Esc(centerValue) & "</span>")
            If subLabel <> "" Then sb.Append("<span class=""dc-sub"">" & Esc(subLabel) & "</span>")
            sb.Append("</div></div></div>")
            Return sb.ToString()
        End Function

        Private Function FmtN(v As Double) As String
            Return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
        End Function

        ''' <summary>True when the icon string is an emoji/unicode glyph rather than a Material Symbol name.</summary>
        Private Function IsGlyph(s As String) As Boolean
            If s = "" Then Return False
            If s.IndexOf("&#", StringComparison.Ordinal) >= 0 Then Return True
            For Each ch As Char In s
                If AscW(ch) > 126 Then Return True
            Next
            Return False
        End Function

        Public Function AlertBox(message As String, Optional kind As String = "err") As String
            If String.IsNullOrWhiteSpace(message) Then Return ""
            Return "<div class=""alert " & kind & """>" & Esc(message) & "</div>"
        End Function

        Public Function EmptyRow(text As String) As String
            Return "<div class=""empty"">" & Esc(text) & "</div>"
        End Function

    End Module

End Namespace
