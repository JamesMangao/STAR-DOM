Imports STAR_DOM.Database
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class CategoryRepository

        ''' <summary>
        ''' Storefront-facing categories: enabled AND holding at least one live product.
        ''' </summary>
        ''' <remarks>
        ''' An enabled category with nothing in it renders as a dead-end filter chip that
        ''' leads to an empty result page, so it is filtered out here rather than left to
        ''' whoever is doing the merchandising. Hiding a category is then automatic: stock
        ''' the first SKU in a new category and the chip appears without anyone toggling a
        ''' flag.
        '''
        ''' ListAll() is untouched — the merchant studio still needs to see and manage
        ''' empty categories in order to put products into them.
        ''' </remarks>
        Public Function ListActive() As List(Of Category)
            Return Db.Rows(
                "SELECT c.* FROM Categories c " &
                "WHERE c.IsActive = TRUE " &
                "AND EXISTS (SELECT 1 FROM Products p WHERE p.CategoryId = c.Id AND p.IsActive = TRUE) " &
                "ORDER BY c.DisplayOrder, c.Name").Select(Function(r) Map(r)).ToList()
        End Function

        Public Function ListAll() As List(Of Category)
            Return Db.Rows("SELECT * FROM Categories ORDER BY DisplayOrder, Name").Select(Function(r) Map(r)).ToList()
        End Function

        Public Function GetById(id As Integer) As Category
            Dim rows As List(Of DataRow) = Db.Rows("SELECT * FROM Categories WHERE Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return Map(rows(0))
        End Function

        Public Function Create(name As String, description As String, displayOrder As Integer, isActive As Boolean) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO Categories (Name, Slug, Description, DisplayOrder, IsActive) VALUES (@n, @s, @d, @o, @a)",
                Db.P("@n", name), Db.P("@s", Slug(name)), Db.P("@d", description), Db.P("@o", displayOrder),
                Db.P("@a", isActive))
        End Function

        Public Sub Update(id As Integer, name As String, description As String, displayOrder As Integer, isActive As Boolean)
            Db.Exec(
                "UPDATE Categories SET Name = @n, Slug = @s, Description = @d, DisplayOrder = @o, IsActive = @a WHERE Id = @id",
                Db.P("@n", name), Db.P("@s", Slug(name)), Db.P("@d", description), Db.P("@o", displayOrder),
                Db.P("@a", isActive), Db.P("@id", id))
        End Sub

        Public Function Delete(id As Integer) As String
            Dim used As Integer = Db.ScalarInt("SELECT COUNT(*) FROM Products WHERE CategoryId = @id", Db.P("@id", id))
            If used > 0 Then Return "This category has " & used.ToString() & " product(s). Reassign or delete them first."
            Db.Exec("DELETE FROM Categories WHERE Id = @id", Db.P("@id", id))
            Return Nothing
        End Function

        Public Function CountProducts(id As Integer) As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Products WHERE CategoryId = @id", Db.P("@id", id))
        End Function

        Private Function Slug(name As String) As String
            Dim s As String = name.ToLowerInvariant().Trim()
            Dim sb As New Text.StringBuilder()
            For Each ch As Char In s
                If Char.IsLetterOrDigit(ch) Then
                    sb.Append(ch)
                ElseIf ch = " "c OrElse ch = "-"c OrElse ch = "_"c Then
                    sb.Append("-")
                End If
            Next
            Return sb.ToString().Trim("-"c)
        End Function

        Private Function Map(r As DataRow) As Category
            Return New Category With {
                .Id = RowReader.AsInt(r, "Id"),
                .Name = RowReader.AsStr(r, "Name"),
                .Slug = RowReader.AsStr(r, "Slug"),
                .Description = RowReader.AsStr(r, "Description"),
                .DisplayOrder = RowReader.AsInt(r, "DisplayOrder"),
                .IsActive = RowReader.AsBool(r, "IsActive"),
                .ParentId = RowReader.AsNullableInt(r, "ParentId")
            }
        End Function

    End Class

End Namespace