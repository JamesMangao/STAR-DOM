Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class EventService

        Private ReadOnly _repo As New EventRepository()
        Private ReadOnly _notif As New NotificationService()

        Public Function ListLocations() As List(Of StoreLocation)
            Return _repo.ListLocations()
        End Function

        Public Function ListEvents(Optional status As String = "") As List(Of PopUpEvent)
            Return _repo.ListEvents(status)
        End Function

        Public Function ListUpcoming() As List(Of PopUpEvent)
            Return _repo.ListUpcoming()
        End Function

        Public Function CurrentEvent() As PopUpEvent
            Return _repo.GetCurrentEvent()
        End Function

        Public Function GetEvent(id As Integer) As PopUpEvent
            Return _repo.GetEvent(id)
        End Function

        Public Function SaveEvent(ev As PopUpEvent) As ServiceResult
            Dim errs As New List(Of String)()
            errs.Add(Validators.Required(ev.Name, "Event name"))
            errs.Add(Validators.Required(ev.BoothNumber, "Booth/stall number"))
            errs.Add(Validators.Required(ev.OpenTime, "Opening time"))
            errs.Add(Validators.Required(ev.CloseTime, "Closing time"))
            errs.Add(Validators.DateRange(ev.StartDate, ev.EndDate))
            Dim clean As String() = errs.Where(Function(e) e IsNot Nothing).ToArray()
            If clean.Length > 0 Then
                Validators.Alert(clean, "Event incomplete")
                Return ServiceResult.Fail(clean(0))
            End If

            If ev.Id > 0 Then
                _repo.UpdateEvent(ev)
                _notif.NotifyRole("CUSTOMER", "Event updated – " & ev.Name,
                                  "Details for " & ev.Name & " were updated by the merchant.", "EVENT", "popup-locations")
                Return ServiceResult.Ok("Event updated.")
            Else
                ev.Id = _repo.CreateEvent(ev)
                _notif.NotifyRole("CUSTOMER", "New pop-up event – " & ev.Name,
                                  ev.Name & " (" & Fmt.EventWindow(ev.StartDate, ev.EndDate) & ") is on the calendar.",
                                  "EVENT", "popup-locations")
                Return ServiceResult.Ok("Event created.")
            End If
        End Function

        Public Function DeleteEvent(id As Integer) As ServiceResult
            Dim err As String = _repo.DeleteEvent(id)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            Return ServiceResult.Ok("Event deleted.")
        End Function

        Public Function SetCurrent(eventId As Integer) As ServiceResult
            _repo.SetCurrent(eventId)
            Dim ev As PopUpEvent = _repo.GetEvent(eventId)
            _notif.NotifyRole("CUSTOMER", "We've moved! Now open at " & ev.Name,
                              "The active physical booth is now " & ev.Name & " – " & ev.BoothNumber &
                              " (" & ev.HoursText & ").", "EVENT", "popup-locations")
            Return ServiceResult.Ok("Location node migrated to " & ev.Name & ".")
        End Function

        Public Function SetStatus(eventId As Integer, status As String) As ServiceResult
            _repo.SetStatus(eventId, status)
            Return ServiceResult.Ok("Event status set to " & status & ".")
        End Function

        ' ----- Locations ---------------------------------------------------------

        Public Function SaveLocation(l As StoreLocation) As ServiceResult
            Dim errs As New List(Of String)()
            errs.Add(Validators.Required(l.Name, "Location name"))
            errs.Add(Validators.Required(l.City, "City"))
            errs.Add(Validators.Required(l.Region, "Region"))
            Dim clean As String() = errs.Where(Function(e) e IsNot Nothing).ToArray()
            If clean.Length > 0 Then
                Validators.Alert(clean, "Location incomplete")
                Return ServiceResult.Fail(clean(0))
            End If
            If l.Id > 0 Then
                _repo.UpdateLocation(l)
            Else
                l.Id = _repo.CreateLocation(l)
            End If
            Return ServiceResult.Ok("Location saved.")
        End Function

        Public Function DeleteLocation(id As Integer) As ServiceResult
            Dim err As String = _repo.DeleteLocation(id)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            Return ServiceResult.Ok("Location deleted.")
        End Function

        ' ----- Inventory ---------------------------------------------------------

        Public Function Inventory(eventId As Integer) As List(Of EventInventory)
            Return _repo.ListInventory(eventId)
        End Function

        Public Function EventExclusive(eventId As Integer) As List(Of Product)
            Return _repo.ListEventExclusiveProducts(eventId)
        End Function

        Public Function AddInventory(eventId As Integer, productId As Integer, startingStock As Integer, isExclusive As Boolean) As ServiceResult
            Dim err As String = _repo.AddInventory(eventId, productId, startingStock, isExclusive)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            Return ServiceResult.Ok("Product assigned to event inventory.")
        End Function

        Public Function UpdateInventory(id As Integer, startingStock As Integer, isExclusive As Boolean, isActive As Boolean) As ServiceResult
            _repo.UpdateInventory(id, startingStock, isExclusive, isActive)
            Return ServiceResult.Ok("Inventory updated.")
        End Function

        Public Function RemoveInventory(id As Integer) As ServiceResult
            _repo.DeleteInventory(id)
            Return ServiceResult.Ok("Product removed from event inventory.")
        End Function

        ' ----- In-person POS sale -------------------------------------------------

        Public Function RecordInPersonSale(eventId As Integer, productId As Integer, quantity As Integer,
                                           paymentMethod As String, saleType As String, notes As String) As ServiceResult
            Dim products As New ProductRepository()
            Dim p As Product = products.GetById(productId)
            If p Is Nothing Then Return ServiceResult.Fail("Product not found.")
            If quantity < 1 Then Return ServiceResult.Fail("Quantity must be at least 1.")

            Dim sale As New EventSale() With {
                .EventId = eventId,
                .ProductId = productId,
                .Quantity = quantity,
                .UnitPrice = p.EffectivePrice,
                .TotalAmount = p.EffectivePrice * quantity,
                .SaleType = If(saleType.Length = 0, "IN_PERSON", saleType),
                .PaymentMethod = paymentMethod,
                .Notes = notes
            }
            Dim err As String = _repo.RecordSale(sale)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            products.BumpSold(productId, quantity)
            Return ServiceResult.Ok("Sale recorded: " & p.Name & " × " & quantity.ToString() & " = " & Fmt.PHP(sale.TotalAmount))
        End Function

        Public Function ListSales(eventId As Integer) As List(Of EventSale)
            Return _repo.ListSales(eventId)
        End Function

        Public Function ListAllSales() As List(Of EventSale)
            Return _repo.ListAllSales()
        End Function

    End Class

End Namespace