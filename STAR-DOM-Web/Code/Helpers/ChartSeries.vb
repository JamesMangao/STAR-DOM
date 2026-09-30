Imports System.Drawing

Namespace STAR_DOM.Helpers

    ''' <summary>
    ''' Chart data series (web copy of the class the shared report services return).
    ''' Colors are carried so the UI can render CSS bars with the same palette.
    ''' </summary>
    Public Class ChartSeries
        Public Property Label As String
        Public Property Value As Decimal
        Public Property Color As Color

        Public Sub New()
        End Sub

        Public Sub New(label As String, value As Decimal, color As Color)
            Me.Label = label
            Me.Value = value
            Me.Color = color
        End Sub
    End Class

End Namespace
