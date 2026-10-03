Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class CommissionRepository

        Private Const SelectSql As String =
            "SELECT cm.*, cu.FullName AS CustomerName, cu.Email AS CustomerEmail, m.FullName AS MerchantName, " &
            "c.Name AS CategoryName, " &
            "(SELECT COUNT(*) FROM CommissionReferenceImages ri WHERE ri.CommissionId = cm.Id) AS ReferenceCount, " &
            "(SELECT COUNT(*) FROM CommissionMessages msg WHERE msg.CommissionId = cm.Id) AS MessageCount " &
            "FROM Commissions cm " &
            "JOIN Users cu ON cu.Id = cm.CustomerId " &
            "JOIN Users m ON m.Id = cm.MerchantId " &
            "LEFT JOIN Categories c ON c.Id = cm.CategoryId "

        Public Function Create(cm As Commission) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO Commissions (CommissionNumber, CustomerId, MerchantId, CategoryId, Title, Description, " &
                "Quantity, PreferredSize, PreferredDeadline, BudgetMin, BudgetMax, AdditionalNotes, Status, CreatedAt, UpdatedAt) " &
                "VALUES (@num, @c, @m, @cat, @t, @d, @q, @s, @pd, @bmin, @bmax, @an, @st, NOW(), NOW())",
                Db.P("@num", cm.CommissionNumber), Db.P("@c", cm.CustomerId), Db.P("@m", cm.MerchantId),
                Db.P("@cat", cm.CategoryId), Db.P("@t", cm.Title), Db.P("@d", cm.Description),
                Db.P("@q", cm.Quantity), Db.P("@s", cm.PreferredSize),
                Db.P("@pd", If(cm.PreferredDeadline.HasValue, CObj(cm.PreferredDeadline.Value), DBNull.Value)),
                Db.P("@bmin", If(cm.BudgetMin.HasValue, CObj(cm.BudgetMin.Value), DBNull.Value)),
                Db.P("@bmax", If(cm.BudgetMax.HasValue, CObj(cm.BudgetMax.Value), DBNull.Value)),
                Db.P("@an", cm.AdditionalNotes), Db.P("@st", cm.Status))
        End Function

        Public Sub SetCommissionNumber(id As Integer, number As String)
            Db.Exec("UPDATE Commissions SET CommissionNumber = @num WHERE Id = @id",
                    Db.P("@num", number), Db.P("@id", id))
        End Sub

        Public Function GetById(id As Integer) As Commission
            Dim rows As List(Of DataRow) = Db.Rows(SelectSql & "WHERE cm.Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function GetByNumber(number As String) As Commission
            Dim rows As List(Of DataRow) = Db.Rows(SelectSql & "WHERE cm.CommissionNumber = @n", Db.P("@n", number))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function ListByCustomer(userId As Integer, Optional status As String = "") As List(Of Commission)
            Dim sql As String = SelectSql & "WHERE cm.CustomerId = @u "
            Dim ps As New List(Of NpgsqlParameter)() From {Db.P("@u", userId)}
            If Not String.IsNullOrEmpty(status) Then
                sql &= "AND cm.Status = @s "
                ps.Add(Db.P("@s", status))
            End If
            sql &= "ORDER BY cm.CreatedAt DESC"
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function ListByMerchant(merchantId As Integer, Optional status As String = "", Optional search As String = "") As List(Of Commission)
            Dim sql As String = SelectSql & "WHERE cm.MerchantId = @m "
            Dim ps As New List(Of NpgsqlParameter)() From {Db.P("@m", merchantId)}
            If Not String.IsNullOrEmpty(status) Then
                sql &= "AND cm.Status = @s "
                ps.Add(Db.P("@s", status))
            End If
            If Not String.IsNullOrEmpty(search) Then
                sql &= "AND (cm.CommissionNumber LIKE @q OR cu.FullName LIKE @q OR cm.Title LIKE @q) "
                ps.Add(Db.P("@q", "%" & search & "%"))
            End If
            sql &= "ORDER BY cm.CreatedAt DESC"
            Return Db.Rows(sql, ps.ToArray()).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function ListRecent(limit As Integer) As List(Of Commission)
            Return Db.Rows(SelectSql & "ORDER BY cm.CreatedAt DESC LIMIT @l", Db.P("@l", limit)).Select(Function(r) Map(r)).ToList()
        End Function

        Public Function CountByMerchant(merchantId As Integer, Optional status As String = "") As Integer
            If status.Length = 0 Then
                Return Db.ScalarInt("SELECT COUNT(*) FROM Commissions WHERE MerchantId = @m", Db.P("@m", merchantId))
            End If
            Return Db.ScalarInt("SELECT COUNT(*) FROM Commissions WHERE MerchantId = @m AND Status = @s",
                                Db.P("@m", merchantId), Db.P("@s", status))
        End Function

        ''' <summary>Open-slot counts for all merchants at once (single query instead of 1-per-merchant).</summary>
        Public Function OpenSlotCounts() As Dictionary(Of Integer, Integer)
            Dim counts As New Dictionary(Of Integer, Integer)()
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT MerchantId, COUNT(*) AS Cnt FROM Commissions WHERE Status IN " &
                "('SUBMITTED','PENDING REVIEW','CLARIFICATION REQUESTED','ACCEPTED','OFFER SENT','CUSTOMER CONFIRMED'," &
                "'PAYMENT PENDING','PAID','IN PRODUCTION','REVISION','FINALIZED') GROUP BY MerchantId")
            For Each r As DataRow In rows
                counts(RowReader.AsInt(r, "MerchantId")) = RowReader.AsInt(r, "Cnt")
            Next
            Return counts
        End Function

        ''' <summary>Transition status and record full history. Returns error string or Nothing.</summary>
        Public Function UpdateStatus(id As Integer, fromStatus As String, toStatus As String, changedByName As String, note As String) As String
            Dim current As String = Db.ScalarStr("SELECT Status FROM Commissions WHERE Id = @id", Db.P("@id", id))
            If fromStatus.Length > 0 AndAlso Not String.Equals(current, fromStatus, StringComparison.OrdinalIgnoreCase) Then
                Return "Cannot transition from '" & current & "'. Expected '" & fromStatus & "'."
            End If
            Db.Exec("UPDATE Commissions SET Status = @t, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@t", toStatus), Db.P("@id", id))
            Db.Exec("INSERT INTO CommissionStatusHistory (CommissionId, FromStatus, ToStatus, ChangedBy, Note, CreatedAt) " &
                    "VALUES (@c, @f, @t, @cb, @n, NOW())",
                    Db.P("@c", id), Db.P("@f", current), Db.P("@t", toStatus),
                    Db.P("@cb", changedByName), Db.P("@n", note))
            Return Nothing
        End Function

        Public Sub UpdateRequestDetails(cm As Commission)
            Db.Exec(
                "UPDATE Commissions SET Title = @t, Description = @d, Quantity = @q, PreferredSize = @s, " &
                "PreferredDeadline = @pd, BudgetMin = @bmin, BudgetMax = @bmax, AdditionalNotes = @an, UpdatedAt = NOW() WHERE Id = @id",
                Db.P("@t", cm.Title), Db.P("@d", cm.Description), Db.P("@q", cm.Quantity),
                Db.P("@s", cm.PreferredSize),
                Db.P("@pd", If(cm.PreferredDeadline.HasValue, CObj(cm.PreferredDeadline.Value), DBNull.Value)),
                Db.P("@bmin", If(cm.BudgetMin.HasValue, CObj(cm.BudgetMin.Value), DBNull.Value)),
                Db.P("@bmax", If(cm.BudgetMax.HasValue, CObj(cm.BudgetMax.Value), DBNull.Value)),
                Db.P("@an", cm.AdditionalNotes), Db.P("@id", cm.Id))
        End Sub

        ''' <summary>
        ''' Stores the artist's quote. There is no deposit: the artist prices the
        ''' finished piece themselves when they accept, and the customer pays that one
        ''' figure in full. DepositAmount is deliberately left untouched (NULL for new
        ''' commissions) so the column stays for legacy rows without being read anywhere.
        ''' </summary>
        Public Sub SetOffer(id As Integer, finalPrice As Decimal, completionDate As Date?, merchantNotes As String)
            Db.Exec(
                "UPDATE Commissions SET FinalPrice = @fp, EstimatedCompletionDate = @ed, MerchantNotes = @mn, " &
                "DepositAmount = NULL, UpdatedAt = NOW() WHERE Id = @id",
                Db.P("@fp", finalPrice),
                Db.P("@ed", If(completionDate.HasValue, CObj(completionDate.Value), DBNull.Value)),
                Db.P("@mn", merchantNotes),
                Db.P("@id", id))
        End Sub

        ' ----- Messages ---------------------------------------------------------

        Public Sub AddMessage(commissionId As Integer, senderId As Integer, message As String)
            Db.Exec("INSERT INTO CommissionMessages (CommissionId, SenderId, Message, IsRead, CreatedAt) " &
                    "VALUES (@c, @s, @m, 0, NOW())",
                    Db.P("@c", commissionId), Db.P("@s", senderId), Db.P("@m", message))
        End Sub

        Public Function ListMessages(commissionId As Integer) As List(Of CommissionMessage)
            Return Db.Rows(
                "SELECT msg.*, u.FullName AS SenderName FROM CommissionMessages msg " &
                "JOIN Users u ON u.Id = msg.SenderId WHERE msg.CommissionId = @c ORDER BY msg.CreatedAt ASC",
                Db.P("@c", commissionId)).Select(Function(r) New CommissionMessage With {
                .Id = RowReader.AsInt(r, "Id"), .CommissionId = RowReader.AsInt(r, "CommissionId"),
                .SenderId = RowReader.AsInt(r, "SenderId"), .SenderName = RowReader.AsStr(r, "SenderName"),
                .Message = RowReader.AsStr(r, "Message"), .IsRead = RowReader.AsBool(r, "IsRead"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt")}).ToList()
        End Function

        Public Sub MarkMessagesRead(commissionId As Integer, readerId As Integer)
            Db.Exec("UPDATE CommissionMessages SET IsRead = TRUE WHERE CommissionId = @c AND SenderId <> @r",
                    Db.P("@c", commissionId), Db.P("@r", readerId))
        End Sub

        ' ----- Reference images -------------------------------------------------

        Public Sub AddReferenceImage(commissionId As Integer, imageFile As String, fileName As String, sizeKb As Integer, sortOrder As Integer)
            Db.Exec("INSERT INTO CommissionReferenceImages (CommissionId, ImageFile, FileName, FileSizeKb, SortOrder) " &
                    "VALUES (@c, @f, @n, @s, @o)",
                    Db.P("@c", commissionId), Db.P("@f", imageFile), Db.P("@n", fileName),
                    Db.P("@s", sizeKb), Db.P("@o", sortOrder))
        End Sub

        Public Function ListReferenceImages(commissionId As Integer) As List(Of CommissionReferenceImage)
            Return Db.Rows("SELECT * FROM CommissionReferenceImages WHERE CommissionId = @c ORDER BY SortOrder, Id",
                           Db.P("@c", commissionId)).Select(Function(r) New CommissionReferenceImage With {
                .Id = RowReader.AsInt(r, "Id"), .CommissionId = RowReader.AsInt(r, "CommissionId"),
                .ImageFile = RowReader.AsStr(r, "ImageFile"), .FileName = RowReader.AsStr(r, "FileName"),
                .FileSizeKb = RowReader.AsInt(r, "FileSizeKb"), .SortOrder = RowReader.AsInt(r, "SortOrder")}).ToList()
        End Function

        Public Sub DeleteReferenceImage(imageId As Integer)
            Db.Exec("DELETE FROM CommissionReferenceImages WHERE Id = @id", Db.P("@id", imageId))
        End Sub

        ' ----- Status history ---------------------------------------------------

        Public Function ListStatusHistory(commissionId As Integer) As List(Of CommissionStatusHistory)
            Return Db.Rows(
                "SELECT h.* FROM CommissionStatusHistory h WHERE h.CommissionId = @c ORDER BY h.CreatedAt ASC",
                Db.P("@c", commissionId)).Select(Function(r) New CommissionStatusHistory With {
                .Id = RowReader.AsInt(r, "Id"), .CommissionId = RowReader.AsInt(r, "CommissionId"),
                .FromStatus = RowReader.AsStr(r, "FromStatus"), .ToStatus = RowReader.AsStr(r, "ToStatus"),
                .ChangedByName = RowReader.AsStr(r, "ChangedBy"), .Note = RowReader.AsStr(r, "Note"),
                .CreatedAt = RowReader.AsDate(r, "CreatedAt")}).ToList()
        End Function

        ' ----- Merchant commission slots (hub cards) ----------------------------

        Public Function OpenSlotCount(merchantId As Integer) As Integer
            Return Db.ScalarInt(
                "SELECT COUNT(*) FROM Commissions WHERE MerchantId = @m AND Status IN " &
                "('SUBMITTED','PENDING REVIEW','CLARIFICATION REQUESTED','ACCEPTED','OFFER SENT','CUSTOMER CONFIRMED'," &
                "'PAYMENT PENDING','PAID','IN PRODUCTION','REVISION','FINALIZED')",
                Db.P("@m", merchantId))
        End Function

        Private Function Map(r As DataRow) As Commission
            Return New Commission With {
                .Id = RowReader.AsInt(r, "Id"), .CommissionNumber = RowReader.AsStr(r, "CommissionNumber"),
                .CustomerId = RowReader.AsInt(r, "CustomerId"), .MerchantId = RowReader.AsInt(r, "MerchantId"),
                .CategoryId = RowReader.AsInt(r, "CategoryId"), .Title = RowReader.AsStr(r, "Title"),
                .Description = RowReader.AsStr(r, "Description"), .Quantity = RowReader.AsInt(r, "Quantity"),
                .PreferredSize = RowReader.AsStr(r, "PreferredSize"),
                .PreferredDeadline = RowReader.AsNullableDate(r, "PreferredDeadline"),
                .BudgetMin = RowReader.AsNullableDec(r, "BudgetMin"), .BudgetMax = RowReader.AsNullableDec(r, "BudgetMax"),
                .AdditionalNotes = RowReader.AsStr(r, "AdditionalNotes"),
                .FinalPrice = RowReader.AsNullableDec(r, "FinalPrice"),
                .EstimatedCompletionDate = RowReader.AsNullableDate(r, "EstimatedCompletionDate"),
                .MerchantNotes = RowReader.AsStr(r, "MerchantNotes"),
                .DepositAmount = RowReader.AsNullableDec(r, "DepositAmount"),
                .Status = RowReader.AsStr(r, "Status"), .CreatedAt = RowReader.AsDate(r, "CreatedAt"),
                .UpdatedAt = RowReader.AsDate(r, "UpdatedAt"), .CustomerName = RowReader.AsStr(r, "CustomerName"),
                .CustomerEmail = RowReader.AsStr(r, "CustomerEmail"), .MerchantName = RowReader.AsStr(r, "MerchantName"),
                .CategoryName = RowReader.AsStr(r, "CategoryName"), .ReferenceCount = RowReader.AsInt(r, "ReferenceCount"),
                .MessageCount = RowReader.AsInt(r, "MessageCount")
            }
        End Function

    End Class

End Namespace