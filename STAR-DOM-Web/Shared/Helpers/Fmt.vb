Imports System.Globalization

Namespace STAR_DOM.Helpers

    Public Module Fmt

        Public ReadOnly Peso As String = "₱"

        Public Function PHP(value As Object) As String
            Dim d As Decimal
            If value Is Nothing OrElse Not Decimal.TryParse(value.ToString(), d) Then d = 0D
            Return Peso & d.ToString("N2", CultureInfo.InvariantCulture)
        End Function

        Public Function Num(value As Object) As String
            Dim d As Decimal
            If value Is Nothing OrElse Not Decimal.TryParse(value.ToString(), d) Then d = 0D
            Return d.ToString("N0", CultureInfo.InvariantCulture)
        End Function

        Public Function Pct(value As Object, Optional digits As Integer = 0) As String
            Dim d As Decimal
            If value Is Nothing OrElse Not Decimal.TryParse(value.ToString(), d) Then d = 0D
            Return (d * 100D).ToString("F" & digits, CultureInfo.InvariantCulture) & "%"
        End Function

        Public Function DateF(value As Object) As String
            If value Is Nothing OrElse value Is DBNull.Value Then Return "—"
            Dim d As Date
            If Date.TryParse(value.ToString(), d) Then Return d.ToString("MMM d, yyyy")
            Return "—"
        End Function

        Public Function DateTimeF(value As Object) As String
            If value Is Nothing OrElse value Is DBNull.Value Then Return "—"
            Dim d As Date
            If Date.TryParse(value.ToString(), d) Then Return d.ToString("MMM d, yyyy h:mm tt")
            Return "—"
        End Function

        Public Function TimeF(value As Object) As String
            If value Is Nothing OrElse value Is DBNull.Value Then Return "—"
            Dim d As Date
            If Date.TryParse(value.ToString(), d) Then Return d.ToString("h:mm tt")
            Return "—"
        End Function

        ''' <summary>"Sept 4–7, 2026" style event window.</summary>
        Public Function EventWindow(startD As Date, endD As Date) As String
            If startD.Year = endD.Year Then
                Return startD.ToString("MMM d") & "–" & endD.ToString("d, yyyy")
            End If
            Return startD.ToString("MMM d, yyyy") & " – " & endD.ToString("MMM d, yyyy")
        End Function

        Public Function DaysToGo(target As Date) As String
            Dim days As Integer = CInt(Math.Ceiling((target.Date - Clock.Now.Date).TotalDays))
            If days <= 0 Then Return "TODAY"
            Return days.ToString() & " DAY" & If(days = 1, "", "S") & " TO GO"
        End Function

        Public Function Truncate(s As String, max As Integer) As String
            If String.IsNullOrEmpty(s) Then Return ""
            If s.Length <= max Then Return s
            Return s.Substring(0, max - 3) & "..."
        End Function

        Public Function Initials(name As String) As String
            If String.IsNullOrWhiteSpace(name) Then Return "★"
            Dim parts As String() = name.Trim().Split(" "c)
            If parts.Length = 1 Then Return parts(0).Substring(0, 1).ToUpper()
            Return (parts(0).Substring(0, 1) & parts(parts.Length - 1).Substring(0, 1)).ToUpper()
        End Function

    End Module

End Namespace