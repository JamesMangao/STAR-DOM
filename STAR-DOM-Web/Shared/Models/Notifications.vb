Namespace STAR_DOM.Models

    Public Class AppNotification
        Public Property Id As Integer
        Public Property UserId As Integer
        Public Property Title As String
        Public Property Message As String
        Public Property NotificationType As String   ' ORDER / COMMISSION / EVENT / SYSTEM
        Public Property LinkPath As String
        Public Property IsRead As Boolean
        Public Property CreatedAt As Date
    End Class

    ''' <summary>Service result: success flag + user-facing message + optional payload.</summary>
    Public Class ServiceResult
        Public Property Success As Boolean
        Public Property Message As String
        Public Property Payload As Object

        Public Shared Function Ok(Optional message As String = "", Optional payload As Object = Nothing) As ServiceResult
            Return New ServiceResult With {.Success = True, .Message = message, .Payload = payload}
        End Function

        Public Shared Function Fail(message As String) As ServiceResult
            Return New ServiceResult With {.Success = False, .Message = message}
        End Function
    End Class

End Namespace