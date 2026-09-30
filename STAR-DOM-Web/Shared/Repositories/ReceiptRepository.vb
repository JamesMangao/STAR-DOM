Imports System.Configuration
Imports System.Web
Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    ''' <summary>Issues and retrieves BIR-style official receipts (OR) for confirmed payments.</summary>
    Public Class ReceiptRepository

        Private Const ReceiptSelect As String =
            "SELECT r.*, p.PaymentMethod, p.PaidAt " &
            "FROM Receipts r JOIN Payments p ON p.Id = r.PaymentId "

        ''' <summary>Create a receipt for a paid payment. Idempotent per payment (UK Receipts.PaymentId).</summary>
        Public Function Issue(order As Order, items As List(Of OrderItem), paymentId As Integer,
                              paymentMethod As String, paidAt As Date?) As Receipt
            Try
                Dim existing As Receipt = GetByPaymentId(paymentId)
                If existing IsNot Nothing Then Return existing

                Dim subtotal As Decimal = order.Subtotal
                Dim discount As Decimal = order.DiscountAmount
                Dim shipping As Decimal = order.ShippingFee
                Dim total As Decimal = order.TotalAmount
                Dim vatable As Decimal = 0D
                Dim vat As Decimal = 0D
                Dim vatExempt As Decimal = 0D
                If total > 0D Then
                    vatable = Math.Round(total / 1.12D, 2)
                    vat = Math.Round(total - vatable, 2)
                Else
                    vatExempt = 0D
                End If

                Dim snapshot As String = CSV(items)

                Dim nextId As Integer = Db.ScalarInt("SELECT COALESCE(MAX(Id), 0) + 1 FROM Receipts")
                Dim number As String = "OR-" & Date.Now.Year.ToString() & "-" & nextId.ToString("D5")

                Db.Exec(
                    "INSERT INTO Receipts (PaymentId, OrderId, ReceiptNumber, ReceiptType, " &
                    "IssuerName, IssuerTin, IssuerAddress, IssuerAccreditation, " &
                    "SoldToName, SoldToAddress, Subtotal, DiscountAmount, ShippingFee, " &
                    "VatableAmount, VatAmount, VatExemptAmount, TotalAmount, ItemsSnapshot, IssuedAt) " &
                    "VALUES (@p, @o, @num, 'OR', @in, @it, @ia, @iac, @sn, @sa, " &
                    "@sub, @d, @sf, @va, @vat, @ve, @tot, @itm, NOW())",
                    Db.P("@p", paymentId), Db.P("@o", order.Id), Db.P("@num", number),
                    Db.P("@in", IssuerValue("Receipt.RegisteredName", "STAR:DOM Artisan Marketplace & Pop-up Tour")),
                    Db.P("@it", IssuerValue("Receipt.IssuerTin", "000-000-000-000")),
                    Db.P("@ia", IssuerValue("Receipt.IssuerAddress", "Unit 12, South Luzon Atelier, Santa Rosa, Laguna")),
                    Db.P("@iac", IssuerValue("Receipt.AccreditationNo", "PTU-2026-00015")),
                    Db.P("@sn", order.CustomerName),
                    Db.P("@sa", order.ShippingAddress),
                    Db.P("@sub", subtotal), Db.P("@d", discount), Db.P("@sf", shipping),
                    Db.P("@va", vatable), Db.P("@vat", vat), Db.P("@ve", vatExempt),
                    Db.P("@tot", total), Db.P("@itm", snapshot))

                Dim fresh As Receipt = GetByNumber(number)
                If fresh IsNot Nothing Then fresh.PaymentMethod = paymentMethod
                Return fresh
            Catch ex As Exception
                Db.LogError("IssueReceipt", ex)
                Return Nothing
            End Try
        End Function

        Public Function GetByPaymentId(paymentId As Integer) As Receipt
            Try
                Dim rows As List(Of DataRow) = Db.Rows(ReceiptSelect & "WHERE r.PaymentId = @p", Db.P("@p", paymentId))
                If rows.Count = 0 Then Return Nothing
                Return Map(rows(0))
            Catch ex As Exception
                Db.LogError("GetReceiptByPayment", ex)
                Return Nothing
            End Try
        End Function

        ''' <summary>Fetch receipts for many payment ids in one query (kills per-row receipt lookups).</summary>
        Public Function GetByPaymentIds(paymentIds As List(Of Integer)) As Dictionary(Of Integer, Receipt)
            Dim result As New Dictionary(Of Integer, Receipt)()
            If paymentIds Is Nothing OrElse paymentIds.Count = 0 Then Return result
            Dim placeholders(0 To paymentIds.Count - 1) As String
            Dim ps(paymentIds.Count - 1) As Npgsql.NpgsqlParameter
            For i As Integer = 0 To paymentIds.Count - 1
                placeholders(i) = "@p" & i.ToString()
                ps(i) = Db.P("@p" & i.ToString(), paymentIds(i))
            Next
            Dim sql As String = ReceiptSelect & "WHERE r.PaymentId IN (" & String.Join(",", placeholders) & ")"
            Try
                For Each r As DataRow In Db.Rows(sql, ps)
                    result(RowReader.AsInt(r, "PaymentId")) = Map(r)
                Next
            Catch ex As Exception
                Db.LogError("GetReceiptsByPaymentIds", ex)
            End Try
            Return result
        End Function

        Public Function GetByOrderId(orderId As Integer) As List(Of Receipt)
            Try
                Return Db.Rows(ReceiptSelect & "WHERE r.OrderId = @o ORDER BY r.IssuedAt", Db.P("@o", orderId)).Select(Function(r) Map(r)).ToList()
            Catch ex As Exception
                Db.LogError("GetReceiptsByOrder", ex)
                Return New List(Of Receipt)()
            End Try
        End Function

        Public Function GetByNumber(receiptNumber As String) As Receipt
            Try
                Dim rows As List(Of DataRow) = Db.Rows(ReceiptSelect & "WHERE r.ReceiptNumber = @n", Db.P("@n", receiptNumber))
                If rows.Count = 0 Then Return Nothing
                Return Map(rows(0))
            Catch ex As Exception
                Db.LogError("GetReceiptByNumber", ex)
                Return Nothing
            End Try
        End Function

        Private Function CSV(items As List(Of OrderItem)) As String
            If items Is Nothing Then Return ""
            Return String.Join(Constants.vbLf, items.Select(Function(i) _
                i.Quantity.ToString() & " x " & i.ProductName & " @ " & i.UnitPrice.ToString("0.00#") & " = " & i.LineTotal.ToString("0.00#")))
        End Function

        Private Shared Function IssuerValue(key As String, fallback As String) As String
            Dim v As String = ConfigurationManager.AppSettings(key)
            If String.IsNullOrWhiteSpace(v) Then Return fallback
            Return HttpUtility.HtmlDecode(v)
        End Function

        Private Shared Function Map(r As DataRow) As Receipt
            Return New Receipt With {
                .Id = RowReader.AsInt(r, "Id"), .PaymentId = RowReader.AsInt(r, "PaymentId"),
                .OrderId = RowReader.AsInt(r, "OrderId"), .ReceiptNumber = RowReader.AsStr(r, "ReceiptNumber"),
                .ReceiptType = RowReader.AsStr(r, "ReceiptType"),
                .IssuerName = RowReader.AsStr(r, "IssuerName"), .IssuerTin = RowReader.AsStr(r, "IssuerTin"),
                .IssuerAddress = RowReader.AsStr(r, "IssuerAddress"), .IssuerAccreditation = RowReader.AsStr(r, "IssuerAccreditation"),
                .SoldToName = RowReader.AsStr(r, "SoldToName"), .SoldToAddress = RowReader.AsStr(r, "SoldToAddress"),
                .Subtotal = RowReader.AsDec(r, "Subtotal"), .DiscountAmount = RowReader.AsDec(r, "DiscountAmount"),
                .ShippingFee = RowReader.AsDec(r, "ShippingFee"), .VatableAmount = RowReader.AsDec(r, "VatableAmount"),
                .VatAmount = RowReader.AsDec(r, "VatAmount"), .VatExemptAmount = RowReader.AsDec(r, "VatExemptAmount"),
                .TotalAmount = RowReader.AsDec(r, "TotalAmount"), .ItemsSnapshot = RowReader.AsStr(r, "ItemsSnapshot"),
                .IssuedAt = RowReader.AsDate(r, "IssuedAt"),
                .PaymentMethod = RowReader.AsStr(r, "PaymentMethod"), .PaidAt = RowReader.AsNullableDate(r, "PaidAt")
            }
        End Function

    End Class

End Namespace