Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    ''' <summary>
    ''' The studio's earnings ledger: one row per order, held until the parcel is
    ''' confirmed received, voided if the order is refunded.
    ''' </summary>
    ''' <remarks>
    ''' This is a record, not a payment rail. There is no gateway in this
    ''' application that could move money, and there is no attempt to fake one --
    ''' the same reason Orders.REFUNDED records a decision rather than performing a
    ''' refund. The value here is that the question "has the studio actually earned
    ''' this sale" has an answer that can be looked up, and that a refund after
    ''' release is visible as a reversal rather than quietly disappearing.
    '''
    ''' The state machine is deliberately small, and every edge is one-way:
    '''   HELD     -> RELEASED   (the customer confirmed the parcel arrived)
    '''   HELD     -> VOID       (refunded before confirmation; nothing to reverse)
    '''   RELEASED -> VOID       (refunded after confirmation; Reversed = TRUE)
    ''' Nothing moves out of RELEASED or VOID, so a retried sweep or a second
    ''' refund record cannot inflate the books.
    ''' </remarks>
    Public Class CommissionLedgerService

        Private ReadOnly _repo As New CommissionLedgerRepository()
        Private ReadOnly _notif As New NotificationService()

        ''' <summary>
        ''' What the studio earns on an order: the goods total, less bundle savings.
        ''' The courier fee is excluded because it is money collected on J&amp;T's
        ''' behalf and handed straight to them -- counting it as earning would
        ''' overstate every single delivery by the full shipping charge.
        ''' </summary>
        Public Shared Function GoodsBasis(order As Order) As Decimal
            If order Is Nothing Then Return 0D
            Return Math.Max(order.Subtotal - order.DiscountAmount, 0D)
        End Function

        ''' <summary>
        ''' Accrue (or leave alone) the earning for an order that has reached
        ''' DELIVERED. Idempotent: a repeated call, a retried sweep and a dispute
        ''' opened later all land on the same single row.
        ''' </summary>
        Public Sub HoldForOrder(order As Order, Optional reason As String = "")
            If order Is Nothing OrElse order.Id <= 0 Then Return
            _repo.EnsureHeld(order.Id, GoodsBasis(order), "GOODS_TOTAL",
                             If(String.IsNullOrWhiteSpace(reason), "Held from delivery until the customer confirms receipt", reason))
        End Sub

        ''' <summary>Release the earning because the order is now RECEIVED.</summary>
        ''' <remarks>
        ''' Accrues first, then releases. That order matters and is not belt-and-braces:
        ''' the release is a guarded UPDATE on a HELD row, so if the accrual never
        ''' happened the release matches nothing and quietly does nothing -- the order
        ''' becomes RECEIVED with no earning recorded anywhere. Delivery is normally the
        ''' accrual point, but a delivered order can predate the ledger, and the sweep
        ''' closes orders it did not itself deliver. Ensuring the row first makes
        ''' "release on RECEIVED" true on every path that can reach RECEIVED, and
        ''' EnsureHeld is ON CONFLICT DO NOTHING, so an already-accrued row is untouched
        ''' ---- no double commission.
        ''' </remarks>
        Public Sub ReleaseForOrder(order As Order, Optional reason As String = "")
            If order Is Nothing OrElse order.Id <= 0 Then Return
            _repo.EnsureHeld(order.Id, GoodsBasis(order), "GOODS_TOTAL",
                             "Accrued on receipt (held from the courier hand-off)")
            _repo.Release(order.Id, If(reason, ""))
        End Sub

        ''' <summary>
        ''' Void the earning because the order was refunded. A row that had already
        ''' been released is clawed back (Reversed = TRUE); one still held is simply
        ''' voided. Both are attempted without knowing which applies, because each is
        ''' guarded on its own starting state and a mismatch is a no-op rather than an
        ''' error.
        ''' </summary>
        Public Sub VoidForOrder(order As Order, reason As String)
            If order Is Nothing OrElse order.Id <= 0 Then Return
            Dim clawedBack As Integer = _repo.VoidReleased(order.Id, reason)
            Dim voided As Integer = _repo.VoidHeld(order.Id, reason)
            If clawedBack > 0 Then
                Dim row As OrderCommission = _repo.GetByOrder(order.Id)
                Dim amt As Decimal = If(row IsNot Nothing, row.Amount, 0D)
                _notif.NotifyRole("ADMIN", "Commission clawed back – " & order.OrderNumber,
                                  "The " & Fmt.PHP(amt) & " already released for this order has been voided and flagged " &
                                  "as a reversal because the order was refunded.",
                                  "ORDER", "merchant-earnings")
            ElseIf voided > 0 Then
                ' Nothing to flag: the earning was still held, so voiding it is the
                ' whole story and no reversal row is needed.
            End If
        End Sub

        Public Function GetForOrder(orderId As Integer) As OrderCommission
            Return _repo.GetByOrder(orderId)
        End Function

        Public Function ListRecent(Optional limit As Integer = 200) As List(Of OrderCommission)
            Return _repo.ListRecent(limit)
        End Function

        Public Function Totals() As CommissionLedgerTotals
            Return _repo.Totals()
        End Function

    End Class

End Namespace
