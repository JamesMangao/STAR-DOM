Namespace STAR_DOM.Reports

    ''' <summary>Key/value row used by report grids and chart series.</summary>
    Public Class ReportRow
        Public Property Label As String
        Public Property Value As Decimal
        Public Property Extra As String = ""

        Public Sub New()
        End Sub

        Public Sub New(label As String, value As Decimal, Optional extra As String = "")
            Me.Label = label
            Me.Value = value
            Me.Extra = extra
        End Sub
    End Class

    ''' <summary>Aggregated KPI block rendered on the reports screen.</summary>
    Public Class KpiBlock
        Public Property Label As String
        Public Property Value As String
        Public Property Accent As String = "Primary"
    End Class

End Namespace