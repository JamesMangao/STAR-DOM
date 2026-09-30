Imports System.Text.RegularExpressions

Namespace STAR_DOM.Helpers

    ''' <summary>Field validation helpers. Each returns Nothing when valid, else an error message.
    ''' Web variant: no MessageBox — Alert/Info are no-ops (the page shows service messages).</summary>
    Public Module Validators

        Private ReadOnly _emailRegex As New Regex("^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)

        Public Function Required(value As String, label As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return label & " is required."
            Return Nothing
        End Function

        Public Function Email(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return Nothing
            If Not _emailRegex.IsMatch(value.Trim()) Then Return "Please enter a valid email address."
            Return Nothing
        End Function

        Public Function Phone(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return Nothing
            Dim digits As String = New String(value.Where(Function(c) Char.IsDigit(c)).ToArray())
            If digits.Length < 7 Then Return "Please enter a valid phone number."
            Return Nothing
        End Function

        Public Function Number(value As String, label As String, Optional min As Decimal = Decimal.MinValue, Optional max As Decimal = Decimal.MaxValue) As String
            If String.IsNullOrWhiteSpace(value) Then Return Nothing
            Dim d As Decimal
            If Not Decimal.TryParse(value, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, d) Then
                Return label & " must be a number."
            End If
            If d < min OrElse d > max Then
                Return label & " must be between " & min.ToString() & " and " & max.ToString() & "."
            End If
            Return Nothing
        End Function

        Public Function IntegerValue(value As String, label As String, Optional min As Integer = Integer.MinValue, Optional max As Integer = Integer.MaxValue) As String
            If String.IsNullOrWhiteSpace(value) Then Return Nothing
            Dim i As Integer
            If Not Integer.TryParse(value, i) Then
                Return label & " must be a whole number."
            End If
            If i < min OrElse i > max Then
                Return label & " must be between " & min.ToString() & " and " & max.ToString() & "."
            End If
            Return Nothing
        End Function

        Public Function DateRange(startD As Date?, endD As Date?) As String
            If Not startD.HasValue Then Return "Start date is required."
            If Not endD.HasValue Then Return "End date is required."
            If endD.Value.Date < startD.Value.Date Then Return "End date cannot be before the start date."
            Return Nothing
        End Function

        Public Function Quantity(value As Integer) As String
            If value < 1 Then Return "Quantity must be at least 1."
            If value > 999 Then Return "Quantity is too large (max 999)."
            Return Nothing
        End Function

        Public Function PasswordStrength(value As String) As String
            If String.IsNullOrEmpty(value) Then Return "Password is required."
            If value.Length < 6 Then Return "Password must be at least 6 characters."
            Return Nothing
        End Function

        Public Function Duplicate(exists As Boolean, label As String) As String
            If exists Then Return label & " already exists. Please use a different value."
            Return Nothing
        End Function

        Public Sub Alert(messages As IEnumerable(Of String), Optional title As String = "Please check the form")
            ' Web: no client dialog here — the caller surfaces the first error via ServiceResult.
        End Sub

        Public Sub Info(message As String, Optional title As String = "STAR:DOM")
        End Sub

        Public Sub ShowError(message As String, Optional title As String = "STAR:DOM – Error")
        End Sub

        Public Function Confirm(message As String, Optional title As String = "STAR:DOM") As Boolean
            Return False
        End Function

    End Module

End Namespace
