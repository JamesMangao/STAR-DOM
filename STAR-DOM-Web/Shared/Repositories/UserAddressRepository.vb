Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class UserAddressRepository

        Public Function CreateAddress(userId As Integer, label As String, address As String, city As String, region As String, phone As String) As ServiceResult
            Dim addr As New UserAddress() With {
                .UserId = userId,
                .Label = label,
                .Address = address,
                .City = city,
                .Region = region,
                .Phone = phone,
                .IsDefault = True  ' First address is default; can be changed later
            }

            ' Unmark any existing default addresses for this user
            Db.Exec("UPDATE UserAddresses SET IsDefault = FALSE WHERE UserId = @uid", Db.P("@uid", userId))

            Dim id As Integer = Db.ExecIdentity("INSERT INTO UserAddresses (UserId, Label, Address, City, Region, Phone, IsDefault, CreatedAt) VALUES (@uid, @label, @address, @city, @region, @phone, TRUE, NOW())",
                Db.P("@uid", userId),
                Db.P("@label", label),
                Db.P("@address", address),
                Db.P("@city", city),
                Db.P("@region", region),
                Db.P("@phone", phone))

            addr.Id = id
            Return ServiceResult.Ok("Address saved successfully.", id)
        End Function

        Public Function ListByUserId(userId As Integer) As List(Of UserAddress)
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT Id, UserId, Label, Address, City, Region, Phone, IsDefault, CreatedAt " &
                "FROM UserAddresses WHERE UserId = @uid ORDER BY IsDefault DESC, CreatedAt DESC",
                Db.P("@uid", userId))

            Dim result As New List(Of UserAddress)()
            For Each row As DataRow In rows
                Dim addr As New UserAddress() With {
                    .Id = RowReader.AsInt(row, "Id"),
                    .UserId = RowReader.AsInt(row, "UserId"),
                    .Label = RowReader.AsStr(row, "Label"),
                    .Address = RowReader.AsStr(row, "Address"),
                    .City = RowReader.AsStr(row, "City"),
                    .Region = RowReader.AsStr(row, "Region"),
                    .Phone = RowReader.AsStr(row, "Phone"),
                    .IsDefault = RowReader.AsBool(row, "IsDefault"),
                    .CreatedAt = RowReader.AsDate(row, "CreatedAt")
                }
                result.Add(addr)
            Next
            Return result
        End Function

        Public Function GetAddress(id As Integer, userId As Integer) As UserAddress
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT Id, UserId, Label, Address, City, Region, Phone, IsDefault, CreatedAt " &
                "FROM UserAddresses WHERE Id = @id AND UserId = @uid",
                Db.P("@id", id), Db.P("@uid", userId))

            If rows.Count = 0 Then Return Nothing

            Dim row As DataRow = rows(0)
            Return New UserAddress() With {
                .Id = RowReader.AsInt(row, "Id"),
                .UserId = RowReader.AsInt(row, "UserId"),
                .Label = RowReader.AsStr(row, "Label"),
                .Address = RowReader.AsStr(row, "Address"),
                .City = RowReader.AsStr(row, "City"),
                .Region = RowReader.AsStr(row, "Region"),
                .Phone = RowReader.AsStr(row, "Phone"),
                .IsDefault = RowReader.AsBool(row, "IsDefault"),
                .CreatedAt = RowReader.AsDate(row, "CreatedAt")
            }
        End Function

        Public Function UpdateAddress(id As Integer, userId As Integer, label As String, address As String, city As String, region As String, phone As String) As ServiceResult
            Dim existing As UserAddress = GetAddress(id, userId)
            If existing Is Nothing Then Return ServiceResult.Fail("Address not found.")

            Dim setDefault As Boolean = False
            If existing.IsDefault Then
                ' Check if this is the only address
                Dim count As Integer = Db.ScalarInt("SELECT COUNT(*) FROM UserAddresses WHERE UserId = @uid", Db.P("@uid", userId))
                If count > 1 Then
                    setDefault = True
                End If
            End If

            Db.Exec(
                "UPDATE UserAddresses SET Label = @label, Address = @address, City = @city, Region = @region, Phone = @phone" &
                If(setDefault, ", IsDefault = TRUE", "") &
                " WHERE Id = @id AND UserId = @uid",
                Db.P("@label", label),
                Db.P("@address", address),
                Db.P("@city", city),
                Db.P("@region", region),
                Db.P("@phone", phone),
                Db.P("@id", id),
                Db.P("@uid", userId))

            Return ServiceResult.Ok("Address updated successfully.")
        End Function

        Public Function DeleteAddress(id As Integer, userId As Integer) As ServiceResult
            Dim existing As UserAddress = GetAddress(id, userId)
            If existing Is Nothing Then Return ServiceResult.Fail("Address not found.")

            ' If this is the default address, unmark it and set another as default
            If existing.IsDefault Then
                Dim altCount As Integer = Db.ScalarInt(
                    "SELECT COUNT(*) FROM UserAddresses WHERE UserId = @uid AND Id <> @id",
                    Db.P("@uid", userId), Db.P("@id", id))

                If altCount = 0 Then
                    ' No other addresses, just delete
                End If
            End If

            Db.Exec("DELETE FROM UserAddresses WHERE Id = @id AND UserId = @uid",
                Db.P("@id", id), Db.P("@uid", userId))

            Return ServiceResult.Ok("Address removed.")
        End Function

        Public Function SetDefaultAddress(id As Integer, userId As Integer) As ServiceResult
            Dim existing As UserAddress = GetAddress(id, userId)
            If existing Is Nothing Then Return ServiceResult.Fail("Address not found.")

            ' Unmark all defaults for this user
            Db.Exec("UPDATE UserAddresses SET IsDefault = FALSE WHERE UserId = @uid", Db.P("@uid", userId))

            ' Set this one as default
            Db.Exec("UPDATE UserAddresses SET IsDefault = TRUE WHERE Id = @id AND UserId = @uid",
                Db.P("@id", id), Db.P("@uid", userId))

            Return ServiceResult.Ok("Default address updated.")
        End Function

    End Class

End Namespace
