Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class UserAddressRepository

        Private Const SelectCols As String =
            "Id, UserId, Label, Address, Barangay, City, Region, PostalCode, Landmark, Phone, IsDefault, CreatedAt "

        Public Function CreateAddress(userId As Integer, label As String, address As String, barangay As String,
                                      city As String, region As String, postalCode As String, landmark As String,
                                      phone As String) As ServiceResult
            ' Unmark any existing default addresses for this user
            Db.Exec("UPDATE UserAddresses SET IsDefault = FALSE WHERE UserId = @uid", Db.P("@uid", userId))

            Dim id As Integer = Db.ExecIdentity(
                "INSERT INTO UserAddresses (UserId, Label, Address, Barangay, City, Region, PostalCode, Landmark, Phone, IsDefault, CreatedAt) " &
                "VALUES (@uid, @label, @address, @brgy, @city, @region, @zip, @landmark, @phone, TRUE, NOW())",
                Db.P("@uid", userId),
                Db.P("@label", label),
                Db.P("@address", address),
                Db.P("@brgy", barangay),
                Db.P("@city", city),
                Db.P("@region", region),
                Db.P("@zip", postalCode),
                Db.P("@landmark", landmark),
                Db.P("@phone", phone))

            Return ServiceResult.Ok("Address saved successfully.", id)
        End Function

        Public Function ListByUserId(userId As Integer) As List(Of UserAddress)
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT " & SelectCols & "FROM UserAddresses WHERE UserId = @uid ORDER BY IsDefault DESC, CreatedAt DESC",
                Db.P("@uid", userId))

            Dim result As New List(Of UserAddress)()
            For Each row As DataRow In rows
                result.Add(Map(row))
            Next
            Return result
        End Function

        Public Function GetAddress(id As Integer, userId As Integer) As UserAddress
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT " & SelectCols & "FROM UserAddresses WHERE Id = @id AND UserId = @uid",
                Db.P("@id", id), Db.P("@uid", userId))

            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        ''' <summary>Save the detailed address from a checkout/commission form, unless
        ''' an address with the same street/barangay/city/zip already exists for this
        ''' user. Returns the address id (0 when nothing was saved).</summary>
        Public Function CreateIfNew(userId As Integer, label As String, address As String, barangay As String,
                                    city As String, region As String, postalCode As String, landmark As String,
                                    phone As String) As Integer
            If String.IsNullOrWhiteSpace(address) Then Return 0
            Dim existing As Integer = Db.ScalarInt(
                "SELECT COALESCE(MIN(Id), 0) FROM UserAddresses WHERE UserId = @uid AND Address = @a " &
                "AND Barangay = @b AND City = @c AND PostalCode = @z",
                Db.P("@uid", userId), Db.P("@a", address.Trim()), Db.P("@b", If(barangay, "").Trim()),
                Db.P("@c", If(city, "").Trim()), Db.P("@z", If(postalCode, "").Trim()))
            If existing > 0 Then Return existing
            Dim r As ServiceResult = CreateAddress(userId, If(String.IsNullOrWhiteSpace(label), "Home", label),
                                                   address, barangay, city, region, postalCode, landmark, phone)
            If r.Success AndAlso r.Payload IsNot Nothing Then Return Convert.ToInt32(r.Payload)
            Return 0
        End Function

        Public Function UpdateAddress(id As Integer, userId As Integer, label As String, address As String, barangay As String,
                                      city As String, region As String, postalCode As String, landmark As String,
                                      phone As String) As ServiceResult
            Dim existing As UserAddress = GetAddress(id, userId)
            If existing Is Nothing Then Return ServiceResult.Fail("Address not found.")

            Db.Exec(
                "UPDATE UserAddresses SET Label = @label, Address = @address, Barangay = @brgy, City = @city, " &
                "Region = @region, PostalCode = @zip, Landmark = @landmark, Phone = @phone " &
                "WHERE Id = @id AND UserId = @uid",
                Db.P("@label", label),
                Db.P("@address", address),
                Db.P("@brgy", barangay),
                Db.P("@city", city),
                Db.P("@region", region),
                Db.P("@zip", postalCode),
                Db.P("@landmark", landmark),
                Db.P("@phone", phone),
                Db.P("@id", id),
                Db.P("@uid", userId))

            Return ServiceResult.Ok("Address updated successfully.")
        End Function

        Private Function Map(row As DataRow) As UserAddress
            Return New UserAddress() With {
                .Id = RowReader.AsInt(row, "Id"),
                .UserId = RowReader.AsInt(row, "UserId"),
                .Label = RowReader.AsStr(row, "Label"),
                .Address = RowReader.AsStr(row, "Address"),
                .Barangay = RowReader.AsStr(row, "Barangay"),
                .City = RowReader.AsStr(row, "City"),
                .Region = RowReader.AsStr(row, "Region"),
                .PostalCode = RowReader.AsStr(row, "PostalCode"),
                .Landmark = RowReader.AsStr(row, "Landmark"),
                .Phone = RowReader.AsStr(row, "Phone"),
                .IsDefault = RowReader.AsBool(row, "IsDefault"),
                .CreatedAt = RowReader.AsDate(row, "CreatedAt")
            }
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
