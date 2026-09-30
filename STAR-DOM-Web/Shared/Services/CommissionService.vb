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
            Dim id As Integer = Db.ScalarInt(
                "SELECT u.Id FROM Users u JOIN Roles r ON r.Id = u.RoleId " &
                "WHERE (r.Name = 'ADMIN' OR r.Name = 'MERCHANT') ORDER BY FIELD(r.Name, 'ADMIN', 'MERCHANT'), u.Id LIMIT 1")
            Return id
        End Function

        ' ----- Submission -------------------------------------------------------

        Public Function Submit(merchantId As Integer, categoryId As Integer, title As String, description As String,
                               quantity As Integer, preferredSize As String, deadline As Date?,
                               budgetMin As Decimal?, budgetMax As Decimal?, additionalNotes As String,
                               references As List(Of (file As String, name As String, kb As Integer))) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If merchantId <= 0 Then merchantId = PrimaryMerchantId()

            Dim errors As New List(Of String)()
            errors.Add(Validators.Required(title, "Title"))
            errors.Add(Validators.Required(description, "Commission description"))
            errors.Add(Validators.IntegerValue(quantity.ToString(), "Quantity", 1, 99999))
            errors.Add(Validators.Required(preferredSize, "Preferred size"))
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
            Dim number As String = ""
            Dim cm As New Commission() With {
                .CommissionNumber = "REQ-PLACEHOLDER-" & Guid.NewGuid().ToString("N"),
                .CustomerId = Session.CurrentUser.Id,
                .MerchantId = merchantId,
                .CategoryId = categoryId,
                .Title = title.Trim(),
                .Description = description.Trim(),
                .Quantity = quantity,
                .PreferredSize = preferredSize,
                .PreferredDeadline = deadline,
                .BudgetMin = budgetMin,
                .BudgetMax = budgetMax,
                .AdditionalNotes = additionalNotes.Trim(),
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

        ' ----- Merchant actions -------------------------------------------------

        Public Function RequestClarification(commissionId As Integer, message As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
            If String.IsNullOrWhiteSpace(message) Then Return ServiceResult.Fail("Please enter a clarification message.")

            Dim err As String = _repo.UpdateStatus(commissionId, "", CommissionStatuses.ClarificationRequested,
                                                   Session.DisplayName, "Merchant requested clarification")
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _repo.AddMessage(commissionId, Session.CurrentUser.Id, message)
            _notif.Notify(cm.CustomerId, "Clarification requested – " & cm.CommissionNumber,
                          "The merchant asked a question about your commission. Please reply to continue.",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Clarification requested and sent to the customer.")
        End Function

        Public Function AcceptAndOffer(commissionId As Integer, finalPrice As Decimal, completionDate As Date?,
                                       merchantNotes As String, deposit As Decimal?) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
            If finalPrice <= 0 Then Return ServiceResult.Fail("Final price must be greater than zero.")

            _repo.SetOffer(commissionId, finalPrice, completionDate, merchantNotes, deposit)
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
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
            _repo.UpdateStatus(commissionId, "", CommissionStatuses.Declined, Session.DisplayName, note)
            _notif.Notify(cm.CustomerId, "Commission declined – " & cm.CommissionNumber,
                          If(String.IsNullOrWhiteSpace(note), "The merchant declined your request.", note),
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Commission declined.")
        End Function

        ' ----- Customer actions -------------------------------------------------

        Public Function ReplyToClarification(commissionId As Integer, message As String, updated As Commission) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If cm.CustomerId <> Session.CurrentUser.Id Then Return ServiceResult.Fail("Not your commission.")
            If Not String.Equals(cm.Status, CommissionStatuses.ClarificationRequested, StringComparison.OrdinalIgnoreCase) Then
                Return ServiceResult.Fail("This commission is not awaiting clarification.")
            End If

            If updated IsNot Nothing Then _repo.UpdateRequestDetails(updated)
            If Not String.IsNullOrWhiteSpace(message) Then _repo.AddMessage(commissionId, Session.CurrentUser.Id, message)
            _repo.UpdateStatus(commissionId, CommissionStatuses.ClarificationRequested, CommissionStatuses.PendingReview,
                               Session.DisplayName, "Customer replied to clarification")
            _notif.Notify(cm.MerchantId, "Clarification reply – " & cm.CommissionNumber,
                          Session.DisplayName & " replied. The request is back in PENDING REVIEW.",
                          "COMMISSION", "commission-pipeline")
            Return ServiceResult.Ok("Reply sent; your request is back under review.")
        End Function

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

        Public Function MarkPaid(commissionId As Integer, reference As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If cm.CustomerId <> Session.CurrentUser.Id AndAlso Not Session.CanManageStore Then
                Return ServiceResult.Fail("Not authorized.")
            End If
            If Not (String.Equals(cm.Status, CommissionStatuses.CustomerConfirmed, StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(cm.Status, CommissionStatuses.PaymentPending, StringComparison.OrdinalIgnoreCase)) Then
                Return ServiceResult.Fail("Payment can only be recorded after the offer is confirmed.")
            End If
            _repo.UpdateStatus(commissionId, "", CommissionStatuses.Paid, Session.DisplayName,
                               "Payment recorded (ref " & reference & ")")
            _notif.Notify(cm.MerchantId, "Commission paid – " & cm.CommissionNumber,
                          Session.DisplayName & " paid " & Fmt.PHP(If(cm.FinalPrice.HasValue, cm.FinalPrice.Value, 0D)) &
                          ". You may begin production.",
                          "COMMISSION", "commission-pipeline")
            Return ServiceResult.Ok("Payment recorded. Production can begin.")
        End Function

        Public Function Cancel(commissionId As Integer) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If cm.CustomerId <> Session.CurrentUser.Id AndAlso Not Session.CanManageStore Then
                Return ServiceResult.Fail("Not authorized.")
            End If
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

        Public Function Complete(commissionId As Integer) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
            _repo.UpdateStatus(commissionId, "", CommissionStatuses.Completed, Session.DisplayName, "Delivered to customer")
            _notif.Notify(cm.CustomerId, "Commission completed – " & cm.CommissionNumber,
                          "Your commission has been completed and delivered. Enjoy!",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Commission marked completed.")
        End Function

        Private Function MerchantTransition(commissionId As Integer, fromStatus As String, toStatus As String, note As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If Not Session.CanManageStore Then Return ServiceResult.Fail("Not authorized.")
            Dim err As String = _repo.UpdateStatus(commissionId, fromStatus, toStatus, Session.DisplayName, note)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            _notif.Notify(cm.CustomerId, "Update – " & cm.CommissionNumber,
                          "Your commission is now: " & toStatus.Replace("_", " ") & ".",
                          "COMMISSION", "commission-hub")
            Return ServiceResult.Ok("Commission moved to " & toStatus & ".")
        End Function

        ' ----- Queries ----------------------------------------------------------

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

        Public Function ListMessages(commissionId As Integer) As List(Of CommissionMessage)
            Return _repo.ListMessages(commissionId)
        End Function

        Public Function SendMessage(commissionId As Integer, message As String) As ServiceResult
            Dim cm As Commission = _repo.GetById(commissionId)
            If cm Is Nothing Then Return ServiceResult.Fail("Commission not found.")
            If cm.CustomerId <> Session.CurrentUser.Id AndAlso Not Session.CanManageStore Then
                Return ServiceResult.Fail("Not authorized.")
            End If
            If String.IsNullOrWhiteSpace(message) Then Return ServiceResult.Fail("Message cannot be empty.")
            _repo.AddMessage(commissionId, Session.CurrentUser.Id, message)
            Dim recipient As Integer = If(cm.CustomerId = Session.CurrentUser.Id, cm.MerchantId, cm.CustomerId)
            _notif.Notify(recipient, "New message – " & cm.CommissionNumber, message, "COMMISSION",
                          If(recipient = cm.MerchantId, "commission-pipeline", "commission-hub"))
            Return ServiceResult.Ok("Message sent.")
        End Function

        Public Sub MarkRead(commissionId As Integer)
            If Session.IsAuthenticated Then _repo.MarkMessagesRead(commissionId, Session.CurrentUser.Id)
        End Sub

        Public Function ListStatusHistory(commissionId As Integer) As List(Of CommissionStatusHistory)
            Return _repo.ListStatusHistory(commissionId)
        End Function

    End Class

End Namespace