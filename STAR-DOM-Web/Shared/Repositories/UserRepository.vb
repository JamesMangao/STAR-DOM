Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class UserRepository

        Public Function FindByEmail(email As String) As User
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT u.*, r.Name AS RoleName FROM Users u " &
                "JOIN Roles r ON r.Id = u.RoleId WHERE LOWER(u.Email) = LOWER(@e) LIMIT 1",
                Db.P("@e", email))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function FindByUsername(username As String) As User
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT u.*, r.Name AS RoleName FROM Users u " &
                "JOIN Roles r ON r.Id = u.RoleId WHERE LOWER(u.Username) = LOWER(@u) LIMIT 1",
                Db.P("@u", username))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function GetById(id As Integer) As User
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT u.*, r.Name AS RoleName FROM Users u " &
                "JOIN Roles r ON r.Id = u.RoleId WHERE u.Id = @id LIMIT 1",
                Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function EmailExists(email As String) As Boolean
            Return Db.ScalarInt("SELECT COUNT(*) FROM Users WHERE LOWER(Email) = LOWER(@e)", Db.P("@e", email)) > 0
        End Function

        Public Function UsernameExists(username As String) As Boolean
            Return Db.ScalarInt("SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER(@u)", Db.P("@u", username)) > 0
        End Function

        Public Function Create(user As User, roleName As String) As Integer
            Dim roleId As Integer = GetRoleId(roleName)
            Return Db.ExecIdentity(
                "INSERT INTO Users (Email, Username, FullName, Phone, PasswordHash, RoleId, AvatarFile, Status, EmailVerified, CreatedAt, UpdatedAt) " &
                "VALUES (@e, @u, @n, @p, @h, @r, @a, 'ACTIVE', TRUE, NOW(), NOW())",
                Db.P("@e", user.Email), Db.P("@u", user.Username), Db.P("@n", user.FullName),
                Db.P("@p", user.Phone), Db.P("@h", user.PasswordHash), Db.P("@r", roleId),
                Db.P("@a", If(String.IsNullOrEmpty(user.AvatarFile), "", user.AvatarFile)))
        End Function

        Public Sub UpdateProfile(user As User)
            Db.Exec(
                "UPDATE Users SET FullName = @n, Phone = @p, AvatarFile = @a, UpdatedAt = NOW() WHERE Id = @id",
                Db.P("@n", user.FullName), Db.P("@p", user.Phone), Db.P("@a", user.AvatarFile), Db.P("@id", user.Id))
        End Sub

        Public Sub UpdatePassword(userId As Integer, hash As String)
            Db.Exec("UPDATE Users SET PasswordHash = @h, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@h", hash), Db.P("@id", userId))
        End Sub

        Public Sub UpdateLastLogin(userId As Integer)
            Db.Exec("UPDATE Users SET LastLoginAt = NOW() WHERE Id = @id", Db.P("@id", userId))
        End Sub

        Public Sub UpdateCommissionProfile(userId As Integer, capacity As Integer, startingPrice As Decimal,
                                           turnaround As String, formats As String, sampleImage As String, tagline As String)
            Db.Exec(
                "UPDATE Users SET CommissionSlotCapacity = @c, CommissionStartingPrice = @sp, CommissionTurnaround = @t, " &
                "CommissionFormats = @f, CommissionSampleImage = @s, CommissionTagline = @tg, UpdatedAt = NOW() WHERE Id = @id",
                Db.P("@c", capacity), Db.P("@sp", startingPrice), Db.P("@t", turnaround),
                Db.P("@f", formats), Db.P("@s", sampleImage), Db.P("@tg", tagline), Db.P("@id", userId))
        End Sub

        ' ----- Role management -------------------------------------------------

        Public Function GetRoles() As List(Of Role)
            Return Db.Rows("SELECT * FROM Roles ORDER BY Id").Select(Function(r) New Role With {
                .Id = RowReader.AsInt(r, "Id"),
                .Name = RowReader.AsStr(r, "Name"),
                .Description = RowReader.AsStr(r, "Description")
            }).ToList()
        End Function

        Public Function GetRoleId(roleName As String) As Integer
            Return Db.ScalarInt("SELECT Id FROM Roles WHERE Name = @n", Db.P("@n", roleName))
        End Function

        Public Function GetRoleName(roleId As Integer) As String
            Return Db.ScalarStr("SELECT Name FROM Roles WHERE Id = @id", Db.P("@id", roleId))
        End Function

        ' ----- Admin user management -------------------------------------------

        Public Function ListUsers(search As String) As List(Of User)
            Dim sql As String =
                "SELECT u.*, r.Name AS RoleName FROM Users u JOIN Roles r ON r.Id = u.RoleId " &
                "WHERE (@s = '' OR u.FullName LIKE @like OR u.Email LIKE @like) ORDER BY u.CreatedAt DESC"
            Return Db.Rows(sql, Db.P("@s", search), Db.P("@like", "%" & search & "%")).Select(Function(r) Map(r)).ToList()
        End Function

        ''' <summary>Commission creators/artists (Admin or Merchant).</summary>
        Public Function ListMerchants() As List(Of User)
            Return Db.Rows(
                "SELECT u.*, r.Name AS RoleName FROM Users u JOIN Roles r ON r.Id = u.RoleId " &
                "WHERE (r.Name = 'ADMIN' OR r.Name = 'MERCHANT') AND u.Status = 'ACTIVE' " &
                "AND u.CommissionSlotCapacity > 0 ORDER BY u.FullName").Select(Function(r) Map(r)).ToList()
        End Function

        Public Sub SetStatus(userId As Integer, status As String)
            Db.Exec("UPDATE Users SET Status = @s, UpdatedAt = NOW() WHERE Id = @id", Db.P("@s", status), Db.P("@id", userId))
        End Sub

        Public Sub SetRole(userId As Integer, roleId As Integer)
            Db.Exec("UPDATE Users SET RoleId = @r, UpdatedAt = NOW() WHERE Id = @id", Db.P("@r", roleId), Db.P("@id", userId))
        End Sub

        Public Function CountByRole(roleName As String) As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Users u JOIN Roles r ON r.Id = u.RoleId WHERE r.Name = @n",
                                Db.P("@n", roleName))
        End Function

        Private Function Map(r As DataRow) As User
            Return New User With {
                .Id = RowReader.AsInt(r, "Id"),
                .Email = RowReader.AsStr(r, "Email"),
                .Username = RowReader.AsStr(r, "Username"),
                .FullName = RowReader.AsStr(r, "FullName"),
                .Phone = RowReader.AsStr(r, "Phone"),
                .PasswordHash = RowReader.AsStr(r, "PasswordHash"),
                .RoleId = RowReader.AsInt(r, "RoleId"),
                .RoleName = RowReader.AsStr(r, "RoleName"),
                .AvatarFile = RowReader.AsStr(r, "AvatarFile"),
                .Status = RowReader.AsStr(r, "Status"),
                .EmailVerified = RowReader.AsBool(r, "EmailVerified"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt"),
                .UpdatedAt = RowReader.AsDate(r, "UpdatedAt"),
                .LastLoginAt = RowReader.AsNullableDate(r, "LastLoginAt"),
                .CommissionSlotCapacity = RowReader.AsInt(r, "CommissionSlotCapacity", 5),
                .CommissionStartingPrice = RowReader.AsDec(r, "CommissionStartingPrice"),
                .CommissionTurnaround = RowReader.AsStr(r, "CommissionTurnaround", "3-5 business days"),
                .CommissionFormats = RowReader.AsStr(r, "CommissionFormats", "High-Res PNG + Print"),
                .CommissionSampleImage = RowReader.AsStr(r, "CommissionSampleImage"),
                .CommissionTagline = RowReader.AsStr(r, "CommissionTagline")
            }
        End Function

    End Class

End Namespace