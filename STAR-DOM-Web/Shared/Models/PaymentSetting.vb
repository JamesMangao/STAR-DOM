Namespace STAR_DOM.Models

    ''' <summary>
    ''' Admin-managed payment channel settings for one e-wallet (GCash or GOtyme).
    ''' Rendered by the order-detail QR popup; edited from App/Admin/PaymentSettings.aspx.
    ''' </summary>
    Public Class PaymentSetting

        Public Property Id As Integer
        ''' <summary>'GCASH' or 'GOTYME'.</summary>
        Public Property Channel As String

        Public Property AccountName As String
        Public Property AccountNumber As String

        ''' <summary>Root-relative path of the uploaded QR image ("" when none is set).</summary>
        Public Property QrImageFile As String

        ''' <summary>
        ''' The QR image bytes, stored in the database rather than on disk. Nothing()
        ''' when no image is stored. This is the source of truth; QrImageFile is only
        ''' read for rows uploaded before the move into the database.
        ''' </summary>
        Public Property QrImageData As Byte()

        ''' <summary>Content type of QrImageData, e.g. "image/png" ("" when unknown).</summary>
        Public Property QrImageMime As String

        ''' <summary>True when there is an image to show, from either source.</summary>
        Public ReadOnly Property HasQrImage As Boolean
            Get
                Return (QrImageData IsNot Nothing AndAlso QrImageData.Length > 0) OrElse
                       QrImageFile <> ""
            End Get
        End Property

        ''' <summary>Optional admin caption shown under the QR code.</summary>
        Public Property QrCaption As String

        ''' <summary>What the customer-facing popup shows: BOTH / QR_ONLY / NUMBER_NAME / NAME_ONLY.</summary>
        Public Property QrDisplayMode As String

        ''' <summary>When FALSE the channel is hidden from checkout and the QR popup.</summary>
        Public Property IsEnabled As Boolean

        Public Property UpdatedBy As String
        Public Property UpdatedAt As Date

        ''' <summary>GCASH -> GCash, GOTYME -> GOtyme, anything else passes through.</summary>
        Public ReadOnly Property DisplayChannel As String
            Get
                Return DisplayName(Channel)
            End Get
        End Property

        ''' <summary>
        ''' Customer-facing brand name for a stored payment method. The pre-rename
        ''' 'MAYA' value is still mapped, so orders recorded before the switch to
        ''' GOtyme are labelled with the wallet the customer actually paid with.
        ''' </summary>
        Public Shared Function DisplayName(raw As String) As String
            Select Case If(raw, "").Trim().ToUpperInvariant()
                Case "GCASH" : Return "GCash"
                Case "GOTYME", "MAYA" : Return "GOtyme"
                Case Else : Return If(raw, "")
            End Select
        End Function

        ''' <summary>True for either e-wallet, which always need a reference number.</summary>
        Public Shared Function IsEWallet(raw As String) As Boolean
            Select Case If(raw, "").Trim().ToUpperInvariant()
                Case "GCASH", "GOTYME", "MAYA" : Return True
                Case Else : Return False
            End Select
        End Function

        ''' <summary>
        ''' Where a customer goes when a payment cannot be confirmed. Held here, not in
        ''' the page layer, because the service layer writes the same sentence into its
        ''' failure messages — and a customer who reads a different phone number on the
        ''' order page than on the merchant console has been told to trust two stores.
        ''' </summary>
        Public Shared ReadOnly Property SupportContact As String =
            "For concerns please message @star.d0mm on instagram or contact 09701375033."

        ''' <summary>
        ''' A failed-payment sentence. It always opens with the same fixed line the
        ''' customer is told to quote when they get in touch, then gives the reason,
        ''' then who to talk to. The fixed line comes FIRST on purpose: it is the
        ''' sentence support reads back, and a reason that varies per cause used to
        ''' push it off the end of the flash on long ones.
        ''' </summary>
        Public Shared Function PaymentFailedMessage(detail As String) As String
            Const headline As String = "Order / payment failed."
            Dim why As String = If(detail, "").Trim()
            If why = "" Then why = "The payment could not be verified."
            Return headline & " " & why & " " & SupportContact
        End Function

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
