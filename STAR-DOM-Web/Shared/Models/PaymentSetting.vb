Namespace STAR_DOM.Models

    ''' <summary>
    ''' Admin-managed payment channel settings for one e-wallet (GCash or Maya).
    ''' Rendered by the order-detail QR popup; edited from App/Admin/PaymentSettings.aspx.
    ''' </summary>
    Public Class PaymentSetting

        Public Property Id As Integer
        ''' <summary>'GCASH' or 'MAYA'.</summary>
        Public Property Channel As String

        Public Property AccountName As String
        Public Property AccountNumber As String

        ''' <summary>Root-relative path of the uploaded QR image ("" when none is set).</summary>
        Public Property QrImageFile As String

        ''' <summary>Optional admin caption shown under the QR code.</summary>
        Public Property QrCaption As String

        ''' <summary>What the customer-facing popup shows: BOTH / QR_ONLY / NUMBER_NAME / NAME_ONLY.</summary>
        Public Property QrDisplayMode As String

        ''' <summary>When FALSE the channel is hidden from checkout and the QR popup.</summary>
        Public Property IsEnabled As Boolean

        Public Property UpdatedBy As String
        Public Property UpdatedAt As Date

        ''' <summary>GCASH -> GCash, MAYA -> Maya, anything else passes through.</summary>
        Public ReadOnly Property DisplayChannel As String
            Get
                Select Case If(Channel, "").ToUpperInvariant()
                    Case "GCASH" : Return "GCash"
                    Case "MAYA" : Return "Maya"
                    Case Else : Return If(Channel, "")
                End Select
            End Get
        End Property

        Public Shared Function NormalizeMode(raw As String) As String
            Dim m As String = If(raw, "").Trim().ToUpperInvariant()
            Select Case m
                Case "QR_ONLY", "NUMBER_NAME", "NAME_ONLY"
                    Return m
                Case Else
                    Return "BOTH"
            End Select
        End Function

    End Class

End Namespace
