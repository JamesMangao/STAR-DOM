Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Web

    Public Class AdminUsersPage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _users As New UserRepository()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireAdmin()
            Try
                If Request.QueryString("suspend") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("suspend"), id)
                    If id > 0 AndAlso id <> STAR_DOM.Helpers.Session.CurrentUser.Id Then _users.SetStatus(id, "SUSPENDED")
                    Response.Redirect("/App/Admin/Users.aspx", True)
                End If
                If Request.QueryString("activate") <> "" Then
                    Dim id As Integer = 0
                    Integer.TryParse(Request.QueryString("activate"), id)
                    If id > 0 Then _users.SetStatus(id, "ACTIVE")
                    Response.Redirect("/App/Admin/Users.aspx", True)
                End If
                If Guard.IsPost() AndAlso Request.Form("roleUserId") IsNot Nothing Then
                    Dim uid As Integer = 0
                    Integer.TryParse(Request.Form("roleUserId"), uid)
                    Dim roleName As String = Convert.ToString(Request.Form("newRole"))
                    If uid > 0 AndAlso roleName <> "" AndAlso uid <> STAR_DOM.Helpers.Session.CurrentUser.Id Then
                        Dim role As Role = _users.GetRoles().FirstOrDefault(Function(r) String.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase))
                        If role IsNot Nothing Then _users.SetRole(uid, role.Id)
                    End If
                    Response.Redirect("/App/Admin/Users.aspx", True)
                End If
                Render()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load users: " & ex.Message)
            End Try
        End Sub

        Private Sub Render()
            ' Role counts come straight from SQL; don't load the whole table just to count.
            Dim nCust As Integer = _users.CountByRole("CUSTOMER")
            Dim nMerc As Integer = _users.CountByRole("MERCHANT")
            Dim nAdmin As Integer = _users.CountByRole("ADMIN")
            Dim all As List(Of User) = _users.ListUsers("").OrderBy(Function(u) u.Id).ToList()

            Dim sb As New StringBuilder()
            sb.Append(WebUi.Section("Admin Console", "SYSTEM ADMIN / USERS & ROLES",
                                    "Manage accounts, assign roles, and control marketplace access."))

            sb.Append("<div class=""grid kpis"">")
            sb.Append(Kpi("TOTAL USERS", all.Count.ToString()))
            sb.Append(Kpi("CUSTOMERS", nCust.ToString()))
            sb.Append(Kpi("MERCHANTS", nMerc.ToString()))
            sb.Append(Kpi("ADMINS", nAdmin.ToString()))
            sb.Append("</div>")

            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"ID", "NAME", "EMAIL", "USERNAME", "ROLE", "STATUS", "CHANGE ROLE", "ACTIONS"}
                sb.Append("<th>" & h & "</th>")
            Next
            sb.Append("</tr></thead><tbody>")
            For Each u As User In all
                sb.Append("<tr>")
                sb.Append("<td>" & u.Id.ToString() & "</td>")
                sb.Append("<td><b>" & WebUi.Esc(u.FullName) & "</b></td>")
                sb.Append("<td>" & WebUi.Esc(u.Email) & "</td>")
                sb.Append("<td>" & WebUi.Esc(u.Username) & "</td>")
                sb.Append("<td><span class=""pill yellow"">" & WebUi.Esc(u.RoleName) & "</span></td>")
                sb.Append("<td>" & If(u.Status = "ACTIVE", WebUi.Badge("ACTIVE"), WebUi.Badge("SUSPENDED")) & "</td>")
                sb.Append("<td><form method=""post"" style=""display:flex;gap:6px"">" &
                          "<input type=""hidden"" name=""roleUserId"" value=""" & u.Id.ToString() & """>" &
                          "<select name=""newRole"" style=""padding:5px;border:1px solid var(--line);border-radius:7px"">")
                For Each r As Role In _users.GetRoles()
                    Dim sel As String = If(String.Equals(r.Name, u.RoleName, StringComparison.OrdinalIgnoreCase), " selected", "")
                    sb.Append("<option value=""" & WebUi.Esc(r.Name) & """" & sel & ">" & WebUi.Esc(r.Name) & "</option>")
                Next
                sb.Append("</select><button class=""btn ghost sm"" type=""submit""><span class=""ms sm"">manage_accounts</span> Set</button></form></td>")
                sb.Append("<td class=""rowact"">")
                If u.Id <> STAR_DOM.Helpers.Session.CurrentUser.Id Then
                    If u.Status = "ACTIVE" Then
                        sb.Append("<a href=""/App/Admin/Users.aspx?suspend=" & u.Id.ToString() & """ data-confirm=""Suspend " &
                                  WebUi.Esc(u.FullName) & "?"" data-confirm-danger""><span class=""ms sm"">block</span> Suspend</a>")
                    Else
                        sb.Append("<a href=""/App/Admin/Users.aspx?activate=" & u.Id.ToString() & """><span class=""ms sm"">check_circle</span> Activate</a>")
                    End If
                Else
                    sb.Append("<span class=""sub"">you</span>")
                End If
                sb.Append("</td></tr>")
            Next
            sb.Append("</tbody></table></div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function Kpi(label As String, value As String) As String
            Return "<div class=""kpi""><div class=""k-label"">" & WebUi.Esc(label) & "</div><div class=""k-value"">" & WebUi.Esc(value) & "</div></div>"
        End Function

    End Class

End Namespace
