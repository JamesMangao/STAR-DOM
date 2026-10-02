Imports System.Text
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Admin console page for managing the GCash / GOtyme payment channels:
    ''' QR code image, account number, account name, what the customer-facing
    ''' popup displays (QR only / number+name / both / name only), and whether
    ''' the channel is offered at checkout. Everything the order-detail QR
    ''' popup renders comes from here — nothing is hardcoded anymore.
    ''' </summary>
    Public Class AdminPaymentSettingsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _settings As New PaymentSettingRepository()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireAdmin()
            Try
                If Guard.IsPost() Then
                    HandlePost()
                    Return
                End If
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load payment settings: " & ex.Message)
            End Try
        End Sub

        ' ---------------- POST: save channel settings ----------------

        Private Sub HandlePost()
            If Not Csrf.IsValidRequest() Then
                Out.Text = WebUi.AlertBox("Your session expired — please try again.")
                Render()
                Return
            End If

            Dim channel As String = PostedChannel()
            If channel = "" Then
                Out.Text = WebUi.AlertBox("Unknown payment channel.")
                Render()
                Return
            End If
            Dim brand As String = WebUi.ChannelBrand(channel)

            ' Resolve the saved state so an untouched file input keeps the existing QR image.
            Dim current As PaymentSetting = _settings.GetByChannel(channel)

            Dim s As New PaymentSetting With {
                .Channel = channel,
                .AccountName = TrimTo(Posted(channel, "accountName"), 120),
                .AccountNumber = TrimTo(Posted(channel, "accountNumber"), 60),
                .QrImageFile = If(current IsNot Nothing, current.QrImageFile, ""),
                .QrCaption = TrimTo(Posted(channel, "qrCaption"), 120),
                .QrDisplayMode = PaymentSetting.NormalizeMode(Posted(channel, "qrDisplayMode")),
                .IsEnabled = (Posted(channel, "isEnabled") = "1"),
                .UpdatedBy = If(STAR_DOM.Helpers.Session.CurrentUser IsNot Nothing,
                                STAR_DOM.Helpers.Session.CurrentUser.Username, "")
            }

            ' QR image upload (optional). JPEG/PNG/WebP only, up to 5 MB — these are
            ' screenshots of wallet QR codes, so anything larger is suspicious anyway.
            ' The bytes only arrive here because the master page's single shell form
            ' posts as multipart/form-data; a nested form tag cannot carry the
            ' enctype, because browsers drop it.
            Dim uploadError As String = ""
            Dim newQr As Byte() = Nothing
            Dim newQrMime As String = ""
            Dim upload As HttpPostedFile = Request.Files(Field(channel, "qrImage"))
            If upload IsNot Nothing AndAlso upload.ContentLength > 0 Then
                Dim err As String = Nothing
                ' The image goes into the database, not onto disk, so a pg_dump
                ' carries it and the QR survives a move to another machine.
                newQr = ReadQrImage(upload, err)
                If newQr IsNot Nothing AndAlso newQr.Length > 0 Then
                    newQrMime = MimeFor(IO.Path.GetExtension(upload.FileName))
                Else
                    ' Surface the failure instead of silently keeping the old image —
                    ' the admin would otherwise believe the QR was replaced.
                    uploadError = If(err, "Could not save the QR image.")
                End If
            End If
            Dim removeQr As Boolean = (Posted(channel, "removeQr") = "1")
            If removeQr Then s.QrImageFile = ""

            If uploadError <> "" Then
                Session("flash_msg") = uploadError
                Session("flash_ok") = False
                Response.Redirect("/App/Admin/PaymentSettings.aspx", True)
                Return
            End If

            ' Warn (not block) when the display mode needs something that is missing.
            Dim warnings As New List(Of String)()
            If (s.QrDisplayMode = "BOTH" OrElse s.QrDisplayMode = "QR_ONLY") AndAlso
               Not s.HasQrImage AndAlso (newQr Is Nothing) Then
                warnings.Add("no QR code image uploaded yet — customers will only see the " &
                             "account details until you upload one")
            End If
            If String.IsNullOrWhiteSpace(s.AccountName) AndAlso
               (s.QrDisplayMode = "BOTH" OrElse s.QrDisplayMode = "NUMBER_NAME" OrElse s.QrDisplayMode = "NAME_ONLY") Then
                warnings.Add("no account name set — that line will be hidden on the popup")
            End If

            If Not _settings.Save(s) Then
                Out.Text = WebUi.AlertBox("Unknown payment channel.")
                Render()
                Return
            End If

            ' Applied after Save so the row exists — UpdateQrImage is an UPDATE and
            ' would silently match nothing on a brand-new channel.
            If newQr IsNot Nothing AndAlso newQr.Length > 0 Then
                _settings.UpdateQrImage(s.Channel, newQr, newQrMime, s.UpdatedBy)
            ElseIf removeQr Then
                _settings.UpdateQrImage(s.Channel, Nothing, "", s.UpdatedBy)
            End If

            Dim flash As String = brand & " payment settings saved."
            If warnings.Count > 0 Then
                flash &= " Note: " & String.Join("; ", warnings) & "."
            End If
            Session("flash_msg") = flash
            Session("flash_ok") = True
            Response.Redirect("/App/Admin/PaymentSettings.aspx", True)
        End Sub

        ''' <summary>
        ''' Every input on this page lives in the master page's single form, one card per
        ''' channel. Input names are therefore namespaced by channel: with plain names
        ''' both cards post "accountName", Request.Form hands back a String[], and the
        ''' saved value silently becomes "gcashName,gotymeName". Worse, one card's
        ''' "isEnabled" or "removeQr" would act on the other wallet's row.
        ''' </summary>
        Private Shared Function Field(channel As String, name As String) As String
            Return channel & "_" & name
        End Function

        ''' <summary>
        ''' One namespaced value from the POST, trimmed; "" when the card sent none.
        ''' Read through GetValues rather than Convert.ToString(Request.Form(...)):
        ''' an unchecked box is simply absent from the POST, and an unchecked box on
        ''' the card being saved must read as "" rather than blowing up mid-save.
        ''' </summary>
        Private Function Posted(channel As String, name As String) As String
            Dim values As String() = Request.Form.GetValues(Field(channel, name))
            If values Is Nothing OrElse values.Length = 0 Then Return ""
            Return If(values(0), "").Trim()
        End Function

        ''' <summary>
        ''' The channel comes from the "Save &lt;brand&gt; Settings" button that was
        ''' clicked: each card posts its own name/value pair, because one page renders
        ''' every channel inside the master's single form. Scanned instead of read
        ''' straight off Request.Form so pressing Enter in a text box — which submits
        ''' the first submit button on the page — cannot hand back a mangled value.
        ''' </summary>
        Private Function PostedChannel() As String
            Dim values As String() = Request.Form.GetValues("channel")
            If values Is Nothing Then Return ""
            For Each raw As String In values
                Dim c As String = If(raw, "").Trim().ToUpperInvariant()
                If c = PaymentSettingRepository.Gcash OrElse c = PaymentSettingRepository.Gotyme Then
                    Return c
                End If
            Next
            Return ""
        End Function

        Private Function TrimTo(value As String, max As Integer) As String
            Dim v As String = If(value, "").Trim()
            If v.Length > max Then v = v.Substring(0, max)
            Return v
        End Function

        ''' <summary>
        ''' Saves an uploaded QR image under /Uploads/payments and returns the
        ''' root-relative path. Returns "" when the file is rejected, with a
        ''' customer-readable reason in <paramref name="err"/>.
        ''' </summary>
        Private Function ReadQrImage(f As HttpPostedFile, ByRef err As String) As Byte()
            err = ""
            Dim allowed As String() = {".png", ".jpg", ".jpeg", ".webp"}
            Dim ext As String = IO.Path.GetExtension(f.FileName).ToLowerInvariant()
            If Array.IndexOf(allowed, ext) < 0 Then
                err = "QR image must be a PNG, JPG, or WebP file."
                Return Nothing
            End If
            If f.ContentLength > 5 * 1024 * 1024 Then
                err = "QR image is too large — keep it under 5 MB."
                Return Nothing
            End If
            Try
                Dim buf As New IO.MemoryStream()
                f.InputStream.CopyTo(buf)
                Return buf.ToArray()
            Catch ex As Exception
                err = "Could not read the QR image: " & ex.Message
                Return Nothing
            End Try
        End Function

        Private Function MimeFor(ext As String) As String
            Select Case If(ext, "").ToLowerInvariant()
                Case ".png" : Return "image/png"
                Case ".jpg", ".jpeg" : Return "image/jpeg"
                Case ".gif" : Return "image/gif"
                Case ".webp" : Return "image/webp"
                Case Else : Return "application/octet-stream"
            End Select
        End Function

        ' ---------------- GET: the editor ----------------

        Private Sub Render()
            Dim all As List(Of PaymentSetting) = _settings.ListAll()

            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Payment Settings",
                                    "SYSTEM ADMIN · E-WALLET QR MANAGEMENT",
                                    "Control what customers see when they pay with GCash or GOtyme: the QR code, " &
                                    "the account number, the account name, and how much of it is displayed."))
            sb.Append(Flash())

            sb.Append("<div class=""sub"" style=""margin-bottom:14px"">" & WebUi.Ic("info", "sm") &
                      " Changes take effect immediately on the order payment popup and at checkout. " &
                      "Upload a PNG/JPG/WebP of each wallet's official QR code (max 5 MB). Display mode options: " &
                      "<b>QR code only</b>, <b>number + name only</b>, <b>name only</b>, or <b>everything</b>.</div>")

            sb.Append("<div class=""grid"" style=""grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:16px"">")
            For Each s As PaymentSetting In all
                sb.Append(ChannelCard(s))
            Next
            sb.Append("</div>")

            sb.Append("<div class=""card"" style=""margin-top:16px"">")
            sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("visibility", "sm") & " Preview — how customers see it</h3>")
            For Each s As PaymentSetting In all
                sb.Append(QrPreviewBlock(s, True))
            Next
            sb.Append("</div>")

            Out.Text = sb.ToString()
        End Sub

        Private Function ChannelCard(s As PaymentSetting) As String
            Dim isGcash As Boolean = String.Equals(s.Channel, PaymentSettingRepository.Gcash, StringComparison.OrdinalIgnoreCase)
            Dim brandColor As String = If(isGcash, "#007dfe", "#00a651")
            Dim brand As String = s.DisplayChannel
            ' Prefers the image stored in the database; falls back to an old file path.
            Dim qrFile As String = WebUi.QrImageUrl(s, s.Channel)

            Dim sb As New StringBuilder()
            sb.Append("<div class=""card"">")
            sb.Append("<div style=""display:flex;align-items:center;gap:10px;margin-bottom:12px"">")
            sb.Append("<span class=""ph-ic"" style=""width:36px;height:36px;font-size:18px;background:" & brandColor & ";color:#fff;border-radius:9px"">" &
                      WebUi.Ic(If(isGcash, "qr_code_2", "account_balance_wallet"), "sm") & "</span>")
            sb.Append("<div><b style=""font-size:15px"">" & WebUi.Esc(brand) & "</b><br><span class=""sub"" style=""font-size:11px"">Channel: " &
                      WebUi.Esc(s.Channel) & "</span></div>")
            sb.Append("<span style=""margin-left:auto"">" & If(s.IsEnabled, WebUi.Badge("ACTIVE"), WebUi.Badge("HIDDEN")) & "</span>")
            sb.Append("</div>")

            ' No <form> of its own: everything on this page posts through the master
            ' page's shell form, which is what carries enctype=multipart/form-data.
            sb.Append(Csrf.HiddenField())

            ' toggle
            sb.Append("<label class=""card"" style=""display:flex;gap:10px;align-items:center;margin-bottom:12px;cursor:pointer;background:var(--surface-low)"">")
            sb.Append("<input type=""checkbox"" name=""" & Field(s.Channel, "isEnabled") & """ value=""1""" & If(s.IsEnabled, " checked", "") & " style=""width:18px;height:18px"">")
            sb.Append("<span><b>Show " & WebUi.Esc(brand) & " at checkout</b><br><span class=""sub"" style=""font-size:11.5px"">" &
                      "When off, customers cannot choose " & WebUi.Esc(brand) & " as a payment method.</span></span>")
            sb.Append("</label>")

            ' account name
            sb.Append("<div class=""field""><label for=""an-" & WebUi.Attr(s.Channel) & """>Account name</label>")
            sb.Append("<input id=""an-" & WebUi.Attr(s.Channel) & """ name=""" & Field(s.Channel, "accountName") & """ maxlength=""120"" placeholder=""e.g. STAR:DOM ATELIER / JAMES M."" value=""" & WebUi.Attr(s.AccountName) & """></div>")

            ' account number
            sb.Append("<div class=""field""><label for=""num-" & WebUi.Attr(s.Channel) & """>" & WebUi.Esc(brand) & " number (QR number)</label>")
            sb.Append("<input id=""num-" & WebUi.Attr(s.Channel) & """ name=""" & Field(s.Channel, "accountNumber") & """ maxlength=""60"" inputmode=""tel"" placeholder=""09xx xxx xxxx"" value=""" & WebUi.Attr(s.AccountNumber) & """></div>")

            ' QR image upload + current state
            sb.Append("<div class=""field""><label for=""qr-" & WebUi.Attr(s.Channel) & """>QR code image</label>")
            If s.HasQrImage Then
                sb.Append("<div style=""display:flex;gap:12px;align-items:center;margin-bottom:8px"">")
                ' An image stored as a file by an older install can still be absent
                ' here (Uploads is not in Git), so it is flagged rather than rendered
                ' as a broken image. An image in the database cannot go missing.
                If qrFile = "" Then
                    sb.Append("<span class=""ph-ic"" style=""width:84px;height:84px;background:#ffe0de;color:var(--primary);border-radius:10px"">" &
                              WebUi.Ic("broken_image", "sm") & "</span>")
                    sb.Append("<span class=""sub"" style=""font-size:11.5px"">This QR is stored as a file from an older " &
                              "install and the file is not on this machine. Re-upload it to move it into the database.<br>" &
                              "<label class=""sub"" style=""font-size:11.5px;cursor:pointer;display:flex;gap:6px;align-items:center"">" &
                              "<input type=""checkbox"" name=""" & Field(s.Channel, "removeQr") & """ value=""1"" style=""width:16px;height:16px""> Clear the stale path</label></span>")
                Else
                    sb.Append("<img src=""" & WebUi.Attr(qrFile) & """ alt=""" & WebUi.Attr(brand & " QR code") & """ " &
                              "style=""width:84px;height:84px;object-fit:contain;border:1px solid var(--line);border-radius:10px;background:#fff;padding:4px"">")
                End If
                sb.Append("<label class=""sub"" style=""font-size:11.5px;cursor:pointer;display:flex;gap:6px;align-items:center"">" &
                          "<input type=""checkbox"" name=""" & Field(s.Channel, "removeQr") & """ value=""1"" style=""width:16px;height:16px""> Remove current QR image</label>")
                sb.Append("</div>")
            Else
                sb.Append("<div class=""sub"" style=""font-size:11.5px;margin-bottom:8px"">No QR image uploaded yet — the popup will fall back to the stylized placeholder.</div>")
            End If
            sb.Append("<input id=""qr-" & WebUi.Attr(s.Channel) & """ type=""file"" name=""" & Field(s.Channel, "qrImage") & """ accept=""image/png,image/jpeg,image/webp,.png,.jpg,.jpeg,.webp"">")
            sb.Append("<div class=""sub"" style=""font-size:11px;margin-top:4px"">PNG / JPG / WebP up to 5 MB. Leave empty to keep the current image.</div>")
            sb.Append("</div>")

            ' caption
            sb.Append("<div class=""field""><label for=""cap-" & WebUi.Attr(s.Channel) & """>Caption under the QR (optional)</label>")
            sb.Append("<input id=""cap-" & WebUi.Attr(s.Channel) & """ name=""" & Field(s.Channel, "qrCaption") & """ maxlength=""120"" placeholder=""e.g. Scan using the GCash app"" value=""" & WebUi.Attr(s.QrCaption) & """></div>")

            ' display mode
            sb.Append("<div class=""field""><label for=""dm-" & WebUi.Attr(s.Channel) & """>What customers see</label>")
            sb.Append("<select id=""dm-" & WebUi.Attr(s.Channel) & """ name=""" & Field(s.Channel, "qrDisplayMode") & """>")
            For Each opt As String() In {New String() {"BOTH", "Everything — QR code + number + name"},
                                         New String() {"QR_ONLY", "QR code only"},
                                         New String() {"NUMBER_NAME", "Number + name only (no QR image)"},
                                         New String() {"NAME_ONLY", "Account name only"}}
                sb.Append("<option value=""" & opt(0) & """" & If(s.QrDisplayMode = opt(0), " selected", "") & ">" & WebUi.Esc(opt(1)) & "</option>")
            Next
            sb.Append("</select></div>")

            ' meta + submit
            If s.UpdatedAt <> Date.MinValue Then
                sb.Append("<div class=""sub"" style=""font-size:11px;margin-bottom:8px"">Last updated " & s.UpdatedAt.ToString("MMM d, yyyy h:mm tt") &
                          If(s.UpdatedBy <> "", " by " & WebUi.Esc(s.UpdatedBy), "") & "</div>")
            End If
            sb.Append("<button class=""btn primary"" type=""submit"" name=""channel"" value=""" & WebUi.Attr(s.Channel) & """>" &
                      "<span class=""ic ms"">save</span><span>Save " & WebUi.Esc(brand) & " Settings</span></button>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' A read-only rendering of the customer-facing QR popup for one channel —
        ''' the same layout logic the order-detail page uses, minus the amount row.
        ''' Pass <paramref name="compact"/> to render it inside the admin preview card.
        ''' </summary>
        Private Function QrPreviewBlock(s As PaymentSetting, compact As Boolean) As String
            Dim isGcash As Boolean = String.Equals(s.Channel, PaymentSettingRepository.Gcash, StringComparison.OrdinalIgnoreCase)
            Dim brandColor As String = If(isGcash, "#007dfe", "#00a651")
            Dim brand As String = s.DisplayChannel
            Dim showQr As Boolean = (s.QrDisplayMode = "BOTH" OrElse s.QrDisplayMode = "QR_ONLY")
            Dim showNumber As Boolean = (s.QrDisplayMode = "BOTH" OrElse s.QrDisplayMode = "NUMBER_NAME")
            Dim showName As Boolean = (s.QrDisplayMode = "BOTH" OrElse s.QrDisplayMode = "NUMBER_NAME" OrElse s.QrDisplayMode = "NAME_ONLY")
            Dim qrFile As String = WebUi.QrImageUrl(s, s.Channel)

            Dim sb As New StringBuilder()
            sb.Append("<div style=""display:flex;gap:18px;align-items:flex-start;flex-wrap:wrap;padding:12px 0" & If(Not compact, "", ";border-top:1px solid var(--line)") & """>")
            sb.Append("<div style=""min-width:150px;text-align:center"">")

            If showQr Then
                If qrFile <> "" Then
                    sb.Append("<img src=""" & WebUi.Attr(qrFile) & """ alt=""" & WebUi.Attr(brand & " QR code") & """ " &
                              "style=""width:150px;height:150px;object-fit:contain;border:2px solid var(--line);border-radius:14px;background:#fff;padding:8px"">")
                Else
                    ' Same stylized fallback the order page uses while no image is uploaded.
                    sb.Append("<div style=""width:150px;height:150px;border:2px solid var(--line);border-radius:14px;background:#fff;display:flex;align-items:center;justify-content:center;color:var(--ink-soft);font-size:11px;text-align:center;padding:10px"">" &
                              "Placeholder QR<br>(upload the real " & WebUi.Esc(brand) & " QR image)</div>")
                End If
                If s.QrCaption <> "" Then
                    sb.Append("<div class=""sub"" style=""font-size:10.5px;margin-top:6px"">" & WebUi.Esc(s.QrCaption) & "</div>")
                End If
            End If
            sb.Append("</div>")

            sb.Append("<div style=""flex:1;min-width:200px"">")
            sb.Append("<div style=""display:flex;align-items:center;gap:8px;margin-bottom:8px"">" &
                      "<span class=""ph-ic"" style=""width:28px;height:28px;font-size:14px;background:" & brandColor & ";color:#fff;border-radius:7px"">" &
                      WebUi.Ic(If(isGcash, "qr_code_2", "account_balance_wallet"), "sm") & "</span>" &
                      "<b style=""font-size:13px"">Scan to Pay with " & WebUi.Esc(brand) & "</b></div>")
            If showName AndAlso s.AccountName <> "" Then
                sb.Append("<div class=""row space-between"" style=""margin-bottom:4px""><span class=""sub"" style=""font-size:11px"">Account Name</span><b style=""font-size:12px"">" & WebUi.Esc(s.AccountName) & "</b></div>")
            End If
            If showNumber AndAlso s.AccountNumber <> "" Then
                sb.Append("<div class=""row space-between"" style=""margin-bottom:4px""><span class=""sub"" style=""font-size:11px"">" & WebUi.Esc(brand) & " Number</span><b style=""font-size:13px;color:" & brandColor & ";font-family:var(--font-mono)"">" & WebUi.Esc(s.AccountNumber) & "</b></div>")
            End If
            If Not showQr AndAlso Not showNumber AndAlso Not showName Then
                sb.Append("<div class=""sub"" style=""font-size:11.5px"">Nothing is set to display — pick a display mode above.</div>")
            End If
            sb.Append("<div class=""row space-between""><span class=""sub"" style=""font-size:11px"">Display mode</span><b style=""font-size:11px"">" & WebUi.Esc(ModeLabel(s.QrDisplayMode)) & "</b></div>")
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function ModeLabel(mode As String) As String
            Select Case If(mode, "").ToUpperInvariant()
                Case "QR_ONLY" : Return "QR code only"
                Case "NUMBER_NAME" : Return "Number + name only"
                Case "NAME_ONLY" : Return "Account name only"
                Case Else : Return "Everything (QR + number + name)"
            End Select
        End Function

        ''' <summary>Same one-shot flash message convention used across the app.</summary>
        Private Function Flash() As String
            Dim msg As String = Convert.ToString(Session("flash_msg"))
            If msg = "" Then Return ""
            Session("flash_msg") = Nothing
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_ok") = Nothing
            Return WebUi.AlertBox(msg, If(ok, "ok", "err"))
        End Function

    End Class

End Namespace
