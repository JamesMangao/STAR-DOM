Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class NotificationService

        Private ReadOnly _repo As New NotificationRepository()

        Public Sub Notify(userId As Integer, title As String, message As String,
                          Optional ntype As String = "SYSTEM", Optional linkPath As String = "")
            If userId <= 0 Then Return
            _repo.Create(userId, title, message, ntype, linkPath)
        End Sub

        Public Sub NotifyRole(roleName As String, title As String, message As String,
                              Optional ntype As String = "SYSTEM", Optional linkPath As String = "")
            _repo.CreateForRole(roleName, title, message, ntype, linkPath)
        End Sub

        Public Function ListMine(Optional unreadOnly As Boolean = False) As List(Of AppNotification)
            If Not Session.IsAuthenticated Then Return New List(Of AppNotification)()
            Return _repo.ListByUser(Session.CurrentUser.Id, unreadOnly)
        End Function

        Public Function UnreadCount() As Integer
            If Not Session.IsAuthenticated Then Return 0
            Return _repo.CountUnread(Session.CurrentUser.Id)
        End Function

        Public Sub MarkRead(nid As Integer)
            _repo.MarkRead(nid)
        End Sub

        Public Sub MarkAllRead()
            If Session.IsAuthenticated Then _repo.MarkAllRead(Session.CurrentUser.Id)
        End Sub

        Public Sub Delete(nid As Integer)
            _repo.Delete(nid)
        End Sub

    End Class

End Namespace