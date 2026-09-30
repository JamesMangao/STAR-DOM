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
                If cm.CustomerId <> STAR_DOM.Helpers.Session.CurrentUser.Id AndAlso Not STAR_DOM.Helpers.Session.CanManageStore Then
                    Out.Text = WebUi.AlertBox("You don't have access to this commission request.")
                    Return
                End If
                _svc.MarkRead(id)

                Dim act As String = CStr(Request.QueryString("act")).ToLowerInvariant()
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
                Case "complete"
                    Return _svc.Complete(cm.Id)
                Case "cancel"
                    Return _svc.Cancel(cm.Id)
                Case Else
                    Return Nothing
            End Select
        End Function

        Private Function HandlePost(cm As Commission) As ServiceResult
            Dim kind As String = Convert.ToString(Request.Form("kind"))
            Select Case kind
                Case "clarify"
                    If Not STAR_DOM.Helpers.Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
                    Return _svc.RequestClarification(cm.Id, Convert.ToString(Request.Form("message")))
                Case "decline"
                    If Not STAR_DOM.Helpers.Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
                    Return _svc.Decline(cm.Id, Convert.ToString(Request.Form("note")))
                Case "offer"
                    If Not STAR_DOM.Helpers.Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
                    Dim price As Decimal = 0D
                    Decimal.TryParse(Request.Form("price"), price)
                    Dim deposit As Decimal? = Nothing
                    Dim dep As String = Convert.ToString(Request.Form("deposit"))
                    If dep <> "" Then
                        Dim d As Decimal
                        If Decimal.TryParse(dep, d) Then deposit = d
                    End If
                    Dim est As Date? = Nothing
                    Dim dt As String = Convert.ToString(Request.Form("est"))
                    If dt <> "" Then
                        Dim d As Date
                        If Date.TryParse(dt, d) Then est = d
                    End If
                    Return _svc.AcceptAndOffer(cm.Id, price, est, Convert.ToString(Request.Form("merchantNotes")), deposit)
                Case "reply"
                    ' customer (or staff) replies to a clarification request
                    Return _svc.ReplyToClarification(cm.Id, Convert.ToString(Request.Form("message")), cm)
                Case "pay"
                    Dim ref As String = Convert.ToString(Request.Form("ref"))
                    Return _svc.MarkPaid(cm.Id, ref)
                Case "message"
                    Return _svc.SendMessage(cm.Id, Convert.ToString(Request.Form("message")))
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
            sb.Append("</div>")
            sb.Append("<p style=""margin:10px 0 0"">" & WebUi.Esc(cm.Description) & "</p>")
            If cm.AdditionalNotes <> "" Then sb.Append("<p class=""sub""><b>Notes:</b> " & WebUi.Esc(cm.AdditionalNotes) & "</p>")
            sb.Append("</div>")

            ' offer details (merchant side)
            If cm.FinalPrice.HasValue Then
                sb.Append("<div class=""card mb"" style=""border-color:#eec200"">")
                sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("request_quote", "sm") & " Official offer</h3>")
                sb.Append("<div class=""kv"">")
                sb.Append("<dt>Final price</dt><dd>" & WebUi.Money(cm.FinalPrice) & "</dd>")
                If cm.DepositAmount.HasValue Then sb.Append("<dt>Deposit</dt><dd>" & WebUi.Money(cm.DepositAmount) & "</dd>")
                If cm.EstimatedCompletionDate.HasValue Then
                    sb.Append("<dt>Est. completion</dt><dd>" & WebUi.Esc(cm.EstimatedCompletionDate.Value.ToString("MMM d, yyyy")) & "</dd>")
                End If
                sb.Append("</div>")
                If cm.MerchantNotes <> "" Then sb.Append("<p class=""sub"">" & WebUi.Esc(cm.MerchantNotes) & "</p>")
                sb.Append("</div>")
            End If

            ' ---- actions ----
            Dim actions As String = BuildActions(cm)
            If actions <> "" Then
                sb.Append("<div class=""card mb"">" & actions & "</div>")
            End If

            ' ---- message thread ----
            Dim msgs As List(Of CommissionMessage) = _svc.ListMessages(cm.Id)
            sb.Append("<div class=""card"">")
            sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("forum", "sm") & " Message thread (" & msgs.Count.ToString() & ")</h3>")
            If msgs.Count > 0 Then
                sb.Append("<div class=""flex-col"" style=""max-height:340px;overflow:auto;padding:2px"">")
                For Each m As CommissionMessage In msgs
                    Dim mine As Boolean = m.SenderId = STAR_DOM.Helpers.Session.CurrentUser.Id
                    sb.Append("<div class=""card"" style=""align-self:" & If(mine, "flex-end;background:var(--surface-low)", "flex-start") &
                              ";width:88%;padding:10px 12px"">")
                    sb.Append("<b style=""font-size:12px"">" & WebUi.Esc(m.SenderName) & "</b> " &
                              "<span class=""sub"" style=""font-size:11px"">" & WebUi.Esc(m.CreatedAt.ToString("MMM d, h:mm tt")) & "</span>")
                    sb.Append("<p style=""margin:4px 0 0"">" & WebUi.Esc(m.Message) & "</p>")
                    sb.Append("</div>")
                Next
                sb.Append("</div>")
            Else
                sb.Append(WebUi.EmptyRow("No messages yet."))
            End If

            ' message composer
            sb.Append("<form method=""post"" action=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & """>")
            sb.Append("<input type=""hidden"" name=""kind"" value=""message"">")
            sb.Append("<div class=""field"" style=""margin-top:10px""><textarea name=""message"" required style=""min-height:60px"" placeholder=""Send a message…""></textarea></div>")
            sb.Append("<button class=""btn secondary"" type=""submit""><span class=""ic ms"">send</span><span>Send Message</span></button>")
            sb.Append("</form>")
            sb.Append("</div>")
            sb.Append("</div>")

            ' ---- right: timeline / history ----
            sb.Append("<div style=""flex:1;min-width:280px"">")
            Dim history As List(Of CommissionStatusHistory) = _svc.ListStatusHistory(cm.Id).OrderByDescending(Function(h) h.Id).ToList()
            sb.Append("<div class=""card mb"">")
            sb.Append("<h3 style=""margin-bottom:8px"">" & WebUi.Ic("history", "sm") & " Status history</h3>")
            If history.Count > 0 Then
                sb.Append("<ul class=""timeline"">")
                For Each h As CommissionStatusHistory In history
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
            sb.Append("</div></div>")
            sb.Append("</div></div>")

            Out.Text = sb.ToString()
        End Sub

        ' ---------- contextual action panels ----------

        Private Function BuildActions(cm As Commission) As String
            Dim sb As New StringBuilder()
            Dim st As String = cm.Status.ToUpperInvariant()
            Dim isMerchant As Boolean = STAR_DOM.Helpers.Session.CanManageStore
            Dim isOwner As Boolean = cm.CustomerId = STAR_DOM.Helpers.Session.CurrentUser.Id
            Dim showPanel As String = CStr(Request.QueryString("panel")).ToLowerInvariant()

            sb.Append("<h3 style=""margin-bottom:8px"">Actions</h3>")
            sb.Append("<div class=""frow"">")

            If isMerchant Then
                If st = "PENDING REVIEW" OrElse st = "CLARIFICATION REQUESTED" OrElse st = "SUBMITTED" Then
                    sb.Append(PanelLink(cm, "offer", "Accept & Send Offer", "primary", showPanel, "send"))
                    sb.Append(PanelLink(cm, "clarify", "Request Clarification", "secondary", showPanel, "help"))
                    sb.Append(PanelLink(cm, "decline", "Decline", "ghost", showPanel, "thumb_down"))
                ElseIf st = "PAID" Then
                    sb.Append("<a class=""btn primary"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=startprod""><span class=""ic ms"">factory</span><span>Start Production</span></a>")
                ElseIf st = "IN PRODUCTION" Then
                    sb.Append("<a class=""btn secondary"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=finalize""><span class=""ic ms"">check_circle</span><span>Finalize Work</span></a>")
                    sb.Append("<a class=""btn ghost"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=revision""><span class=""ic ms"">refresh</span><span>Request Revision</span></a>")
                ElseIf st = "FINALIZED" OrElse st = "REVISION" OrElse st = "COMPLETED" Then
                    sb.Append("<a class=""btn primary"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=complete""><span class=""ic ms"">flag</span><span>Mark Completed</span></a>")
                End If
            End If

            If isOwner Then
                If st = "OFFER SENT" Then
                    sb.Append("<a class=""btn primary"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() & "&act=confirmoffer""><span class=""ic ms"">how_to_reg</span><span>Confirm Offer</span></a>")
                End If
                If st = "PAYMENT PENDING" OrElse st = "CUSTOMER CONFIRMED" OrElse st = "PAID" Then
                    sb.Append(PanelLink(cm, "pay", "Pay Deposit", "primary", showPanel, "payments"))
                End If
                If st = "CLARIFICATION REQUESTED" Then
                    sb.Append(PanelLink(cm, "reply", "Reply & Resubmit", "primary", showPanel, "reply"))
                End If
                If st = "PENDING REVIEW" OrElse st = "SUBMITTED" OrElse st = "CLARIFICATION REQUESTED" OrElse st = "OFFER SENT" Then
                    sb.Append("<a class=""btn danger"" href=""/App/CommissionDetail.aspx?id=" & cm.Id.ToString() &
                              "&act=cancel"" data-confirm=""Cancel this request?"" data-confirm-danger""><span class=""ic ms"">cancel</span><span>Cancel Request</span></a>")
                End If
            End If
            sb.Append("</div>")

            ' panel forms
            Select Case showPanel
                Case "offer"
                    sb.Append(PanelForm(cm, "offer", "Send the official offer",
                                        "<div class=""field""><label>Final price (₱)</label><input name=""price"" type=""number"" step=""0.01"" required></div>" &
                                        "<div class=""field""><label>Estimated completion</label><input name=""est"" type=""date""></div>" &
                                        "<div class=""field""><label>Deposit (₱, optional)</label><input name=""deposit"" type=""number"" step=""0.01""></div>" &
                                        "<div class=""field""><label>Merchant notes</label><textarea name=""merchantNotes"" style=""min-height:60px""></textarea></div>"))
                Case "clarify"
                    sb.Append(PanelForm(cm, "clarify", "Request clarification from the customer",
                                        "<div class=""field""><label>What do you need clarified?</label><textarea name=""message"" required style=""min-height:80px""></textarea></div>"))
                Case "decline"
                    sb.Append(PanelForm(cm, "decline", "Decline this request",
                                        "<div class=""field""><label>Reason (shared with the customer)</label><textarea name=""note"" required style=""min-height:80px""></textarea></div>"))
                Case "reply"
                    sb.Append(PanelForm(cm, "reply", "Reply to the merchant's clarification",
                                        "<div class=""field""><label>Your reply / updated details</label><textarea name=""message"" required style=""min-height:100px""></textarea></div>"))
                Case "pay"
                    sb.Append(PanelForm(cm, "pay", "Record deposit payment (simulated)",
                                        "<div class=""field""><label>Payment reference (GCash/Maya transaction no.)</label><input name=""ref"" placeholder=""Optional — auto-generated if blank""></div>"))
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
