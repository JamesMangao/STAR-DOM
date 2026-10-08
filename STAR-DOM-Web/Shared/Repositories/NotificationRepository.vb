Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class NotificationRepository

        Public Sub Create(userId As Integer, title As String, message As String,
                          Optional ntype As String = "SYSTEM", Optional linkPath As String = "")
            Db.Exec("INSERT INTO Notifications (UserId, Title, Message, NotificationType, LinkPath, IsRead, CreatedAt) " &
                    "VALUES (@u, @t, @m, @n, @l, FALSE, NOW())",
                    Db.P("@u", userId), Db.P("@t", title), Db.P("@m", message),
                    Db.P("@n", ntype), Db.P("@l", linkPath))
        End Sub

        ''' <summary>Notify every user with the given role (e.g., every MERCHANT).</summary>
        Public Sub CreateForRole(roleName As String, title As String, message As String,
                                 Optional ntype As String = "SYSTEM", Optional linkPath As String = "")
            Db.Exec("INSERT INTO Notifications (UserId, Title, Message, NotificationType, LinkPath, IsRead, CreatedAt) " &
                    "SELECT u.Id, @t, @m, @n, @l, FALSE, NOW() FROM Users u JOIN Roles r ON r.Id = u.RoleId WHERE r.Name = @r",
                    Db.P("@t", title), Db.P("@m", message), Db.P("@n", ntype),
                    Db.P("@l", linkPath), Db.P("@r", roleName))
        End Sub

        Public Function ListByUser(userId As Integer, Optional unreadOnly As Boolean = False) As List(Of AppNotification)
            Dim sql As String = "SELECT * FROM Notifications WHERE UserId = @u "
            If unreadOnly Then sql &= "AND IsRead = FALSE "
            sql &= "ORDER BY CreatedAt DESC LIMIT 100"
            Return Db.Rows(sql, Db.P("@u", userId)).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function CountUnread(userId As Integer) As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Notifications WHERE UserId = @u AND IsRead = FALSE", Db.P("@u", userId))
        End Function

        Public Sub MarkRead(notificationId As Integer, Optional userId As Integer = 0)
            Dim sql As String = "UPDATE Notifications SET IsRead = TRUE WHERE Id = @id"
            If userId > 0 Then sql &= " AND UserId = @u"
            If userId > 0 Then
                Db.Exec(sql, Db.P("@id", notificationId), Db.P("@u", userId))
            Else
                Db.Exec(sql, Db.P("@id", notificationId))
            End If
        End Sub

        Public Sub MarkAllRead(userId As Integer)
            Db.Exec("UPDATE Notifications SET IsRead = TRUE WHERE UserId = @u AND IsRead = FALSE", Db.P("@u", userId))
        End Sub

        Public Sub Delete(notificationId As Integer, Optional userId As Integer = 0)
            Dim sql As String = "DELETE FROM Notifications WHERE Id = @id"
            If userId > 0 Then sql &= " AND UserId = @u"
            If userId > 0 Then
                Db.Exec(sql, Db.P("@id", notificationId), Db.P("@u", userId))
            Else
                Db.Exec(sql, Db.P("@id", notificationId))
            End If
        End Sub

        Private Function Map(r As DataRow) As AppNotification
            Return New AppNotification With {
                .Id = RowReader.AsInt(r, "Id"), .UserId = RowReader.AsInt(r, "UserId"),
                .Title = RowReader.AsStr(r, "Title"), .Message = RowReader.AsStr(r, "Message"),
                .NotificationType = RowReader.AsStr(r, "NotificationType"), .LinkPath = RowReader.AsStr(r, "LinkPath"),
                .IsRead = RowReader.AsBool(r, "IsRead"), .CreatedAt = RowReader.AsDate(r, "CreatedAt")
            }
        End Function

    End Class

End Namespace