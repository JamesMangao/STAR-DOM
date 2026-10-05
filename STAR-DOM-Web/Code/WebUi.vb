Imports System.Text
Imports System.Web
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models

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

        ''' <summary>
        ''' An admin-uploaded file path, but only when the file really exists.
        ''' Returns "" otherwise, so the caller falls back to its placeholder.
        ''' The stored path lives in the database while the bytes live under
        ''' Uploads\payments: restoring or re-seeding the database (a fresh clone, a
        ''' pg_dump onto another machine) can bring the path without the file, and a
        ''' missing QR must not render as a broken image icon.
        ''' </summary>
        Public Function UploadedFile(rootRelative As String) As String
            Dim rel As String = Convert.ToString(rootRelative).Trim()
            If rel = "" Then Return ""
            rel = rel.TrimStart("/"c).Replace("\", "/")
            If rel.Contains("..") Then Return ""
            Dim ctx As HttpContext = HttpContext.Current
            If ctx Is Nothing Then Return ""
            Try
                If IO.File.Exists(ctx.Server.MapPath("~/" & rel)) Then Return "/" & rel
            Catch
                ' Unmapped virtual path or no request context — treat as missing.
            End Try
            Return ""
        End Function

        ' ---- payment channel branding -------------------------------------------------
        ' The wallet logos live in Assets\Payment, as real WebP files so IIS sends a
        ' Content-Type that matches the bytes and browsers load them smaller.
        Public Const GcashLogo As String = "/Assets/Payment/GCash.webp"
        Public Const GotymeLogo As String = "/Assets/Payment/GOtyme.webp"

        ''' <summary>
        ''' The brand as customers must read it, from any stored channel key. 'MAYA' is
        ''' the pre-rename key for the second wallet and still resolves here. Every page
        ''' that names a wallet goes through this, so the capitalisation is decided once.
        ''' </summary>
        Public Function ChannelBrand(channel As String) As String
            Select Case Convert.ToString(channel).ToUpperInvariant()
                Case "GCASH" : Return "GCash"
                Case "GOTYME", "MAYA" : Return "GOtyme"
                Case "CARD" : Return "Card"
                Case "COD" : Return "Cash on Delivery"
                Case Else : Return Convert.ToString(channel)
            End Select
        End Function

        ''' <summary>Brand colour used for the QR tile, the account number and the logo ring.</summary>
        Public Function ChannelColor(channel As String) As String
            Select Case Convert.ToString(channel).ToUpperInvariant()
                Case "GCASH" : Return "#007dfe"
                Case "GOTYME", "MAYA" : Return "#00a651"
                Case Else : Return "#7c3aed"
            End Select
        End Function

        ''' <summary>
        ''' Where to point the popup's img tag for this channel. Prefers the bytes
        ''' held in the database — those travel with a pg_dump and restore intact —
        ''' and otherwise falls back to an image still sitting under Uploads\. Empty
        ''' when neither is available, which is the caller's cue to draw the
        ''' placeholder.
        ''' </summary>
        Public Function QrImageUrl(ps As PaymentSetting, method As String) As String
            If ps Is Nothing Then Return ""
            If ps.QrImageData IsNot Nothing AndAlso ps.QrImageData.Length > 0 Then
                Return "/App/PaymentQr.aspx?ch=" & HttpUtility.UrlEncode(ChannelKey(method))
            End If
            ' Pre-migration row: the image is a file and UploadedFile answers "" when
            ' that file is not on this machine.
            Return UploadedFile(ps.QrImageFile)
        End Function

        ''' <summary>The storage key for a channel, keeping the legacy MAYA value mapped.</summary>
        Public Function ChannelKey(channel As String) As String
            Select Case Convert.ToString(channel).ToUpperInvariant()
                Case "GCASH" : Return "GCASH"
                Case "GOTYME", "MAYA" : Return "GOTYME"
                Case Else : Return Convert.ToString(channel).ToUpperInvariant()
            End Select
        End Function

        Public Function ChannelLogo(channel As String) As String
            Select Case Convert.ToString(channel).ToUpperInvariant()
                Case "GCASH" : Return GcashLogo
                Case "GOTYME", "MAYA" : Return GotymeLogo
                Case Else : Return ""
            End Select
        End Function

        ''' <summary>
        ''' The wallet's logo as an img tag, or "" when the channel has no logo (COD).
        ''' Used in Checkout's payment picker and in the Scan to Pay popup header.
        ''' </summary>
        Public Function PayLogo(channel As String, size As Integer) As String
            Dim src As String = ChannelLogo(channel)
            If src = "" Then Return ""
            Return "<img class=""pay-logo"" src=""" & Attr(src) & """ alt=""" & Attr(ChannelBrand(channel)) &
                   """ width=""" & size.ToString() & """ height=""" & size.ToString() & """ style=""width:" & size.ToString() & "px;height:" & size.ToString() & "px;object-fit:contain;flex:0 0 auto"">"
        End Function

        ''' <summary>
        ''' The "Scan to Pay" popup for an e-wallet order, shared by Checkout (before
        ''' the order exists) and Order Detail (after it does) so both look identical.
        ''' Every value shown comes from the admin-managed PaymentSettings row.
        '''
        ''' submitProceed decides what the primary button does. On Checkout it is a real
        ''' submit button inside the checkout form: that POST is what finally creates
        ''' the order. On Order Detail the order already exists, so the same button just
        ''' closes the popup and focuses the reference field.
        ''' </summary>
        Public Function QrPaymentModal(ps As PaymentSetting, method As String, amount As Decimal,
                                       openNow As Boolean, submitProceed As Boolean) As String
            Dim brand As String = ChannelBrand(method)
            Dim color As String = ChannelColor(method)
            Dim showQrImg As Boolean = (ps.QrDisplayMode = "BOTH" OrElse ps.QrDisplayMode = "QR_ONLY")
            Dim showNumber As Boolean = (ps.QrDisplayMode = "BOTH" OrElse ps.QrDisplayMode = "NUMBER_NAME")
            Dim showName As Boolean = (ps.QrDisplayMode = "BOTH" OrElse ps.QrDisplayMode = "NUMBER_NAME" OrElse ps.QrDisplayMode = "NAME_ONLY")
            ' Resolved to the streaming endpoint, which prefers the copy stored in
            ' the database and falls back to the old file when there is one. Empty
            ' when neither exists, so the placeholder is drawn instead of a broken
            ' image.
            Dim qrFile As String = QrImageUrl(ps, method)
            Dim sb As New StringBuilder()

            sb.Append("<div class=""modal-backdrop" & If(openNow, " open", "") & """ id=""qrPaymentModal"" aria-hidden=""" & If(openNow, "false", "true") & """>")
            sb.Append("<div class=""modal-card"" role=""dialog"" aria-modal=""true"" style=""max-width:440px;text-align:center;padding:24px 26px"">")
            sb.Append("<div style=""display:flex;justify-content:space-between;align-items:center;margin-bottom:12px"">")
            sb.Append("<div style=""display:flex;align-items:center;gap:10px"">")
            sb.Append(PayLogo(method, 32))
            If ChannelLogo(method) = "" Then
                sb.Append("<span class=""ph-ic"" style=""width:32px;height:32px;font-size:16px;background:" & color & ";color:#fff;border-radius:8px"">" & Ic("account_balance_wallet", "sm") & "</span>")
            End If
            sb.Append("<b style=""font-size:15px;color:var(--ink)"">Scan to Pay with " & Esc(brand) & "</b></div>")
            sb.Append("<button type=""button"" id=""qrModalCloseX"" style=""background:none;border:none;cursor:pointer;color:var(--ink-soft);padding:4px""><span class=""ms"">close</span></button>")
            sb.Append("</div>")

            ' QR graphic — the admin-uploaded image when the display mode includes it.
            If showQrImg Then
                sb.Append("<div style=""background:#fff;border:2px solid var(--line);border-radius:16px;padding:16px;margin:12px auto;display:inline-block;box-shadow:var(--sh-1)"">")
                If qrFile <> "" Then
                    sb.Append("<img src=""" & Attr(qrFile) & """ alt=""" & Attr(brand & " QR code") & """ width=""180"" height=""180"" style=""display:block;margin:0 auto;object-fit:contain;background:#fff"">")
                Else
                    sb.Append(QrPlaceholder(color, method))
                End If
                If ps.QrCaption <> "" Then
                    sb.Append("<div class=""sub"" style=""font-size:10.5px;text-align:center;margin-top:6px"">" & Esc(ps.QrCaption) & "</div>")
                End If
                sb.Append("</div>")
            End If

            ' Account details — a row appears only when the display mode allows it.
            sb.Append("<div class=""card"" style=""background:var(--surface-low);border:1px solid var(--line);border-radius:12px;padding:12px;margin:8px 0 16px;text-align:left"">")
            If showName AndAlso ps.AccountName <> "" Then
                sb.Append("<div class=""row space-between"" style=""margin-bottom:4px""><span class=""sub"" style=""font-size:11px"">Account Name</span><b style=""font-size:12px"">" & Esc(ps.AccountName) & "</b></div>")
            End If
            If showNumber AndAlso ps.AccountNumber <> "" Then
                sb.Append("<div class=""row space-between"" style=""margin-bottom:4px""><span class=""sub"" style=""font-size:11px"">" & Esc(brand) & " Number</span><b style=""font-size:13px;color:" & color & ";font-family:var(--font-mono)"">" & Esc(ps.AccountNumber) & "</b></div>")
            End If
            sb.Append("<div class=""row space-between""><span class=""sub"" style=""font-size:11px"">Amount Due</span><b style=""font-size:14px;color:var(--primary)"">" & Money(amount) & "</b></div>")
            sb.Append("</div>")

            sb.Append("<p class=""sub"" style=""margin:0 0 16px;font-size:11.5px;line-height:1.4"">1. Open your " & Esc(brand) & " app &amp; ")
            If showQrImg AndAlso showNumber Then
                sb.Append("scan the QR code above or send to the number.")
            ElseIf showQrImg Then
                sb.Append("scan the QR code above.")
            ElseIf showNumber Then
                sb.Append("send to the " & Esc(brand) & " number.")
            Else
                sb.Append("send to the account name shown.")
            End If
            sb.Append("<br>2. Save your receipt reference number.")
            If submitProceed Then
                sb.Append("<br>3. Tap the button below to finish your order.</p>")
            Else
                sb.Append("<br>3. Enter the reference number below to verify payment.</p>")
            End If

            ' Back first, primary second. On Checkout Back simply closes the popup —
            ' the order was never created, and every field they typed is still on the
            ' form underneath, so changing the payment method costs them nothing.
            sb.Append("<div style=""display:flex;gap:10px;align-items:center"">")
            sb.Append("<button type=""button"" class=""btn ghost"" id=""qrModalBackBtn"" style=""flex:0 0 auto""><span class=""ms sm"">arrow_back</span><span>Back</span></button>")
            If submitProceed Then
                sb.Append("<button type=""submit"" name=""scanConfirmed"" value=""1"" class=""btn primary"" id=""qrModalProceedBtn"" style=""flex:1 1 auto;justify-content:center;border-radius:10px;padding:10px;font-weight:700"">I Have Scanned &amp; Sent Payment</button>")
            Else
                sb.Append("<button type=""button"" class=""btn primary"" id=""qrModalProceedBtn"" style=""flex:1 1 auto;justify-content:center;border-radius:10px;padding:10px;font-weight:700"">I Have Scanned &amp; Sent Payment</button>")
            End If
            sb.Append("</div>")
            sb.Append("</div></div>")

            sb.Append("<" & "script>")
            sb.Append("(function(){")
            sb.Append("var qrM=document.getElementById('qrPaymentModal');")
            sb.Append("var qrClose=document.getElementById('qrModalCloseX');")
            sb.Append("var qrBack=document.getElementById('qrModalBackBtn');")
            sb.Append("var qrBtn=document.getElementById('qrModalProceedBtn');")
            sb.Append("function hideQr(){if(qrM){qrM.classList.remove('open');qrM.setAttribute('aria-hidden','true');}}")
            sb.Append("function showQr(){if(qrM){qrM.classList.add('open');qrM.setAttribute('aria-hidden','false');}}")
            sb.Append("if(qrClose)qrClose.addEventListener('click',hideQr);")
            sb.Append("if(qrBack)qrBack.addEventListener('click',hideQr);")
            sb.Append("if(qrBtn)qrBtn.addEventListener('click',function(e){")
            If Not submitProceed Then
                ' Nothing to submit here — the order already exists. Just close the
                ' popup and put the cursor in the reference field.
                sb.Append("e.preventDefault();")
                sb.Append("var refIn=document.querySelector('input[name=""payRef""]');hideQr();if(refIn)refIn.focus();")
            End If
            ' On Checkout the button is a real submit: do not interfere, or the order
            ' is never created.
            sb.Append("});")
            sb.Append("if(qrM)qrM.addEventListener('click',function(e){if(e.target===qrM)hideQr();});")
            sb.Append("document.addEventListener('keydown',function(e){if(qrM&&qrM.classList.contains('open')&&e.key==='Escape')hideQr();});")
            ' Lets Order Detail's "Scan to Pay" button reopen this popup.
            sb.Append("window.openQrPayModal=showQr;")
            sb.Append("})();")
            sb.Append("</" & "script>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Stylized SVG QR stand-in used when the admin has not uploaded a real QR image
        ''' but the display mode still calls for one. Kept from the original design so
        ''' the popup never renders an empty box.
        ''' </summary>
        Private Function QrPlaceholder(qrColor As String, method As String) As String
            Dim sb As New StringBuilder()
            sb.Append("<svg width=""180"" height=""180"" viewBox=""0 0 180 180"" xmlns=""http://www.w3.org/2000/svg"" style=""display:block;margin:0 auto"">")
            sb.Append("<rect width=""180"" height=""180"" fill=""#ffffff""/>")
            sb.Append("<rect x=""10"" y=""10"" width=""46"" height=""46"" rx=""6"" fill=""none"" stroke=""" & qrColor & """ stroke-width=""5""/>")
            sb.Append("<rect x=""22"" y=""22"" width=""22"" height=""22"" rx=""3"" fill=""" & qrColor & """/>")
            sb.Append("<rect x=""124"" y=""10"" width=""46"" height=""46"" rx=""6"" fill=""none"" stroke=""" & qrColor & """ stroke-width=""5""/>")
            sb.Append("<rect x=""136"" y=""22"" width=""22"" height=""22"" rx=""3"" fill=""" & qrColor & """/>")
            sb.Append("<rect x=""10"" y=""124"" width=""46"" height=""46"" rx=""6"" fill=""none"" stroke=""" & qrColor & """ stroke-width=""5""/>")
            sb.Append("<rect x=""22"" y=""136"" width=""22"" height=""22"" rx=""3"" fill=""" & qrColor & """/>")
            For Each r As String In {
                "66,16,14,14", "90,16,20,10", "66,38,10,24", "86,38,24,12",
                "16,66,12,18", "38,66,18,12", "66,66,16,16", "124,66,20,12", "152,66,18,18",
                "16,94,24,18", "124,90,14,24", "148,94,22,14",
                "66,124,20,14", "94,124,16,24", "124,124,14,14", "148,124,22,20",
                "66,148,18,22", "124,148,18,22"}
                Dim p As String() = r.Split(","c)
                sb.Append("<rect x=""" & p(0) & """ y=""" & p(1) & """ width=""" & p(2) & """ height=""" & p(3) & """ fill=""#1e1b19""/>")
            Next
            sb.Append("<rect x=""68"" y=""68"" width=""44"" height=""44"" rx=""8"" fill=""#ffffff"" stroke=""" & qrColor & """ stroke-width=""2""/>")
            sb.Append("<circle cx=""90"" cy=""90"" r=""16"" fill=""" & qrColor & """/>")
            sb.Append("<text x=""90"" y=""95"" font-size=""13"" font-weight=""800"" fill=""#ffffff"" text-anchor=""middle"" font-family=""sans-serif"">" & ChannelBrand(method).Substring(0, 1).ToUpperInvariant() & "</text>")
            sb.Append("</svg>")
            Return sb.ToString()
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
            Return "<div class=""art"" style=""background-image:url('" & Attr(AssetUrl(f)) & "');" & style & """></div>"
        End Function

        ''' <summary>
        ''' Where an img tag or CSS background should fetch a stored asset path from.
        '''
        ''' Venue photos under /Assets/Malls/ are held twice: the file in Git and the
        ''' bytes in AssetImages. They are served by App/AssetImg.aspx, which reads the
        ''' database row first and falls back to the file, so a lost folder still
        ''' renders. Every other path (product art, logos, payment marks) is a plain
        ''' static file and is returned untouched.
        '''
        ''' The path is URL-encoded on the way out: several venue files carry spaces
        ''' and one used to carry an apostrophe, which breaks a bare query string.
        ''' </summary>
        Public Function AssetUrl(path As Object) As String
            Dim f As String = Convert.ToString(path)
            If String.IsNullOrWhiteSpace(f) Then Return ""
            If Not f.StartsWith("/Assets/Malls/", StringComparison.OrdinalIgnoreCase) Then Return f
            Return "/App/AssetImg.aspx?p=" & HttpUtility.UrlEncode(f)
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
            ' The banner is the brand logo, not placeholder initials. ProductImg falls
            ' back to a gradient tile spelling out initials ("SA") whenever no file is
            ' on record, which is what a merchant with no sample image used to get --
            ' wrong for a card whose whole job is to say "this is STAR:DOM". The logo is
            ' letterboxed rather than cropped because it is a wide wordmark: cover
            ' would cut the ends off, so it is contained on a soft brand-tinted panel.
            If String.IsNullOrWhiteSpace(art) Then
                sb.Append("<div class=""art"" style=""height:170px;background:linear-gradient(135deg,#fff7d6,#ffe9a8);" &
                          "background-image:none"">")
                sb.Append("<img src=""/Assets/stardom-logo.webp"" alt=""STAR:DOM"" " &
                          "style=""max-width:82%;max-height:118px;object-fit:contain"">")
                sb.Append("</div>")
            Else
                sb.Append(ProductImg(art, s.Seed, s.MerchantName, "height:170px"))
            End If
            sb.Append("<span class=""badge warn"" style=""position:absolute;top:8px;right:8px"">" & Esc(s.SlotsText) & "</span></div>")
            sb.Append("<div class=""pbody"">")
            ' No style tagline row: the artist name is the identity, the style line was
            ' noise above it. MerchantTagline is still stored and editable on the profile.
            sb.Append("<b style=""font-size:16px"">" & Esc(s.MerchantName) & "</b>")
            ' No starting price / deposit rows: commissions are quoted per request, and
            ' finished commissioned products can only be claimed via delivery.
            ' No formats row either: the artist decides output per request, so a fixed
            ' list on the card was misleading. CommissionFormats stays in the database.
            sb.Append("<div class=""kv"" style=""grid-template-columns:110px 1fr"">")
            sb.Append("<dt>Turnaround</dt><dd>" & Esc(s.Turnaround) & "</dd>")
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
                     "PAYMENT PENDING", "HIDDEN", "PROCESSING"
                    kind = "warn"
                Case "CANCELLED", "DECLINED", "SUSPENDED", "FAILED", "LOW STOCK", "ENDED", "REFUNDED"
                    kind = "muted"
            End Select
            Return "<span class=""badge " & kind & """>" & Esc(status) & "</span>"
        End Function

        ''' <summary>
        ''' Booth / stall label for display.
        ''' </summary>
        ''' <remarks>
        ''' BoothNumber is stored inconsistently across events -- most rows hold a bare
        ''' code ("D-04", "Island F") but some were seeded as "Booth D-04". Renderers
        ''' that always prefixed "Booth " therefore printed "Booth Booth D-04", and the
        ''' ones that used an uppercased "BOOTH " chip printed "BOOTH Booth D-04". This
        ''' prefixes only when the stored value does not already name its own prefix,
        ''' so both shapes render correctly without a data migration.
        ''' </remarks>
        Public Function BoothLabel(number As Object) As String
            Dim s As String = Convert.ToString(number).Trim()
            If s.Length = 0 Then Return "&mdash;"
            If s.StartsWith("Booth", StringComparison.OrdinalIgnoreCase) Then Return Esc(s)
            If s.StartsWith("Stall", StringComparison.OrdinalIgnoreCase) Then Return Esc(s)
            Return "Booth " & Esc(s)
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

        ''' <summary>
        ''' The plain-text contact sentence that closes every failed-payment message.
        ''' Lives on PaymentSetting, not here, because the service layer writes the
        ''' same sentence into its flash messages — the two must never drift apart.
        ''' </summary>
        Public ReadOnly Property PaymentFailureContact As String
            Get
                Return Models.PaymentSetting.SupportContact
            End Get
        End Property

        ''' <summary>
        ''' Shown wherever an order or payment did not go through. The customer is
        ''' pointed at a human instead of a dead-end error, so this must stay in
        ''' step with the number published by the studio.
        ''' </summary>
        Public Function PaymentFailureNote(Optional heading As String = "Order / payment failed") As String
            Return "<div class=""card"" style=""border-color:var(--primary);background:#ffe0de"">" &
                   "<b style=""display:block;margin-bottom:4px"">" & Esc(heading) & "</b>" &
                   "<span class=""sub"" style=""color:var(--ink)"">For concerns please message " &
                   "<b>@star.d0mm</b> on Instagram or contact <b>09701375033</b>.</span></div>"
        End Function

        ''' <summary>
        ''' "You cannot cancel from here" notice. Once the studio has the order or
        ''' commission moving it is off the customer's hands, so say so before they
        ''' reach for a cancel button.
        ''' </summary>
        Public Function NoCancelNote(Optional subject As String = "an order") As String
            Return "<div class=""sub"" style=""font-size:12px;margin-top:8px"">" &
                   WebUi.Ic("lock", "sm") & " Note that once " & Esc(subject) & " is processing, it cannot be canceled.</div>"
        End Function

        Public Function Pill(text As String, Optional kind As String = "yellow") As String
            Return "<span class=""pill " & kind & """>" & Esc(text) & "</span>"
        End Function

        ''' <summary>
        ''' Shopper-facing "N for ₱M" bundle legend, e.g. "Stickers Bundle — any 4 for
        ''' ₱100 · Button Pins Bundle — any 3 for ₱100". Returns an empty string when no
        ''' deal bundle is active, so callers can append it unconditionally.
        ''' </summary>
        Public Function BundleNote() As String
            Return (New STAR_DOM.Services.CartService()).BundleNote()
        End Function

        ''' <summary>
        ''' Banner form of <see cref="BundleNote"/>, for the browsing pages (catalog,
        ''' marketplace, public landing) so the deal is visible before the cart. It only
        ''' advertises the group rule — product cards still show BasePrice, and
        ''' CartService.BundleDiscount is what actually takes the money off — so this
        ''' never contradicts the price printed on a card.
        ''' </summary>
        Public Function BundleNoteStrip() As String
            Dim note As String = BundleNote()
            If note = "" Then Return ""
            Dim sb As New StringBuilder()
            sb.Append("<div class=""bundle-note"">")
            sb.Append("<span class=""bundle-note-ic ms"">sell</span>")
            sb.Append("<span><b>BUNDLE DEALS</b><br>")
            sb.Append("<span class=""bundle-note-body"">" & Esc(note) & "</span></span>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' One line per active bundle, e.g. "Stickers Bundle · 4 for ₱100". For tight
        ''' spots where the joined legend from <see cref="BundleNote"/> would wrap over
        ''' too many lines, such as the marketplace Bazaar Exclusive tile. Names are
        ''' escaped; the separator is real markup. Empty list when none is active.
        ''' </summary>
        Public Function BundleNoteLines() As List(Of String)
            Dim lines As New List(Of String)()
            For Each g As BundleGroup In (New STAR_DOM.Services.CartService()).BundleGroups()
                Dim label As String = If(g.Name, "").Trim()
                If label = "" Then
                    label = "Bundle #" & g.BundleId.ToString()
                ElseIf label.EndsWith(" Bundle", StringComparison.OrdinalIgnoreCase) Then
                    ' The tile already says BUNDLE DEALS / BAZAAR EXCLUSIVE, so repeating
                    ' the word on every line only costs width. Data-driven, not hardcoded.
                    label = label.Substring(0, label.Length - " Bundle".Length).Trim()
                End If
                lines.Add(Esc(label) & " &middot; " & g.GroupSize.ToString() & " for " & Fmt.PHP(g.GroupPrice))
            Next
            Return lines
        End Function

        ''' <summary>
        ''' The bundle block for the yellow promo tiles: one compact line per deal,
        ''' separated from the tile's own copy by a rule. Colours suit the yellow tile
        ''' background. Returns an empty string when no deal bundle is active, so
        ''' callers can append it unconditionally.
        ''' </summary>
        Public Function BundleNoteBlock() As String
            Dim lines As List(Of String) = BundleNoteLines()
            If lines.Count = 0 Then Return ""
            Dim sb As New StringBuilder()
            sb.Append("<div style=""border-top:1px solid rgba(35,27,0,.18);margin-top:5px;padding-top:7px;" &
                      "display:flex;flex-direction:column;gap:3px"">")
            For Each line As String In lines
                sb.Append("<span style=""font-size:9.5px;font-weight:700;line-height:1.35;color:#231b00"">" &
                          line & "</span>")
            Next
            sb.Append("</div>")
            Return sb.ToString()
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
            sb.Append(" data-price=""" & Attr(Fmt_PHP(p.BasePrice)) & """")
            sb.Append(" data-unit=""" & p.BasePrice.ToString(System.Globalization.CultureInfo.InvariantCulture) & """")
            sb.Append(" data-stock=""" & p.StockQuantity.ToString() & """")
            sb.Append(" data-img=""" & Attr(p.PrimaryImageFile) & """")
            sb.Append(" data-seed=""" & p.Id.ToString() & """")
            sb.Append(AuthGateAttrs(p.Name, p.PrimaryImageFile, p.Id, p.BasePrice))
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
