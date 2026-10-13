Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    ''' <summary>
    ''' Reads and writes the one-row-per-order commission ledger.
    ''' </summary>
    ''' <remarks>
    ''' Every state change here is expressed as a guarded UPDATE rather than a
    ''' read-modify-write, because the delivery sweep is scheduled, retried and
    ''' occasionally re-run by hand. A guard in the WHERE clause means a second run
    ''' matches zero rows and does nothing, which is what makes the sweep safe to
    ''' re-run -- and is the whole reason no double commission can be accrued.
    ''' </remarks>
    Public Class CommissionLedgerRepository

        ''' <summary>
        ''' Accrue the studio's earning on an order, if it has not been accrued yet.
        ''' Idempotent via UNIQUE(OrderId) + ON CONFLICT DO NOTHING.
        ''' </summary>
        ''' <remarks>
        ''' DO NOTHING rather than DO UPDATE on purpose: once a row exists its amount
        ''' and status are the record of what happened, and a re-run of the sweep must
        ''' not be able to rewrite that. Only a genuine state change (release, void)
        ''' moves a row afterwards, and each of those has its own guarded method.
        ''' </remarks>
        Public Sub EnsureHeld(orderId As Integer, amount As Decimal, basis As String, reason As String)
            Db.Exec(
                "INSERT INTO OrderCommissions (OrderId, Amount, Basis, Status, Reversed, HeldAt, Reason, UpdatedAt) " &
                "VALUES (@o, @a, @b, 'HELD', FALSE, NOW(), @r, NOW()) " &
                "ON CONFLICT (OrderId) DO NOTHING",
                Db.P("@o", orderId), Db.P("@a", amount), Db.P("@b", If(basis, "GOODS_TOTAL")),
                Db.P("@r", If(reason, "")))
        End Sub

        ''' <summary>
        ''' Release a held earning. Only a HELD row moves, so releasing twice (a user
        ''' confirming receipt after the sweep already closed the order) is a no-op.
        ''' Returns rows touched.
        ''' </summary>
        Public Function Release(orderId As Integer, reason As String) As Integer
            Return Db.Exec(
                "UPDATE OrderCommissions SET Status = 'RELEASED', ReleasedAt = COALESCE(ReleasedAt, NOW()), " &
                "Reason = CASE WHEN @r = '' THEN Reason ELSE @r END, UpdatedAt = NOW() " &
                "WHERE OrderId = @o AND Status = 'HELD'",
                Db.P("@r", If(reason, "")), Db.P("@o", orderId))
        End Function

        ''' <summary>
        ''' Void an earning the studio had not yet been given. Nothing is reversed
        ''' because nothing was released, so Reversed stays FALSE.
        ''' </summary>
        Public Function VoidHeld(orderId As Integer, reason As String) As Integer
            Return Db.Exec(
                "UPDATE OrderCommissions SET Status = 'VOID', Reversed = FALSE, VoidedAt = COALESCE(VoidedAt, NOW()), " &
                "Reason = CASE WHEN @r = '' THEN Reason ELSE @r END, UpdatedAt = NOW() " &
                "WHERE OrderId = @o AND Status = 'HELD'",
                Db.P("@r", If(reason, "")), Db.P("@o", orderId))
        End Function

        ''' <summary>
        ''' Claw back an earning that had already been released. The row becomes VOID
        ''' AND Reversed, because \"never earned\" and \"earned then taken back\" are
        ''' different entries in the books. Returns rows touched.
        ''' </summary>
        Public Function VoidReleased(orderId As Integer, reason As String) As Integer
            Return Db.Exec(
                "UPDATE OrderCommissions SET Status = 'VOID', Reversed = TRUE, VoidedAt = COALESCE(VoidedAt, NOW()), " &
                "Reason = CASE WHEN @r = '' THEN Reason ELSE @r END, UpdatedAt = NOW() " &
                "WHERE OrderId = @o AND Status = 'RELEASED'",
                Db.P("@r", If(reason, "")), Db.P("@o", orderId))
        End Function

        ''' <summary>The ledger row for one order, joined for display, or Nothing.</summary>
        Public Function GetByOrder(orderId As Integer) As OrderCommission
            Dim rows As List(Of DataRow) = Db.Rows(SelectSql & "WHERE c.OrderId = @o", Db.P("@o", orderId))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        ''' <summary>Newest first, bounded in SQL.</summary>
        Public Function ListRecent(Optional limit As Integer = 200) As List(Of OrderCommission)
            Dim capped As Integer = If(limit < 1, 1, Math.Min(limit, 1000))
            Return Db.Rows(SelectSql & "ORDER BY c.HeldAt DESC, c.Id DESC LIMIT @l",
                           Db.P("@l", capped)).Select(Function(r) Map(r)).ToList()
        End Function

        ''' <summary>
        ''' One row of totals per state, so the ledger page does not have to pull the
        ''' whole table into memory to add it up.
        ''' </summary>
        Public Function Totals() As CommissionLedgerTotals
            Dim t As New CommissionLedgerTotals()
            For Each r As DataRow In Db.Rows(
                "SELECT Status, COUNT(*) AS Cnt, COALESCE(SUM(Amount), 0) AS Amt " &
                "FROM OrderCommissions GROUP BY Status")
                Dim cnt As Integer = RowReader.AsInt(r, "Cnt")
                Dim amt As Decimal = RowReader.AsDec(r, "Amt")
                Select Case RowReader.AsStr(r, "Status").Trim().ToUpperInvariant()
                    Case CommissionLedgerStatuses.Released
                        t.ReleasedCount = cnt
                        t.ReleasedAmount = amt
                    Case CommissionLedgerStatuses.Void
                        t.VoidCount = cnt
                        t.VoidAmount = amt
                    Case Else
                        t.HeldCount = cnt
                        t.HeldAmount = amt
                End Select
            Next
            ' A clawed-back row is counted under VOID by the GROUP BY above, so the
            ' reversal total is gathered separately rather than folded into RELEASED,
            ' where it would inflate the released figure by money that went back.
            t.ReversedCount = Db.ScalarInt("SELECT COUNT(*) FROM OrderCommissions WHERE Reversed")
            Return t
        End Function

        Private Const SelectSql As String =
            "SELECT c.Id, c.OrderId, c.Amount, c.Basis, c.Status, c.Reversed, c.HeldAt, c.ReleasedAt, " &
            "c.VoidedAt, c.Reason, o.OrderNumber, u.FullName AS CustomerName " &
            "FROM OrderCommissions c " &
            "LEFT JOIN Orders o ON o.Id = c.OrderId " &
            "LEFT JOIN Users u ON u.Id = o.UserId "

        Private Function Map(r As DataRow) As OrderCommission
            Return New OrderCommission With {
                .Id = RowReader.AsInt(r, "Id"), .OrderId = RowReader.AsInt(r, "OrderId"),
                .Amount = RowReader.AsDec(r, "Amount"), .Basis = RowReader.AsStr(r, "Basis"),
                .Status = RowReader.AsStr(r, "Status"), .Reversed = RowReader.AsBool(r, "Reversed"),
                .HeldAt = RowReader.AsDate(r, "HeldAt"), .ReleasedAt = RowReader.AsNullableDate(r, "ReleasedAt"),
                .VoidedAt = RowReader.AsNullableDate(r, "VoidedAt"), .Reason = RowReader.AsStr(r, "Reason"),
                .OrderNumber = RowReader.AsStr(r, "OrderNumber"), .CustomerName = RowReader.AsStr(r, "CustomerName")
            }
        End Function

    End Class

End Namespace
