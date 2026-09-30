Imports System.Web.UI

Namespace STAR_DOM.Web

    Public Class ErrorPage
        Inherits Page

        Public ReadOnly Property Detail As String
            Get
                Dim m As String = Convert.ToString(Session("LastError"))
                Session("LastError") = Nothing
                If String.IsNullOrWhiteSpace(m) Then m = "An unexpected error occurred while processing your request."
                Return m
            End Get
        End Property

    End Class

End Namespace
