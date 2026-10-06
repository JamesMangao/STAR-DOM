Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class CommissionDetailPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _svc As New CommissionService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                Dim id As Integer = 0
                Integer.TryParse(Request.QueryString("id"), id)
                Dim cm As Commission = _svc.GetCommission(id)
                If cm Is Nothing Then
                    Out.Text = WebUi.AlertBox("Commission request not found.")
                    Return
                End If
                ' Ownership check, not a role check: CanManageStore alone is true for
                ' every merchant, which let any artist open any commission by id.
                If Not _svc.CanAccess(cm) Then
                    Out.Text = WebUi.AlertBox("You don't have access to this commission request.")
                    Return
                End If
                ' No MarkRead call: it marked CommissionMessages rows as read, and
                ' that table is gone with the message thread.

                ' Wrap in Trim(), VB's null-safe String trim. Convert.ToString(Nothing)
                ' hands back Nothing on this runtime, so an instance method chained
                ' straight onto it throws NullReferenceException - and a plain
                ' "View / Reply" link arrives with no ?act at all, which is exactly
                ' the case that used to blank this page.
                Dim act As String = Trim(Convert.ToString(Request.QueryString("act"))).ToLowerInvariant()
                If Guard.IsPost() Then
                    Dim result As ServiceResult = HandlePost(cm)
                    Session("flash_msg") = result.Message
                    Session("flash_ok") = result.Success
                    Response.Redirect("/App/CommissionDetail.aspx?id=" & id.ToString(), True)
                ElseIf act <> "" Then
                    Dim result As ServiceResult = HandleGetAction(cm, act)
                    If result IsNot Nothing Then
                        Session("flash_msg") = result.Message
                        Session("flash_ok") = result.Success
                        Response.Redirect("/App/CommissionDetail.aspx?id=" & id.ToString(), True)
                    End If
                End If

                Render(cm)
            Catch ex As Exception
                STAR_DOM.Database.Db.LogError("CommissionDetail", ex)
                Out.Text = WebUi.AlertBox("Could not load this commission: " & ex.Message)
            End Try
        End Sub

        ' ---------------- actions ----------------

        Private Function HandleGetAction(cm As Commission, act As String) As ServiceResult
            Select Case act
                Case "confirmoffer"
                    Return _svc.ConfirmOffer(cm.Id)
                Case "startprod"
                    Return _svc.StartProduction(cm.Id)
                Case "revision"
                    Return _svc.RequestRevision(cm.Id)
                Case "finalize"
                    Return _svc.FinalizeWork(cm.Id)
                ' "confirmpay" / "declinepay" are deliberately absent: both need the
                ' studio's password, so they only exist as POST forms. A GET link
                ' could confirm real money from a stray click or a link preview.
                Case "cancel"
                    Return _svc.Cancel(cm.Id)
                Case Else
                    Return Nothing
            End Select
        End Function

        Private Function HandlePost(cm As Commission) As ServiceResult
            Dim kind As String = Convert.ToString(Request.Form("kind"))
            Select Case kind
                Case "decline"
                    If Not _svc.CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
                    Return _svc.Decline(cm.Id, Convert.ToString(Request.Form("note")))
                Case "offer"
                    If Not _svc.CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
                    Dim price As Decimal = 0D
                    Decimal.TryParse(Request.Form("price"), price)
                    Dim est As Date? = Nothing
                    Dim dt As String = Convert.ToString(Request.Form("est"))
                    If dt <> "" Then
                        Dim d As Date
                        If Date.TryParse(dt, d) Then est = d
                    End If
                    ' No deposit field: the artist sets the price of the finished piece
                    ' and the customer pays that one figure in full.
                    Return _svc.AcceptAndOffer(cm.Id, price, est, Convert.ToString(Request.Form("merchantNotes")))
                Case "pay"
                    ' The customer submits what they paid against; it still has to be
                    ' verified by the studio before production opens.
                    Dim ref As String = Convert.ToString(Request.Form("ref"))
                    Return _svc.SubmitPayment(cm.Id, ref)
                Case "confirmpay"
                    If Not _svc.CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
                    Return _svc.ConfirmPayment(cm.Id, Convert.ToString(Request.Form("pw")))
                Case "declinepay"
                    If Not _svc.CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
                    Return _svc.DenyPayment(cm.Id, Convert.ToString(Request.Form("pw")))
                Case "deliver"
                    If Not _svc.CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
                    Return _svc.MarkDelivered(cm.Id, Convert.ToString(Request.Form("tracking")))
                Case "received"
                    Return _svc.MarkReceived(cm.Id)
                Case Else
                    Return ServiceResult.Fail("Unknown action.")
            End Select
        End Function

        ' ---------------- render ----------------

        Private Sub Render(cm As Commission)
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            Dim backUrl As String = If(STAR_DOM.Helpers.Session.CanManageStore, "/App/Merchant/Pipeline.aspx", "/App/CommissionHub.aspx")
            sb.Append("<a href=""" & backUrl & """ class=""sub"" style=""display:inline-flex;align-items:center;gap:6px"">" & WebUi.Ic("arrow_back", "sm") & " Back</a>")
            sb.Append(WebUi.Section(cm.CommissionNumber, "COMMISSION " & cm.StatusDisplay.ToUpperInvariant(),
                                    cm.Title & " · for " & WebUi.Esc(cm.MerchantName)))
            sb.Append(WebUi.Badge(cm.Status))

            sb.Append("<div class=""row"" style=""align-items:flex-start;gap:24px;margin-top:14px"">")

            ' ---- left: request details + thread ----
            sb.Append("<div style=""flex:1.7;min-width:320px"">")
            sb.Append("<div class=""card mb""><div class=""kv"">")
            sb.Append("<dt>Category</dt><dd>" & WebUi.Esc(cm.CategoryName) & "</dd>")
            sb.Append("<dt>Quantity</dt><dd>" & cm.Quantity.ToString() & "</dd>")
            sb.Append("<dt>Size</dt><dd>" & WebUi.Esc(cm.PreferredSize) & "</dd>")
            If cm.PreferredDeadline.HasValue Then sb.Append("<dt>Deadline</dt><dd>" & WebUi.Esc(cm.PreferredDeadline.Value.ToString("MMM d, yyyy")) & "</dd>")
            If cm.BudgetMin.HasValue AndAlso cm.BudgetMax.HasValue Then
                sb.Append("<dt>Budget</dt><dd>" & WebUi.Money(cm.BudgetMin) & " – " & WebUi.Money(cm.BudgetMax) & "</dd>")
            ElseIf cm.BudgetMax.HasValue Then
                sb.Append("<dt>Budget</dt><dd>up to " & WebUi.Money(cm.BudgetMax) & "</dd>")
            End If
            ' Delivery details ride along with the request because the finished piece
            ' is delivered — there is no counter for the customer to collect it from.
            sb.Append("<dt>Deliver to</dt><dd>" & WebUi.Esc(If(cm.ShippingAddress = "", "— not provided —", cm.ShippingAddress)) & "</dd>")
            sb.Append("<dt>Phone</dt><dd>" & WebUi.Esc(If(cm.ContactPhone = "", "—", cm.ContactPhone)) & "</dd>")
            sb.Append("<dt>Shipping</dt><dd>Free — the studio covers the courier</dd>")
            sb.Append("</div>")
            sb.Append("<p style=""margin:10px 0 0"">" & WebUi.Esc(cm.Description) & "</p>")
            If cm.AdditionalNotes <> "" Then sb.Append("<p class=""sub""><b>Notes:</b> " & WebUi.Esc(cm.AdditionalNotes) & "</p>")
            sb.Append("</div>")
            sb.Append(ReferenceImages(cm.Id))

            ' offer details (merchant side)
            If cm.FinalPrice.HasValue Then
                sb.Append("<div class=""card mb"" style=""border-color:#eec200"">")
                sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("request_quote", "sm") & " Official offer</h3>")
                sb.Append("<div class=""kv"">")
                sb.Append("<dt>Final price</dt><dd>" & WebUi.Money(cm.FinalPrice) & "</dd>")
                sb.Append("<dt>Shipping</dt><dd>Free</dd>")
                If cm.EstimatedCompletionDate.HasValue Then
                    sb.Append("<dt>Est. completion</dt><dd>" & WebUi.Esc(cm.EstimatedCompletionDate.Value.ToString("MMM d, yyyy")) & "</dd>")
                End If
                sb.Append("<dt>Payment</dt><dd>Paid in full — no deposit</dd>")
                sb.Append("<dt>Payment state</dt><dd>" & PaymentStateLine(cm) & "</dd>")
                sb.Append("</div>")
                If cm.MerchantNotes <> "" Then sb.Append("<p class=""sub"">" & WebUi.Esc(cm.MerchantNotes) & "</p>")
                sb.Append("</div>")
            End If

            ' ---- actions ----
            Dim actions As String = BuildActions(cm)
            If actions <> "" Then
                sb.Append("<div class=""card mb"">" & actions & "</div>")
            End If

            ' A declined payment is a dead end for the customer — hand them the
            ' support contact right where they see the DECLINED state, the same
            ' way a declined order payment does on OrderDetail.
            If cm.CustomerId = STAR_DOM.Helpers.Session.CurrentUser.Id AndAlso
               String.Equals(cm.Status, CommissionStatuses.PaymentDeclined, StringComparison.OrdinalIgnoreCase) Then
                sb.Append(WebUi.PaymentFailureNote("Commission / payment failed"))
            End If

            ' ---- commission policy notice ----
            ' The message thread and "Request Clarification" are gone. The studio
            ' takes a request or declines it, so a vague brief has nowhere to go
            ' but a declined request; the policy is stated up front instead.
            sb.Append("<div class=""card mb"" style=""border-color:#eec200;background:var(--yellow-soft)"">")
            sb.Append("<h3 style=""margin-bottom:6px"">" & WebUi.Ic("info", "sm") & " Before you commission</h3>")
            sb.Append("<p class=""sub"" style=""margin:0 0 6px"">Please provide a detailed description. Vague requests are " &
                      "subjected to be declined.</p>")
            sb.Append("<p class=""sub"" style=""margin:0"">Note that once a commission is in production, it cannot be canceled.</p>")
            sb.Append("</div>")
            sb.Append("</div>")

            ' ---- right: timeline / history ----
            sb.Append("<div style=""flex:1;min-width:280px"">")
            Dim history As List(Of CommissionStatusHistory) = _svc.ListStatusHistory(cm.Id).OrderByDescending(Function(h) h.Id).ToList()
            sb.Append("<div class=""card mb"">")
            sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("history", "sm") & " Status history</h3>")
            If history.Count > 0 Then
                sb.Append("<ul class=""timeline"">")
                For Each h In history
                    sb.Append("<li class=""now""><b>" & WebUi.Esc(h.ToStatus.Replace("_", " ")) & "</b> — " &
                              WebUi.Esc(h.ChangedByName) & " <time>" & WebUi.Esc(h.CreatedAt.ToString("MMM d, yyyy h:mm tt")) &
                              If(h.Note <> "", " · " & WebUi.Esc(h.Note), "") & "</time></li>")
                Next
                sb.Append("</ul>")
            Else
                sb.Append(WebUi.EmptyRow("History will appear as the request moves through the pipeline."))
            End If
            sb.Append("</div>")

            sb.Append("<div class=""card""><div class=""kv"">")
            sb.Append("<dt>Submitted</dt><dd>" & WebUi.Esc(cm.CreatedAt.ToString("MMM d, yyyy h:mm tt")) & "</dd>")
            sb.Append("<dt>Last update</dt><dd>" & WebUi.Esc(cm.UpdatedAt.ToString("MMM d, yyyy h:mm tt")) & "</dd>")
            sb.Append("<dt>Customer</dt><dd>" & WebUi.Esc(cm.CustomerName) & "</dd>")
            ' The courier sentence is the customer's side of the hand-off, so it only
            ' appears once there is something to say about it.
            If cm.Status = "DELIVERED" OrElse cm.Status = "RECEIVED" OrElse cm.Status = "COMPLETED" Then
                sb.Append("<dt>Delivery</dt><dd>" & WebUi.Esc(cm.DeliveryStatusLine))
                If cm.TrackingUrl <> "" Then
                    sb.Append(" <a href=""" & WebUi.Attr(cm.TrackingUrl) & """ target=""_blank"" rel=""noopener"">Track on J&amp;T <span class=""ms sm"" style=""vertical-align:-3px"">open_in_new</span></a>")
                End If
                sb.Append("</dd>")
            End If
            sb.Append("</div></div>")
            sb.Append("</div></div>")

            Out.Text = sb.ToString()
        End Sub

        ' ---------- contextual action panels ----------

        ''' <summary>
        ''' How the money stands. A reference on file is not a confirmed payment:
        ''' the studio still has to verify it, and production stays shut until then.
        ''' </summary>
        Private Function PaymentStateLine(cm As Commission) As String
            If cm.PaymentConfirmed Then
                Return WebUi.Badge("PAID") & " <span class=""sub"" style=""font-size:11.5px"">confirmed " &
                       WebUi.Esc(cm.PaymentConfirmedAt.Value.ToString("MMM d, yyyy")) &
                       If(cm.PaymentReference <> "", " · ref " & WebUi.Esc(cm.PaymentReference), "") & "</span>"
            End If
            ' A declined reference is still on file — without this branch it read as
            ' "awaiting confirmation", which is exactly what it is not.
            If String.Equals(cm.Status, CommissionStatuses.PaymentDeclined, StringComparison.OrdinalIgnoreCase) Then
                Return WebUi.Badge("DECLINED") & " <span class=""sub"" style=""font-size:11.5px"">" &
                       If(cm.PaymentReference <> "", "ref " & WebUi.Esc(cm.PaymentReference) & " was declined — ", "") &
                       "waiting for the customer to resubmit</span>"
            End If
            If cm.PaymentReference <> "" Then
                Return WebUi.Badge("AWAITING CONFIRMATION") & " <span class=""sub"" style=""font-size:11.5px"">ref " &
                       WebUi.Esc(cm.PaymentReference) & " — the studio is verifying it</span>"
            End If
            Return WebUi.Badge("NOT PAID")
        End Function

        Private Function ReferenceImages(cmId As Integer) As String
            ' CommissionReferenceImages rows were written on upload since forever, but
            ' nothing ever read them: ListReferenceImages() had no caller, so the
            ' artwork the customer attached was invisible to both sides. Files live
            ' under /Uploads/comm and are served straight off disk (the folder is
            ' git-ignored), so the stored relative path is the URL.
            Dim refs As List(Of CommissionReferenceImage) = _svc.ReferenceImages(cmId)
            If refs Is Nothing OrElse refs.Count = 0 Then Return ""

            Dim sb As New StringBuilder()
            sb.Append("<div class=""card mb"">")
            sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("image", "sm") & " Reference images (" & refs.Count.ToString() & ")</h3>")
            sb.Append("<div style=""display:flex;gap:10px;flex-wrap:wrap"">")
            For Each r In refs
                Dim url As String = "/" & If(r.ImageFile, "").TrimStart("/"c)
                Dim isPdf As Boolean = url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                sb.Append("<a href=""" & WebUi.Attr(url) & """ target=""_blank"" rel=""noopener"" " &
                          "style=""display:block;border:1px solid var(--line);border-radius:12px;overflow:hidden;" &
                          "max-width:190px;background:var(--surface-low)"" title=""" & WebUi.Attr(r.FileName) & """>")
                If isPdf Then
                    sb.Append("<span style=""display:flex;align-items:center;justify-content:center;height:120px; " &
                              "font-size:12px;font-weight:700;color:var(--primary)"">PDF</span>")
                Else
                    sb.Append("<img src=""" & WebUi.Attr(url) & """ alt=""" & WebUi.Attr(r.FileName) & """ " &
                              "style=""width:190px;height:120px;object-fit:cover;display:block"" loading=""lazy"" />")
                End If
                sb.Append("</a>")
            Next
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        Private Function BuildActions(cm As Commission) As String
            Dim sb As New StringBuilder()
            Dim st As String = cm.Status.ToUpperInvariant()
            Dim isMerchant As Boolean = _svc.CanManageCommission(cm)
            Dim isOwner As Boolean = cm.CustomerId = STAR_DOM.Helpers.Session.CurrentUser.Id
            Dim showPanel As String = Trim(Convert.ToString(Request.QueryString("panel"))).ToLowerInvariant()

            sb.Append("<h3 style=""margin-bottom:8px"">Actions</h3>")
            sb.Append("<div class=""frow"">")

            If isMerchant Then
                If st = "PENDING REVIEW" OrElse st = "SUBMITTED" Then
                    sb.Append(PanelLink(cm, "offer", "Accept & Send Offer", "primary", showPanel, "send"))
                    sb.Append(PanelLink(cm, "decline", "Decline", "ghost", showPanel, "thumb_down"))
                ElseIf st = "PAYMENT PENDING" Then
                    ' The customer says they paid; the studio verifies before anything
                    ' gets made. Password-gated POST, same as an order's payment: the
                    ' reference is shown read-only because verifying means checking
                    ' THEIR number, never typing one in on their behalf.
                    sb.Append("<form method=""post"" action=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & """ " &
                              "style=""display:flex;gap:10px;align-items:flex-end;flex-wrap:wrap"">")
                    sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                    sb.Append("<div class=""field"" style=""margin:0""><label>Reference submitted</label>" &
                              "<input value=""" & WebUi.Attr(cm.PaymentReference) & """ readonly></div>")
                    sb.Append("<div class=""field"" style=""margin:0""><label>Your password *</label>" &
                              "<input name=""pw"" type=""password"" placeholder=""Re-enter to verify"" autocomplete=""current-password""></div>")
                    sb.Append("<button class=""btn primary"" type=""submit"" name=""kind"" value=""confirmpay"" " +
                              "data-confirm=""Confirm this payment so production can start?""><span class=""ic ms"">verified</span><span>Confirm payment</span></button>")
                    sb.Append("<button class=""btn danger"" type=""submit"" name=""kind"" value=""declinepay"" " +
                              "data-confirm=""Decline this payment? The customer will be notified."" data-confirm-danger""><span class=""ic ms"">cancel</span><span>Decline payment</span></button>")
                    sb.Append("</form>")
                    sb.Append("<span class=""act-hint"" style=""display:block;margin-top:6px"">Check the customer's reference (" &
                              WebUi.Esc(cm.PaymentReference) & ") against their wallet receipt first.</span>")
                ElseIf st = "PAID" Then
                    sb.Append("<a class=""btn primary"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=startprod""><span class=""ic ms"">factory</span><span>Start Production</span></a>")
                ElseIf st = "IN PRODUCTION" Then
                    sb.Append("<a class=""btn secondary"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=finalize""><span class=""ic ms"">check_circle</span><span>Finalize Work</span></a>")
                    sb.Append("<a class=""btn ghost"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=revision""><span class=""ic ms"">refresh</span><span>Request Revision</span></a>")
                ElseIf st = "FINALIZED" OrElse st = "REVISION" Then
                    sb.Append(PanelLink(cm, "deliver", "Deliver to customer", "primary", showPanel, "local_shipping"))
                End If
            End If

            If isOwner Then
                If st = "OFFER SENT" Then
                    sb.Append("<a class=""btn primary"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=confirmoffer""><span class=""ic ms"">how_to_reg</span><span>Confirm Offer</span></a>")
                End If
                If st = "CUSTOMER CONFIRMED" Then
                    sb.Append(PanelLink(cm, "pay", "Pay in Full", "primary", showPanel, "payments"))
                End If
                If st = "DELIVERED" Then
                    sb.Append("<form method=""post"" action=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & """ " +
                              "style=""display:inline-flex"">")
                    sb.Append(STAR_DOM.Web.Csrf.HiddenField())
                    sb.Append("<input type=""hidden"" name=""kind"" value=""received"">")
                    sb.Append("<button class=""btn primary"" type=""submit"" data-confirm=""Confirm this commission reached you?"">" +
                              "<span class=""ic ms"">task_alt</span><span>Mark as received</span></button>")
                    sb.Append("</form>")
                End If
                If st = "PENDING REVIEW" OrElse st = "SUBMITTED" OrElse st = "OFFER SENT" Then
                    sb.Append("<a class=""btn danger"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() &
                              "&act=cancel"" data-confirm=""Cancel this request?"" data-confirm-danger""><span class=""ic ms"">cancel</span><span>Cancel Request</span></a>")
                End If
            End If
            sb.Append("</div>")

            ' panel forms
            Select Case showPanel
                Case "offer"
                    sb.Append(PanelForm(cm, "offer", "Accept & price this commission",
                                        "<p class=""sub"" style=""margin:0 0 10px"">You set the price based on this request. " &
                                        "The customer pays this amount in full — there is no deposit.</p>" &
                                        "<div class=""field""><label>Final price (₱)</label><input name=""price"" type=""number"" step=""0.01"" required></div>" &
                                        "<div class=""field""><label>Estimated completion</label><input name=""est"" type=""date""></div>" &
                                        "<div class=""field""><label>Notes for the customer</label><textarea name=""merchantNotes"" style=""min-height:60px""></textarea></div>"))
                Case "decline"
                    sb.Append(PanelForm(cm, "decline", "Decline this request",
                                        "<div class=""field""><label>Reason (shared with the customer)</label><textarea name=""note"" required style=""min-height:80px""></textarea></div>"))
                Case "pay"
                    Dim due As Decimal = If(cm.FinalPrice.HasValue, cm.FinalPrice.Value, 0D)
                    sb.Append(PanelForm(cm, "pay", "Pay in full",
                                        "<p class=""sub"" style=""margin:0 0 10px"">Amount due: <b style=""color:var(--primary)"">" &
                                        WebUi.Money(due) & "</b> — one payment, no deposit. Shipping is free.</p>" &
                                        "<div class=""field""><label>GCash / GOtyme reference no. *</label>" &
                                        "<input name=""ref"" required placeholder=""From your payment receipt""></div>" &
                                        "<p class=""sub"" style=""margin:0"">The studio verifies this before production starts.</p>"))
                Case "deliver"
                    sb.Append(PanelForm(cm, "deliver", "Deliver to the customer",
                                        "<p class=""sub"" style=""margin:0 0 10px"">Delivering to <b>" &
                                        WebUi.Esc(cm.ShippingAddress) & "</b>. Shipping is free on commissions.</p>" &
                                        "<div class=""field""><label>J&amp;T tracking no. (optional)</label>" &
                                        "<input name=""tracking"" placeholder=""Leave blank for pickup or a personal handover""></div>"))
            End Select
            Return sb.ToString()
        End Function

        Private Function PanelLink(cm As Commission, panel As String, label As String, kind As String, showPanel As String, Optional icon As String = "") As String
            Dim url As String = "/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&panel=" & panel
            Dim ic As String = ""
            If icon <> "" Then ic = "<span class=""ic ms"">" & WebUi.Esc(icon) & "</span>"
            If showPanel = panel Then
                Return "<a class=""btn " & kind & """ href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & """>" & WebUi.Ic("close", "sm") & "<span>Close</span></a>"
            End If
            Return "<a class=""btn " & kind & """ href=""" & url & """>" & ic & "<span>" & WebUi.Esc(label) & "</span></a>"
        End Function

        Private Function PanelForm(cm As Commission, kind As String, title As String, fieldsHtml As String) As String
            Dim sb As New StringBuilder()
            sb.Append("<form method=""post"" action=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & """ style=""margin-top:12px;border-top:1px solid var(--line);padding-top:12px"">")
            sb.Append("<input type=""hidden"" name=""kind"" value=""" & kind & """>")
            sb.Append("<h4 style=""margin:0 0 8px"">" & WebUi.Esc(title) & "</h4>")
            sb.Append(fieldsHtml)
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">check</span><span>Confirm</span></button>")
            sb.Append("</form>")
            Return sb.ToString()
        End Function

    End Class

End Namespace