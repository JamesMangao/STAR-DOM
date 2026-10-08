Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class NotificationsPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _notif As New NotificationService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                ' Deleting a notification changes state, so it is a POST: the link below
                ' used to point at ?del= with no handler at all, and the delete button
                ' did nothing.
                If Guard.IsPost() AndAlso Request.Form("delNotif") IsNot Nothing Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.Form("delNotif"), id)
                    If id > 0 Then _notif.Delete(id)
                    Response.Redirect("/App/Notifications.aspx", True)
                End If
                If Request.QueryString("read") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("read"), id)
                    If id > 0 Then _notif.MarkRead(id)
                    Response.Redirect("/App/Notifications.aspx", True)
                End If
                If Request.QueryString("all") = "1" Then
                    _notif.MarkAllRead()
                    Response.Redirect("/App/Notifications.aspx", True)
                End If
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load notifications: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            Dim items As List(Of AppNotification) = _notif.ListMine(False).OrderByDescending(Function(n) n.Id).ToList()
            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Notifications", "INBOX",
                                    "Order updates, commission progress and pop-up reminders land here."))
            sb.Append("<div class=""frow"">" & WebUi.BtnHref("/App/Notifications.aspx?all=1", "Mark all read", "ghost", "done_all") & "</div>")

            If items.Count = 0 Then
                sb.Append(WebUi.EmptyRow("You're all caught up — no notifications."))
                Out.Text = sb.ToString()
                Return
            End If

            sb.Append("<div class=""flex-col"">")
            For Each n As AppNotification In items
                Dim unreadCls As String = If(n.IsRead, "", " style=""border-color:var(--yellow)""")
                sb.Append("<div class=""card""" & unreadCls & ">")
                sb.Append("<div class=""row space-between"">")
                sb.Append("<div><span class=""pill yellow"" style=""margin-right:8px"">" & WebUi.Esc(n.NotificationType) & "</span>" &
                          "<b>" & WebUi.Esc(n.Title) & "</b>" & If(Not n.IsRead, " <span class=""pulse"" style=""display:inline-block;vertical-align:middle""></span>", "") & "</div>")
                sb.Append("<span class=""sub"" style=""font-size:12px"">" & WebUi.Esc(n.CreatedAt.ToString("MMM d, yyyy h:mm tt")) & "</span>")
                sb.Append("</div>")
                sb.Append("<p style=""margin:8px 0 4px"">" & WebUi.Esc(n.Message) & "</p>")
                sb.Append("<div class=""notif-actions"" style=""display:flex;gap:8px;align-items:center;margin-top:10px;padding-top:10px;border-top:1px solid var(--surface-mid);flex-wrap:wrap"">")
                If Not n.IsRead Then
                    sb.Append("<a class=""btn ghost sm"" href=""/App/Notifications.aspx?read=" & n.Id.ToString() & """ style=""padding:4px 10px;font-size:11.5px"">" &
                              "<span class=""ms sm"" style=""color:var(--primary);font-size:16px"">check_circle</span> Mark as read</a>")
                End If
                If n.LinkPath <> "" Then
                    Dim dest As String = ResolveLink(n.LinkPath)
                    If dest <> "" Then
                        sb.Append("<a class=""btn secondary sm"" href=""" & dest & """ style=""padding:4px 10px;font-size:11.5px"">" &
                                  "<span class=""ms sm"" style=""font-size:16px"">open_in_new</span> Open</a>")
                    End If
                End If
                sb.Append("<form method=""post"" style=""display:inline"">" & STAR_DOM.Web.Csrf.HiddenField() &
                          "<button type=""submit"" name=""delNotif"" value=""" & n.Id.ToString() & """ " &
                          "data-confirm=""Delete this notification?"" data-confirm-ok=""Delete"" data-confirm-danger=""true"" " &
                          "class=""btn ghost sm"" style=""padding:4px 10px;font-size:11.5px;color:var(--ink-soft)"">" &
                          "<span class=""ms sm"" style=""font-size:16px"">delete</span> Delete</button></form>")
                sb.Append("</div></div>")
            Next
            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function ResolveLink(path As String) As String
            Select Case path.ToLowerInvariant()
                Case "marketplace", "home" : Return "/App/Marketplace.aspx"
                Case "catalog" : Return "/App/Catalog.aspx"
                Case "orders", "my-orders" : Return "/App/Orders.aspx"
                Case "commission-hub", "commissions" : Return "/App/CommissionHub.aspx"
                Case "commission-pipeline" : Return "/App/Merchant/Pipeline.aspx"
                Case "merchant-dashboard" : Return "/App/Merchant/Dashboard.aspx"
                Case "popup-locations", "events" : Return "/App/PopupLocations.aspx"
                Case Else : Return ""
            End Select
        End Function

    End Class

End Namespace
