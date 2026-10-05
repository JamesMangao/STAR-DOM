Imports STAR_DOM.Database
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class CommissionService

        Private ReadOnly _repo As New CommissionRepository()
        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _notif As New NotificationService()

        Public Function PrimaryMerchantId() As Integer
            ' PostgreSQL has no MySQL FIELD(); a CASE orders ADMIN first. With the
            ' two-role model the owner (ADMIN) is the commission artist.
            Dim id As Integer = Db.ScalarInt(
                "SELECT u.Id FROM Users u JOIN Roles r ON r.Id = u.RoleId " &
                "WHERE (r.Name = 'ADMIN' OR r.Name = 'MERCHANT') AND u.Status = 'ACTIVE' " &
                "ORDER BY CASE WHEN r.Name = 'ADMIN' THEN 0 ELSE 1 END, u.Id LIMIT 1")
            Return id
        End Function

        ' ----- Submission -------------------------------------------------------

        Public Function Submit(merchantId As Integer, categoryId As Integer, title As String, description As String,
                               quantity As Integer, preferredSize As String, deadline As Date?,
                               budgetMin As Decimal?, budgetMax As Decimal?, additionalNotes As String,
                               references As List(Of (file As String, name As String, kb As Integer)),
                               Optional shippingAddress As String = "",
                               Optional contactPhone As String = "") As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If merchantId <= 0 Then merchantId = PrimaryMerchantId()

            ' Normalise before anything else. An optional field the buyer never
            ' touched - notes is the one everybody skips - is absent from the POST
            ' entirely, and .Trim() on that null threw a NullReferenceException out
            ' of this method. The request page caught it and answered with a bare
            ' Object reference not set to an instance of an object, so a perfectly
            ' valid commission could not be created at all. The delivery fields
            ' below have the same exposure, so every string is collapsed to a
            ' non-null value here once instead of being defended at each use.
            title = If(title, "").Trim()
            description = If(description, "").Trim()
            preferredSize = If(preferredSize, "").Trim()
            additionalNotes = If(additionalNotes, "").Trim()
            shippingAddress = If(shippingAddress, "").Trim()
            contactPhone = If(contactPhone, "").Trim()

            Dim errors As New List(Of String)()
            errors.Add(Validators.Required(title, "Title"))
            errors.Add(Validators.Required(description, "Commission description"))
            errors.Add(Validators.IntegerValue(quantity.ToString(), "Quantity", 1, 99999))
            errors.Add(Validators.Required(preferredSize, "Preferred size"))
            ' The finished piece is delivered, so the address is collected up front
            ' rather than chased at the end — there is no map pin to fall back on.
            errors.Add(Validators.Required(shippingAddress, "Delivery address"))
            errors.Add(Validators.Phone(contactPhone))
            If budgetMin.HasValue AndAlso budgetMax.HasValue AndAlso budgetMax.Value < budgetMin.Value Then
                errors.Add("Maximum budget must be at least the minimum budget.")
            End If
            Dim clean As String() = errors.Where(Function(e) e IsNot Nothing).ToArray()
            If clean.Length > 0 Then
                Validators.Alert(clean, "Commission incomplete")
                Return ServiceResult.Fail(clean(0))
            End If

            ' CommissionNumber is a unique placeholder; the real number is set after the
            ' auto-increment id is known, so concurrent submissions can never collide.
            ' "REQ-P" plus a 32-char GUID is 37 characters, which fits the
            ' commissions.commissionnumber column (varchar 40). The longer
            ' "REQ-PLACEHOLDER-" prefix was 48 characters, so every new submission
            ' died on a 22001 value-too-long error before it could be created.
            Dim number As String = ""
            Dim cm As New Commission() With {
                .CommissionNumber = "REQ-P" & Guid.NewGuid().ToString("N"),
                .CustomerId = Session.CurrentUser.Id,
                .MerchantId = merchantId,
                .CategoryId = categoryId,
                .Title = title,
                .Description = description,
                .Quantity = quantity,
                .PreferredSize = preferredSize,
                .PreferredDeadline = deadline,
                .BudgetMin = budgetMin,
                .BudgetMax = budgetMax,
                .AdditionalNotes = additionalNotes,
                .ShippingAddress = shippingAddress,
                .ContactPhone = contactPhone,
                .Status = CommissionStatuses.Submitted
            }
            Dim id As Integer = _repo.Create(cm)
            cm.Id = id
            number = "REQ-2026-" & id.ToString("D3")
            _repo.SetCommissionNumber(id, number)

            Dim sort As Integer = 0
            For Each ref In references
                _repo.AddReferenceImage(id, ref.file, ref.name, ref.kb, sort)
                sort += 1
            Next

            _repo.UpdateStatus(id, "", CommissionStatuses.Submitted, Session.DisplayName, "Customer submitted request")
            _notif.Notify(merchantId, "New commission – " & number,
                          Session.DisplayName & " submitted a " & title & " request (Qty " & quantity.ToString() &
                          "). Review it in the Commission Pipeline.",
                          "COMMISSION", "commission-pipeline")
            _notif.Notify(Session.CurrentUser.Id, "Commission submitted – " & number,
                          "Your request is now PENDING REVIEW. You'll be notified of the merchant's response.",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Commission " & number & " submitted!", id)
        End Function

        ' ----- Access rules ------------------------------------------------------

        ''' <summary>
        ''' True when the signed-in user owns this commission: either they are the
        ''' customer who requested it, or they are the artist it was sent to.
        ''' </summary>
        ''' <remarks>
        ''' Session.CanManageStore on its own is NOT a valid gate. It is true for the
        ''' admin AND for every merchant, so using it directly meant any artist could
        ''' open another artist's commission — and then act on it — just by putting a
        ''' different id in the URL. Request ownership is now part of the check.
        ''' </remarks>
        Public Function CanAccess(cm As Commission) As Boolean
            If cm Is Nothing Or Not Session.IsAuthenticated Then Return False
            Dim uid As Integer = Session.CurrentUser.Id
            If cm.CustomerId = uid Then Return True
            Return cm.MerchantId = uid AndAlso Session.CanManageStore
        End Function

        ''' <summary>Merchant-side actions: the artist this commission is addressed to, and only them.</summary>
        Public Function CanManageCommission(cm As Commission) As Boolean
            If cm Is Nothing Or Not Session.CanManageStore Then Return False
            Return cm.MerchantId = Session.CurrentUser.Id
        End Function

        ' ----- Merchant actions -------------------------------------------------
        ' RequestClarification is gone: the studio accepts a request or declines
        ' it, so a vague brief has nowhere to go but a declined request.

        ''' <summary>
        ''' The artist accepts the request and prices it themselves — there is no
        ''' deposit and no fixed menu price. The customer then pays this one figure in
        ''' full once they confirm the offer.
        ''' </summary>
        Public Function AcceptAndOffer(commissionId As Integer, finalPrice As Decimal, completionDate As Date?,
                                       merchantNotes As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
            If finalPrice <= 0 Then Return ServiceResult.Fail("Final price must be greater than zero.")

            _repo.SetOffer(commissionId, finalPrice, completionDate, merchantNotes)
            Dim err As String = _repo.UpdateStatus(commissionId, "", CommissionStatuses.OfferSent,
                                                   Session.DisplayName, "Merchant accepted and sent an offer")
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.CustomerId, "Offer received – " & cm.CommissionNumber,
                          "The merchant sent you a quote of " & Fmt.PHP(finalPrice) &
                          If(completionDate.HasValue, " with completion by " & Fmt.DateF(completionDate.Value), "") &
                          ". Confirm to proceed.",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Offer sent to the customer.")
        End Function

        Public Function Decline(commissionId As Integer, note As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
            _repo.UpdateStatus(commissionId, "", CommissionStatuses.Declined, Session.DisplayName, note)
            _notif.Notify(cm.CustomerId, "Commission declined – " & cm.CommissionNumber,
                          If(String.IsNullOrWhiteSpace(note), "The merchant declined your request.", note),
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Commission declined.")
        End Function

        ' ----- Customer actions -------------------------------------------------
        ' ReplyToClarification is gone with the clarification round-trip.

        Public Function ConfirmOffer(commissionId As Integer) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If cm.CustomerId <> Session.CurrentUser.Id Then Return ServiceResult.Fail("Not your commission.")
            If Not String.Equals(cm.Status, CommissionStatuses.OfferSent, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Fail("There is no active offer to confirm.")
            End If
            _repo.UpdateStatus(commissionId, CommissionStatuses.OfferSent, CommissionStatuses.CustomerConfirmed,
                               Session.DisplayName, "Customer confirmed the offer")
            _notif.Notify(cm.MerchantId, "Offer confirmed – " & cm.CommissionNumber,
                          Session.DisplayName & " confirmed your quote. Awaiting payment.",
                          "COMMISSION", "commission-pipeline")
            Return ServiceResult.Ok("Offer confirmed. Please complete payment to start production.")
        End Function

        ''' <summary>
        ''' The customer says they have paid the quoted price in full. The reference
        ''' number from their e-wallet receipt is mandatory: without it there is nothing
        ''' to trace the money back to, and the studio would be confirming a payment on
        ''' the strength of someone's word alone.
        '''
        ''' This does NOT confirm the payment. It parks the commission in PAYMENT
        ''' PENDING so the studio can verify it — the same two-step an order goes
        ''' through, and the reason production never starts on an unverified transfer.
        ''' </summary>
        Public Function SubmitPayment(commissionId As Integer, reference As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If cm.CustomerId <> Session.CurrentUser.Id Then Return ServiceResult.Fail("Not your commission.")
            If Not (String.Equals(cm.Status, CommissionStatuses.CustomerConfirmed, StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(cm.Status, CommissionStatuses.PaymentPending, StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(cm.Status, CommissionStatuses.PaymentDeclined, StringComparison.OrdinalIgnoreCase)) Then
                Return ServiceResult.Fail("You can only pay once you have confirmed the offer.")
            End If
            If String.Equals(cm.Status, CommissionStatuses.PaymentPending, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Ok("Payment already submitted — waiting for the studio to confirm it.")
            End If

            Dim ref As String = If(reference, "").Trim()
            If ref = "" Then
                Return ServiceResult.Fail(PaymentSetting.PaymentFailedMessage(
                    "Enter the GCash or GOtyme reference number from your payment receipt."))
            End If

            _repo.SetPaymentReference(commissionId, ref)
            Dim fromStatus As String = If(String.Equals(cm.Status, CommissionStatuses.PaymentDeclined, StringComparison.OrdinalIgnoreCase),
                                          CommissionStatuses.PaymentDeclined, CommissionStatuses.CustomerConfirmed)
            Dim err As String = _repo.UpdateStatus(commissionId, fromStatus,
                                                    CommissionStatuses.PaymentPending, Session.DisplayName,
                                                    "Customer submitted payment (ref " & ref & ")")
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.MerchantId, "Payment submitted – " & cm.CommissionNumber,
                          Session.DisplayName & " submitted their payment (ref " & ref & "). " &
                          "Please verify it so production can start.",
                          "COMMISSION", "commission-pipeline")
            _notif.Notify(cm.CustomerId, "Payment submitted – " & cm.CommissionNumber,
                          "We've got your payment details. The studio will confirm it shortly.",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Payment submitted. The studio will confirm it before production starts.")
        End Function

        ''' <summary>
        ''' Studio-side verification of a submitted commission payment. Production is
        ''' gated on this, exactly as J&amp;T booking is gated on an order's confirmed
        ''' payment, so an unverified transfer can never be worked on.
        ''' </summary>
        Public Function ConfirmPayment(commissionId As Integer) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
            If Not String.Equals(cm.Status, CommissionStatuses.PaymentPending, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Fail("There is no submitted payment waiting to be confirmed.")
            End If
            If String.IsNullOrWhiteSpace(cm.PaymentReference) Then
                Return ServiceResult.Fail(PaymentSetting.PaymentFailedMessage(
                    "No reference number was submitted, so this payment cannot be verified."))
            End If
            _repo.MarkPaymentConfirmed(commissionId)
            Dim err As String = _repo.UpdateStatus(commissionId, CommissionStatuses.PaymentPending,
                                                    CommissionStatuses.Paid, Session.DisplayName,
                                                    "Payment confirmed (ref " & cm.PaymentReference & ")")
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.CustomerId, "Payment confirmed – " & cm.CommissionNumber,
                          "Your payment (ref " & cm.PaymentReference & ") is confirmed. Production is starting.",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Payment confirmed. Production can begin.")
        End Function

        ''' <summary>
        ''' The seller declines a customer's submitted payment reference — the amount
        ''' on the e-wallet receipt does not match, or the transfer cannot be traced.
        ''' The commission moves to PAYMENT DECLINED and the customer is handed the
        ''' support contact immediately.
        ''' </summary>
        Public Function DenyPayment(commissionId As Integer) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
            If Not String.Equals(cm.Status, CommissionStatuses.PaymentPending, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Fail("There is no submitted payment waiting to be declined.")
            End If
            Dim err As String = _repo.UpdateStatus(commissionId, CommissionStatuses.PaymentPending,
                                                    CommissionStatuses.PaymentDeclined, Session.DisplayName,
                                                    "Seller declined the payment reference")
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.CustomerId, "Payment declined – " & cm.CommissionNumber,
                          PaymentSetting.PaymentFailedMessage(
                              "The seller could not confirm your payment for this commission. " &
                              "Please check your reference number and resubmit."),
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Payment declined. The customer has been notified.")
        End Function

        Public Function Cancel(commissionId As Integer) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not CanAccess(cm) Then Return ServiceResult.Fail("Not authorized.")
            _repo.UpdateStatus(commissionId, "", CommissionStatuses.Cancelled, Session.DisplayName, "Request cancelled")
            Return ServiceResult.Ok("Commission cancelled.")
        End Function

        ' ----- Merchant production states --------------------------------------

        Public Function StartProduction(commissionId As Integer) As ServiceResult
            Return MerchantTransition(commissionId, CommissionStatuses.Paid, CommissionStatuses.InProduction, "Production started")
        End Function

        Public Function RequestRevision(commissionId As Integer) As ServiceResult
            Return MerchantTransition(commissionId, CommissionStatuses.InProduction, CommissionStatuses.Revision, "Revision requested")
        End Function

        Public Function FinalizeWork(commissionId As Integer) As ServiceResult
            Return MerchantTransition(commissionId, CommissionStatuses.InProduction, CommissionStatuses.Finalized, "Work finalized")
        End Function

        ''' <summary>
        ''' Hand the finished piece over. A commission is delivered the same way an
        ''' order is: booking a courier is optional (most are collected in person, so
        ''' the tracking number may stay blank), but the customer is the one who
        ''' confirms receipt — the studio cannot mark its own parcel as received.
        ''' </summary>
        Public Function MarkDelivered(commissionId As Integer, Optional tracking As String = "") As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
            If Not String.Equals(cm.Status, CommissionStatuses.Finalized, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Fail("Only a finalized commission can be delivered.")
            End If
            If String.IsNullOrWhiteSpace(cm.ShippingAddress) Then
                Return ServiceResult.Fail("This commission has no delivery address on file.")
            End If
            _repo.SetDelivery(commissionId, Trim(If(tracking, "")))
            Dim err As String = _repo.UpdateStatus(commissionId, CommissionStatuses.Finalized,
                                                    CommissionStatuses.Delivered, Session.DisplayName,
                                                    "Delivered to customer")
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.CustomerId, "Commission delivered – " & cm.CommissionNumber,
                          "Your commission has been delivered. Please confirm you received it.",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Commission delivered — waiting for the customer to confirm receipt.")
        End Function

        ''' <summary>
        ''' Customer confirms the finished piece reached them. Only ever written from
        ''' DELIVERED, so a non-null ReceivedAt is the buyer's word rather than the
        ''' studio's — which is what closes the loop the same way an order does.
        ''' </summary>
        Public Function MarkReceived(commissionId As Integer) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If cm.CustomerId <> Session.CurrentUser.Id Then Return ServiceResult.Fail("Not your commission.")
            If String.Equals(cm.Status, CommissionStatuses.Received, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Ok("You already confirmed this commission.")
            End If
            If Not String.Equals(cm.Status, CommissionStatuses.Delivered, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Fail("You can confirm this once it shows as delivered.")
            End If
            _repo.MarkReceived(commissionId)
            Dim err As String = _repo.UpdateStatus(commissionId, CommissionStatuses.Delivered,
                                                    CommissionStatuses.Received, Session.DisplayName,
                                                    "Customer confirmed receipt")
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.MerchantId, "Commission received – " & cm.CommissionNumber,
                          Session.DisplayName & " confirmed your commission arrived. Enjoy!",
                          "COMMISSION", "commission-pipeline")
            Return ServiceResult.Ok("Thanks for confirming! Enjoy your art.")
        End Function

        Private Function MerchantTransition(commissionId As Integer, fromStatus As String, toStatus As String, note As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not CanManageCommission(cm) Then Return ServiceResult.Fail("Not your commission.")
            Dim err As String = _repo.UpdateStatus(commissionId, fromStatus, toStatus, Session.DisplayName, note)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.CustomerId, "Update – " & cm.CommissionNumber,
                          "Your commission is now: " & toStatus.Replace("_", " ") & ".",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Commission moved to " & toStatus & ".")
        End Function

        ' ----- Queries ----------------------------------------------------------

        ''' <summary>
        ''' Reference artwork the customer attached to a commission. Empty when they
        ''' attached none. The rows have existed since uploads were introduced, but
        ''' nothing ever read them, so both sides saw no pictures at all.
        ''' </summary>
        Public Function ReferenceImages(commissionId As Integer) As List(Of CommissionReferenceImage)
            Return _repo.ListReferenceImages(commissionId)
        End Function

        Public Function ListMyCommissions() As List(Of Commission)
            If Not Session.IsAuthenticated Then Return New List(Of Commission)()
            Return _repo.ListByCustomer(Session.CurrentUser.Id)
        End Function

        Public Function ListMerchantCommissions(Optional status As String = "", Optional search As String = "") As List(Of Commission)
            If Not Session.CanManageStore Then Return New List(Of Commission)()
            Return _repo.ListByMerchant(Session.CurrentUser.Id, status, search)
        End Function

        Public Function GetCommission(id As Integer) As Commission
            Return _repo.GetById(id)
        End Function

        Public Function ListStatusHistory(commissionId As Integer) As List(Of CommissionStatusHistory)
            Return _repo.ListStatusHistory(commissionId)
        End Function

    End Class

End Namespace