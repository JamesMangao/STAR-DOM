Imports Npgsql
Imports STAR_DOM.Database
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    Public Class EventRepository

        ' ----- Locations --------------------------------------------------------

        Public Function ListLocations() As List(Of StoreLocation)
            Return Db.Rows("SELECT * FROM StoreLocations ORDER BY IsActive DESC, Name").Select(Function(r) MapLocation(r)).ToList()
        End Function

        Public Function GetLocation(id As Integer) As StoreLocation
            Dim rows As List(Of DataRow) = Db.Rows("SELECT * FROM StoreLocations WHERE Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return MapLocation(rows(0))
        End Function

        Public Function CreateLocation(l As StoreLocation) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO StoreLocations (Name, Venue, Address, City, Region, Latitude, Longitude, Contact, IsActive) " &
                "VALUES (@n, @v, @a, @c, @r, @la, @lo, @ct, @i)",
                Db.P("@n", l.Name), Db.P("@v", l.Venue), Db.P("@a", l.Address), Db.P("@c", l.City),
                Db.P("@r", l.Region),
                Db.P("@la", If(l.Latitude.HasValue, CObj(l.Latitude.Value), DBNull.Value)),
                Db.P("@lo", If(l.Longitude.HasValue, CObj(l.Longitude.Value), DBNull.Value)),
                Db.P("@ct", l.Contact), Db.P("@i", l.IsActive))
        End Function

        Public Sub UpdateLocation(l As StoreLocation)
            Db.Exec(
                "UPDATE StoreLocations SET Name = @n, Venue = @v, Address = @a, City = @c, Region = @r, " &
                "Latitude = @la, Longitude = @lo, Contact = @ct, IsActive = @i WHERE Id = @id",
                Db.P("@n", l.Name), Db.P("@v", l.Venue), Db.P("@a", l.Address), Db.P("@c", l.City),
                Db.P("@r", l.Region),
                Db.P("@la", If(l.Latitude.HasValue, CObj(l.Latitude.Value), DBNull.Value)),
                Db.P("@lo", If(l.Longitude.HasValue, CObj(l.Longitude.Value), DBNull.Value)),
                Db.P("@ct", l.Contact), Db.P("@i", l.IsActive), Db.P("@id", l.Id))
        End Sub

        Public Function DeleteLocation(id As Integer) As String
            Dim used As Integer = Db.ScalarInt("SELECT COUNT(*) FROM PopUpEvents WHERE LocationId = @id", Db.P("@id", id))
            If used > 0 Then Return "This location has " & used.ToString() & " event(s). Remove them first."
            Db.Exec("DELETE FROM StoreLocations WHERE Id = @id", Db.P("@id", id))
            Return Nothing
        End Function

        ' ----- Events -----------------------------------------------------------

        Private Const EventSelect As String =
            "SELECT e.*, l.Name AS LocationName, l.Address AS LocationAddress, l.Region AS RegionLabel, l.City AS CityLabel " &
            "FROM PopUpEvents e LEFT JOIN StoreLocations l ON l.Id = e.LocationId "

        Public Function ListEvents(Optional status As String = "", Optional search As String = "") As List(Of PopUpEvent)
            Dim sql As String = EventSelect & "WHERE 1=1 "
            Dim ps As New List(Of NpgsqlParameter)()
            If Not String.IsNullOrEmpty(status) Then
                sql &= "AND e.Status = @s "
                ps.Add(Db.P("@s", status))
            End If
            If Not String.IsNullOrEmpty(search) Then
                sql &= "AND (e.Name LIKE @q OR l.Name LIKE @q) "
                ps.Add(Db.P("@q", "%" & search & "%"))
            End If
            sql &= "ORDER BY CASE WHEN e.EndDate < @now THEN 2 WHEN e.StartDate <= @now THEN 0 ELSE 1 END, e.StartDate"
            ps.Add(Db.P("@now", Clock.SqlNow()))
            Dim evs As List(Of PopUpEvent) = Db.Rows(sql, ps.ToArray()).Select(Function(r) MapEvent(r)).ToList()
            Return evs.OrderBy(Function(e) StatusRank(e.Status)).ThenBy(Function(e) e.StartDate).ToList()
        End Function

        Public Function ListUpcoming() As List(Of PopUpEvent)
            ' The currently-running booth is already the hero on the locations page, so
            ' drop any derived NOW OPEN event from "upcoming" instead of listing it twice
            ' (once as live, once as a countdown card).
            Return Db.Rows(EventSelect & "WHERE e.Status <> 'CANCELLED' AND e.EndDate >= @now ORDER BY e.StartDate",
                           Db.P("@now", Clock.SqlNow())).Select(Function(r) MapEvent(r)).
                   Where(Function(e) e.Status <> "NOW OPEN").ToList()
        End Function

        Public Function GetCurrentEvent() As PopUpEvent
            Dim candidates As List(Of PopUpEvent) =
                Db.Rows(EventSelect & "WHERE e.Status <> 'CANCELLED' AND e.StartDate <= @now AND e.EndDate >= @now " &
                                       "ORDER BY e.EndDate DESC",
                        Db.P("@now", Clock.SqlNow())).Select(Function(r) MapEvent(r)).ToList()
            Return candidates.FirstOrDefault(Function(e) e.Status = "NOW OPEN")
        End Function

        Public Function GetEvent(id As Integer) As PopUpEvent
            Dim rows As List(Of DataRow) = Db.Rows(EventSelect & "WHERE e.Id = @id", Db.P("@id", id))
            If rows.Count = 0 Then Return Nothing
            Return MapEvent(rows(0))
        End Function

        Public Function CreateEvent(e As PopUpEvent) As Integer
            Return Db.ExecIdentity(
                "INSERT INTO PopUpEvents (LocationId, Name, Description, StartDate, EndDate, OpenTime, CloseTime, " &
                "BoothNumber, VenueDetail, Status, FeaturedGuest, IsCurrent, ImageFile, LineupText, CreatedAt, UpdatedAt) " &
                "VALUES (@l, @n, @d, @sd, @ed, @ot, @ct, @b, @vd, @s, @fg, @c, @img, @lt, NOW(), NOW())",
                Db.P("@l", e.LocationId), Db.P("@n", e.Name), Db.P("@d", e.Description),
                Db.P("@sd", Clock.ToInstant(e.StartDate)), Db.P("@ed", Clock.ToInstant(e.EndDate)),
                Db.P("@ot", e.OpenTime), Db.P("@ct", e.CloseTime),
                Db.P("@b", e.BoothNumber), Db.P("@vd", e.VenueDetail), Db.P("@s", e.Status),
                Db.P("@fg", e.FeaturedGuest), Db.P("@c", e.IsCurrent),
                Db.P("@img", e.ImageFile), Db.P("@lt", e.LineupText))
        End Function

        Public Sub UpdateEvent(e As PopUpEvent)
            Db.Exec(
                "UPDATE PopUpEvents SET LocationId = @l, Name = @n, Description = @d, StartDate = @sd, EndDate = @ed, " &
                "OpenTime = @ot, CloseTime = @ct, BoothNumber = @b, VenueDetail = @vd, Status = @s, FeaturedGuest = @fg, " &
                "ImageFile = @img, LineupText = @lt, UpdatedAt = NOW() WHERE Id = @id",
                Db.P("@l", e.LocationId), Db.P("@n", e.Name), Db.P("@d", e.Description),
                Db.P("@sd", Clock.ToInstant(e.StartDate)), Db.P("@ed", Clock.ToInstant(e.EndDate)),
                Db.P("@ot", e.OpenTime), Db.P("@ct", e.CloseTime),
                Db.P("@b", e.BoothNumber), Db.P("@vd", e.VenueDetail), Db.P("@s", e.Status),
                Db.P("@fg", e.FeaturedGuest), Db.P("@img", e.ImageFile), Db.P("@lt", e.LineupText), Db.P("@id", e.Id))
        End Sub

        Public Function DeleteEvent(id As Integer) As String
            Db.Exec("DELETE FROM EventInventory WHERE EventId = @id", Db.P("@id", id))
            Db.Exec("DELETE FROM EventSales WHERE EventId = @id", Db.P("@id", id))
            Db.Exec("DELETE FROM PopUpEvents WHERE Id = @id", Db.P("@id", id))
            Return Nothing
        End Function

        ''' <summary>
        ''' The only manual override an event has. The live booth is derived from the
        ''' clock (GetCurrentEvent), never from a stored "current" flag, so nothing here
        ''' touches IsCurrent — the column stays at its schema default.
        ''' </summary>
        Public Sub SetStatus(eventId As Integer, status As String)
            Db.Exec("UPDATE PopUpEvents SET Status = @s, UpdatedAt = NOW() WHERE Id = @id",
                    Db.P("@s", status), Db.P("@id", eventId))
        End Sub

        ' ----- Event inventory --------------------------------------------------

        Public Function ListInventory(eventId As Integer) As List(Of EventInventory)
            Return Db.Rows(
                "SELECT ei.*, p.Name AS ProductName, p.Sku AS ProductSku, p.BasePrice, p.SalePrice, " &
                "(SELECT pi.ImageFile FROM ProductImages pi WHERE pi.ProductId = p.Id AND pi.IsPrimary = TRUE LIMIT 1) AS ImageFile " &
                "FROM EventInventory ei JOIN Products p ON p.Id = ei.ProductId WHERE ei.EventId = @e ORDER BY ei.Id",
                Db.P("@e", eventId)).Select(Function(r) New EventInventory With {
                .Id = RowReader.AsInt(r, "Id"), .EventId = RowReader.AsInt(r, "EventId"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .StartingStock = RowReader.AsInt(r, "StartingStock"), .SoldQuantity = RowReader.AsInt(r, "SoldQuantity"),
                .RemainingStock = RowReader.AsInt(r, "RemainingStock"), .IsEventExclusive = RowReader.AsBool(r, "IsEventExclusive"),
                .IsActive = RowReader.AsBool(r, "IsActive"), .ProductName = RowReader.AsStr(r, "ProductName"),
                .ProductSku = RowReader.AsStr(r, "ProductSku"),
                .UnitPrice = If(RowReader.AsNullableDec(r, "SalePrice").HasValue AndAlso RowReader.AsNullableDec(r, "SalePrice").Value > 0,
                                RowReader.AsNullableDec(r, "SalePrice").Value, RowReader.AsDec(r, "BasePrice")),
                .ImageFile = RowReader.AsStr(r, "ImageFile")}).ToList()
        End Function

        Public Function AddInventory(eventId As Integer, productId As Integer, startingStock As Integer) As String
            If startingStock < 0 Then Return "Starting stock cannot be negative."
            Dim dup As Integer = Db.ScalarInt("SELECT COUNT(*) FROM EventInventory WHERE EventId = @e AND ProductId = @p",
                                              Db.P("@e", eventId), Db.P("@p", productId))
            If dup > 0 Then Return "This product is already assigned to the event."
            Db.Exec("INSERT INTO EventInventory (EventId, ProductId, StartingStock, SoldQuantity, RemainingStock, IsEventExclusive, IsActive) " &
                    "VALUES (@e, @p, @s, 0, @s, FALSE, 1)",
                    Db.P("@e", eventId), Db.P("@p", productId), Db.P("@s", startingStock))
            Return Nothing
        End Function

        Public Sub UpdateInventory(id As Integer, startingStock As Integer, isActive As Boolean)
            Db.Exec(
                "UPDATE EventInventory SET StartingStock = @s, " &
                "RemainingStock = GREATEST(@s - SoldQuantity, 0), IsActive = @a WHERE Id = @id",
                Db.P("@s", startingStock),
                Db.P("@a", isActive), Db.P("@id", id))
        End Sub

        Public Sub DeleteInventory(id As Integer)
            Db.Exec("DELETE FROM EventInventory WHERE Id = @id", Db.P("@id", id))
        End Sub

        ' ----- Event sales ------------------------------------------------------
        ' Removed: pop-up booths are a marketing presence, not a till. STAR:DOM takes
        ' money online through Orders only, so EventSales is never written and the
        ' read path (RecordSale / ListSales / ListAllSales / MapSale) went with it.
        ' The legacy table and its seed rows are left in place for history; it holds
        ' no rows that any report or dashboard reads any more. EventInventory is
        ' still edited, but as a planned-stock list, so SoldQuantity / RemainingStock
        ' no longer move.

        Public Function StockForEvent(eventId As Integer, productId As Integer) As EventInventory
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT * FROM EventInventory WHERE EventId = @e AND ProductId = @p LIMIT 1",
                Db.P("@e", eventId), Db.P("@p", productId))
            If rows.Count = 0 Then Return Nothing
            Dim r As DataRow = rows(0)
            Return New EventInventory With {
                .Id = RowReader.AsInt(r, "Id"), .EventId = RowReader.AsInt(r, "EventId"), .ProductId = RowReader.AsInt(r, "ProductId"),
                .StartingStock = RowReader.AsInt(r, "StartingStock"), .SoldQuantity = RowReader.AsInt(r, "SoldQuantity"),
                .RemainingStock = RowReader.AsInt(r, "RemainingStock"), .IsEventExclusive = RowReader.AsBool(r, "IsEventExclusive"),
                .IsActive = RowReader.AsBool(r, "IsActive")
            }
        End Function

        ' ----- Event sales ------------------------------------------------------

        Public Function MapLocation(r As DataRow) As StoreLocation
            Return New StoreLocation With {
                .Id = RowReader.AsInt(r, "Id"), .Name = RowReader.AsStr(r, "Name"), .Venue = RowReader.AsStr(r, "Venue"),
                .Address = RowReader.AsStr(r, "Address"), .City = RowReader.AsStr(r, "City"), .Region = RowReader.AsStr(r, "Region"),
                .Latitude = RowReader.AsNullableDec(r, "Latitude"), .Longitude = RowReader.AsNullableDec(r, "Longitude"),
                .Contact = RowReader.AsStr(r, "Contact"), .IsActive = RowReader.AsBool(r, "IsActive")
            }
        End Function

        Private Function MapEvent(r As DataRow) As PopUpEvent
            Dim e As PopUpEvent = New PopUpEvent With {
                .Id = RowReader.AsInt(r, "Id"), .LocationId = RowReader.AsInt(r, "LocationId"),
                .Name = RowReader.AsStr(r, "Name"), .Description = RowReader.AsStr(r, "Description"),
                .StartDate = RowReader.AsDate(r, "StartDate"), .EndDate = RowReader.AsDate(r, "EndDate"),
                .OpenTime = RowReader.AsStr(r, "OpenTime"), .CloseTime = RowReader.AsStr(r, "CloseTime"),
                .BoothNumber = RowReader.AsStr(r, "BoothNumber"), .VenueDetail = RowReader.AsStr(r, "VenueDetail"),
                .Status = RowReader.AsStr(r, "Status"), .FeaturedGuest = RowReader.AsStr(r, "FeaturedGuest"),
                .IsCurrent = RowReader.AsBool(r, "IsCurrent"), .ImageFile = RowReader.AsStr(r, "ImageFile"),
                .LineupText = RowReader.AsStr(r, "LineupText"), .CreatedAt = RowReader.AsDate(r, "CreatedAt"),
                .UpdatedAt = RowReader.AsDate(r, "UpdatedAt"), .LocationName = RowReader.AsStr(r, "LocationName"),
                .LocationAddress = RowReader.AsStr(r, "LocationAddress"), .RegionLabel = RowReader.AsStr(r, "RegionLabel"),
                .CityLabel = RowReader.AsStr(r, "CityLabel")
            }
            Try
                If r.Table.Columns.Contains("TimeZone") Then e.TimeZone = RowReader.AsStr(r, "TimeZone")
            Catch
            End Try
            e.Status = DeriveStatus(e)
            Return e
        End Function

        ''' <summary>
        ''' Effective event status computed from the Asia/Manila clock on every read.
        ''' CANCELLED is a manual merchant override; the rest are derived from the event window
        ''' (StartDate..EndDate) plus today's open/close hours.
        ''' </summary>
        Private Function DeriveStatus(e As PopUpEvent) As String
            If e.Status = "CANCELLED" Then Return "CANCELLED"
            Dim now As Date = Clock.Now
            If now < e.StartDate Then Return "UPCOMING"
            If now > e.EndDate Then Return "ENDED"
            Dim openT As TimeSpan = Clock.ParseTimeOfDay(e.OpenTime).GetValueOrDefault(New TimeSpan(10, 0, 0))
            Dim closeT As TimeSpan = Clock.ParseTimeOfDay(e.CloseTime).GetValueOrDefault(New TimeSpan(21, 0, 0))
            If now.TimeOfDay >= openT AndAlso now.TimeOfDay <= closeT Then Return "NOW OPEN"
            Return "UPCOMING"
        End Function

        Private Function StatusRank(status As String) As Integer
            Select Case status
                Case "NOW OPEN" : Return 0
                Case "UPCOMING" : Return 1
                Case "ENDED" : Return 2
                Case Else : Return 3
            End Select
        End Function

    End Class

End Namespace