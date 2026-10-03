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
        Private ReadOnly _commissions As New CommissionRepository()

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
                If Guard.IsPost() AndAlso Request.Form("slotUserId") IsNot Nothing Then
                    Dim uid As Integer = 0
                    Integer.TryParse(Request.Form("slotUserId"), uid)
                    Dim cap As Integer = 0
                    Integer.TryParse(Request.Form("slotCapacity"), cap)
                    ' 0 is a real value: it takes an artist out of the commission hub
                    ' entirely, which is how you pause intake without touching them.
                    ' Negative is not a value, it is a typo - refuse it.
                    If uid > 0 AndAlso cap >= 0 AndAlso cap <= 999 Then
                        _users.SetCommissionSlotCapacity(uid, cap)
                        Session("flash_msg") = "Commission slots updated."
                        Session("flash_ok") = True
                    Else
                        Session("flash_msg") = "Enter a slot count between 0 and 999."
                        Session("flash_ok") = False
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
            ' STAR:DOM has two roles: the single owner (ADMIN, who is the merchant)
            ' and customers. The MERCHANT role is a legacy alias still honoured by
            ' the guards, so store-operator accounts are counted as staff.
            Dim nStaff As Integer = _users.CountByRole("ADMIN") + _users.CountByRole("MERCHANT")
            Dim all As List(Of User) = _users.ListUsers("").OrderBy(Function(u) u.Id).ToList()
            ' How many of each artist's slots are already spoken for, so the console
            ' shows "8 of 20 taken" rather than a bare capacity the admin cannot
            ' reconcile against the pipeline.
            Dim used As Dictionary(Of Integer, Integer) = _commissions.OpenSlotCounts()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim flashOk As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing

            Dim sb As New StringBuilder()
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(flashOk, "ok", "err")))

            sb.Append(WebUi.Section("Admin Console", "SYSTEM ADMIN / USERS & ROLES",
                                    "Manage accounts, assign roles, and control marketplace access."))

            sb.Append("<div class=""grid kpis"">")
            sb.Append(Kpi("TOTAL USERS", all.Count.ToString()))
            sb.Append(Kpi("CUSTOMERS", nCust.ToString()))
            sb.Append(Kpi("STORE OWNERS / STAFF", nStaff.ToString()))
            sb.Append("</div>")

            sb.Append("<div class=""tblwrap""><table class=""tbl""><thead><tr>")
            For Each h As String In {"ID", "NAME", "EMAIL", "USERNAME", "ROLE", "STATUS", "COMMISSION SLOTS", "CHANGE ROLE", "ACTIONS"}
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
                ' How many commission slots this artist takes, and how many are left.
                ' Only staff accounts appear in the commission hub at all
                ' (UserRepository.ListMerchants filters on capacity > 0), so 0 slots
                ' is simply "not taking commissions right now".
                Dim taken As Integer = 0
                If used.ContainsKey(u.Id) Then taken = used(u.Id)
                Dim left As Integer = Math.Max(0, u.CommissionSlotCapacity - taken)
                Dim over As Boolean = taken > u.CommissionSlotCapacity
                sb.Append("<td><form method=""post"" style=""display:flex;gap:6px;align-items:center"">" &
                          STAR_DOM.Web.Csrf.HiddenField() &
                          "<input type=""hidden"" name=""slotUserId"" value=""" & u.Id.ToString() & """>" &
                          "<input name=""slotCapacity"" type=""number"" min=""0"" max=""999"" value=""" & u.CommissionSlotCapacity.ToString() & """ " &
                          "aria-label=""Commission slot capacity for " & WebUi.Attr(u.FullName) & """ " &
                          "style=""width:72px;padding:5px;border:1px solid var(--line);border-radius:7px"">" &
                          "<button class=""btn ghost sm"" type=""submit""><span class=""ms sm"">save</span>Set</button></form>")
                sb.Append("<span class=""sub"" style=""font-size:11px"">" & left.ToString() & " open · " & taken.ToString() & " taken" &
                          If(over, " <span style=""color:#b91c1c;font-weight:700"">over capacity</span>", "") & "</span></td>")
                ' The role form is nested inside the shell form, which the browser closes at this tag
                ' — so the shell's token is not submitted with it. It carries its own.
                sb.Append("<td><form method=""post"" style=""display:flex;gap:6px"">" &
                          STAR_DOM.Web.Csrf.HiddenField() &
                          "<input type=""hidden"" name=""roleUserId"" value=""" & u.Id.ToString() & """>" &
                          "<select name=""newRole"" style=""padding:5px;border:1px solid var(--line);border-radius:7px"">")
                ' Two-role model: offer only CUSTOMER and ADMIN. (The MERCHANT role
                ' still exists for legacy accounts and is honoured by the guards.)
                For Each r As Role In _users.GetRoles().Where(Function(x) x.Name = "CUSTOMER" OrElse x.Name = "ADMIN")
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
