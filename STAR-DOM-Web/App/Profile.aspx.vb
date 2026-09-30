Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class ProfilePage
        Inherits Page

        Protected Out As Literal
        Private ReadOnly _auth As New AuthService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Guard.RequireLogin()
            Try
                If Guard.IsPost() Then
                    Dim current As String = Request.Form("current")
                    Dim np As String = Request.Form("np")
                    Dim confirm As String = Request.Form("confirm")
                    Dim r As ServiceResult = _auth.ChangePassword(STAR_DOM.Helpers.Session.CurrentUser.Id, current, np, confirm)
                    Session("flash_msg") = r.Message
                    Session("flash_ok") = r.Success
                    Response.Redirect("/App/Profile.aspx", True)
                End If
                RenderProfile()
            Catch ex As Exception
                Out.Text = WebUi.AlertBox("Could not load profile: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderProfile()
            Dim u As Models.User = STAR_DOM.Helpers.Session.CurrentUser
            Dim sb As New StringBuilder()
            Dim flash As String = Convert.ToString(Session("flash_msg"))
            Dim ok As Boolean = Session("flash_ok") IsNot Nothing AndAlso CBool(Session("flash_ok"))
            Session("flash_msg") = Nothing
            Session("flash_ok") = Nothing
            If flash <> "" Then sb.Append(WebUi.AlertBox(flash, If(ok, "ok", "err")))

            sb.Append(WebUi.Section("My Profile & Settings", "ACCOUNT",
                                    "Manage your account details and sign-in security."))

            sb.Append("<div class=""row"" style=""align-items:flex-start;gap:24px"">")
            sb.Append("<div class=""card"" style=""flex:1;min-width:300px"">")
            sb.Append("<div class=""row"" style=""margin-bottom:12px"">")
            sb.Append("<span class=""avatar"" style=""width:56px;height:56px;font-size:20px"">" & WebUi.Esc(Initials(u.FullName)) & "</span>")
            sb.Append("<div><h3 style=""margin:0"">" & WebUi.Esc(u.FullName) & "</h3><span class=""pill yellow"">" &
                      WebUi.Esc(u.RoleName) & "</span> " & WebUi.Badge(If(u.Status = "ACTIVE", "ACTIVE", u.Status)) & "</div>")
            sb.Append("</div>")
            sb.Append("<div class=""kv"">")
            sb.Append("<dt>Username</dt><dd>" & WebUi.Esc(u.Username) & "</dd>")
            sb.Append("<dt>Email</dt><dd>" & WebUi.Esc(u.Email) & "</dd>")
            sb.Append("<dt>Phone</dt><dd>" & WebUi.Esc(u.Phone) & "</dd>")
            sb.Append("<dt>Member since</dt><dd>" & WebUi.Esc(u.CreatedAt.ToString("MMMM d, yyyy")) & "</dd>")
            If u.LastLoginAt.HasValue Then sb.Append("<dt>Last login</dt><dd>" & WebUi.Esc(u.LastLoginAt.Value.ToString("MMM d, yyyy h:mm tt")) & "</dd>")
            sb.Append("</div>")
            sb.Append("</div>")

            sb.Append("<form method=""post"" action=""/App/Profile.aspx"" style=""flex:1;min-width:300px"">")
            sb.Append("<div class=""card""><h3 style=""margin-bottom:10px"">Change password</h3>")
            sb.Append("<div class=""field""><label for=""c"">Current password</label><input id=""c"" name=""current"" type=""password"" required></div>")
            sb.Append("<div class=""field""><label for=""n"">New password (min 6 chars)</label><input id=""n"" name=""np"" type=""password"" required></div>")
            sb.Append("<div class=""field""><label for=""n2"">Confirm new password</label><input id=""n2"" name=""confirm"" type=""password"" required></div>")
            sb.Append("<button class=""btn primary"" type=""submit""><span class=""ic ms"">lock_reset</span><span>Update Password</span></button>")
            sb.Append("</div>")
            sb.Append("</form>")
            sb.Append("</div>")
            Out.Text = sb.ToString()
        End Sub

        Private Function Initials(name As String) As String
            Dim parts As String() = name.Trim().Split(" "c)
            Dim s As String = ""
            For i As Integer = 0 To Math.Min(parts.Length - 1, 1)
                If parts(i).Length > 0 Then s &= Char.ToUpperInvariant(parts(i)(0))
            Next
            If s = "" Then s = "?"
            Return s
        End Function

    End Class

End Namespace
