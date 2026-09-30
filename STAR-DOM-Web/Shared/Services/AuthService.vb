Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class AuthService

        Private ReadOnly _users As New UserRepository()

        Public Function Login(identifier As String, password As String) As ServiceResult
            If String.IsNullOrWhiteSpace(identifier) OrElse String.IsNullOrEmpty(password) Then
                Return ServiceResult.Fail("Please enter your email/username and password.")
            End If

            Dim user As User = _users.FindByEmail(identifier)
            If user Is Nothing Then user = _users.FindByUsername(identifier)
            If user Is Nothing Then
                Return ServiceResult.Fail("No account found with that email/username.")
            End If

            If String.Equals(user.Status, "SUSPENDED", StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Fail("This account has been suspended. Contact STAR:DOM support.")
            End If

            If Not PasswordHasher.Verify(password, user.PasswordHash) Then
                Return ServiceResult.Fail("Incorrect password. Please try again.")
            End If

            Session.CurrentUser = user
            Session.CurrentRoleName = user.RoleName
            _users.UpdateLastLogin(user.Id)
            Return ServiceResult.Ok("Welcome back, " & user.FullName & "!", user)
        End Function

        Public Function Register(fullName As String, email As String, username As String, phone As String,
                                 password As String, confirm As String, roleName As String) As ServiceResult
            Dim errors As New List(Of String)()
            errors.Add(Validators.Required(fullName, "Full name"))
            errors.Add(Validators.Required(email, "Email"))
            errors.Add(Validators.Email(email))
            errors.Add(Validators.Required(username, "Username"))
            errors.Add(Validators.Phone(phone))
            errors.Add(Validators.PasswordStrength(password))
            If password <> confirm Then errors.Add("Passwords do not match.")
            errors.Add(Validators.Duplicate(_users.EmailExists(email), "Email"))
            errors.Add(Validators.Duplicate(_users.UsernameExists(username), "Username"))

            Dim clean As String() = errors.Where(Function(e) e IsNot Nothing).ToArray()
            If clean.Length > 0 Then
                Validators.Alert(clean, "Registration incomplete")
                Return ServiceResult.Fail(clean(0))
            End If

            roleName = "CUSTOMER"

            Dim user As New User() With {
                .FullName = fullName.Trim(),
                .Email = email.Trim().ToLowerInvariant(),
                .Username = username.Trim(),
                .Phone = phone.Trim(),
                .PasswordHash = PasswordHasher.Hash(password)
            }
            Dim id As Integer = _users.Create(user, roleName)
            user.Id = id
            user.RoleName = roleName
            user.Status = "ACTIVE"
            Session.CurrentUser = user
            Session.CurrentRoleName = roleName

            Dim notes As New NotificationService()
            notes.Notify(id, "Welcome to STAR:DOM!",
                         "Your account is ready. Explore the marketplace and start collecting handcrafted art.",
                         "SYSTEM", "marketplace")
            Return ServiceResult.Ok("Account created. Welcome, " & user.FullName & "!")
        End Function

        Public Function ChangePassword(userId As Integer, current As String, newPass As String, confirm As String) As ServiceResult
            Dim user As User = _users.GetById(userId)
            If user Is Nothing Then Return ServiceResult.Fail("Account not found.")
            If Not PasswordHasher.Verify(current, user.PasswordHash) Then
                Return ServiceResult.Fail("Current password is incorrect.")
            End If
            If newPass.Length < 6 Then Return ServiceResult.Fail("New password must be at least 6 characters.")
            If newPass <> confirm Then Return ServiceResult.Fail("New passwords do not match.")
            _users.UpdatePassword(userId, PasswordHasher.Hash(newPass))
            Return ServiceResult.Ok("Password updated successfully.")
        End Function

        Public Sub RefreshSession()
            If Session.IsAuthenticated Then
                Dim fresh As User = _users.GetById(Session.CurrentUser.Id)
                If fresh IsNot Nothing Then
                    Session.CurrentUser = fresh
                    Session.CurrentRoleName = fresh.RoleName
                End If
            End If
        End Sub

        Public Function MerchantId() As Integer
            If Session.IsAuthenticated Then Return Session.CurrentUser.Id
            Return 0
        End Function

    End Class

End Namespace