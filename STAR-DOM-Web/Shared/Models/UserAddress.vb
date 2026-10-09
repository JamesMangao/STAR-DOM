Namespace STAR_DOM.Models

    Public Class UserAddress
        Public Property Id As Integer
        Public Property UserId As Integer
        Public Property Label As String
        ''' <summary>House number and street.</summary>
        Public Property Address As String
        Public Property Barangay As String
        ''' <summary>City / municipality.</summary>
        Public Property City As String
        ''' <summary>Province (stored in the legacy Region column).</summary>
        Public Property Region As String
        Public Property PostalCode As String
        Public Property Landmark As String
        Public Property Phone As String
        Public Property IsDefault As Boolean
        Public Property CreatedAt As Date

        ''' <summary>
        ''' The single delivery line the Orders/Commissions tables store, built from
        ''' the detailed parts so nothing the customer entered is dropped.
        ''' </summary>
        Public ReadOnly Property Compose As String
            Get
                Dim sb As New System.Text.StringBuilder()
                sb.Append(Address)
                If Not String.IsNullOrWhiteSpace(Barangay) Then sb.Append(", Brgy. " & Barangay.Trim())
                If Not String.IsNullOrWhiteSpace(City) Then sb.Append(", " & City.Trim())
                If Not String.IsNullOrWhiteSpace(Region) Then sb.Append(", " & Region.Trim())
                If Not String.IsNullOrWhiteSpace(PostalCode) Then sb.Append(" " & PostalCode.Trim())
                If Not String.IsNullOrWhiteSpace(Landmark) Then sb.Append(" · landmark: " & Landmark.Trim())
                Return sb.ToString()
            End Get
        End Property
    End Class

End Namespace
