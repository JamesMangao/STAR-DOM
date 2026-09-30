Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class CatalogService

        Private ReadOnly _products As New ProductRepository()
        Private ReadOnly _categories As New CategoryRepository()
        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _commissions As New CommissionRepository()

        Public Function ListCategories() As List(Of Category)
            Return _categories.ListActive()
        End Function

        Public Function ListProducts(Optional categoryId As Integer = 0, Optional search As String = "") As List(Of Product)
            Return _products.ListActive(categoryId, search)
        End Function

        Public Function GetProduct(id As Integer) As Product
            Return _products.GetById(id)
        End Function

        Public Function Featured(Optional limit As Integer = 8) As List(Of Product)
            Return _products.ListFeatured(limit)
        End Function

        Public Function EventExclusive(eventId As Integer) As List(Of Product)
            If eventId <= 0 Then Return New List(Of Product)()
            Return (New EventRepository()).ListEventExclusiveProducts(eventId)
        End Function

        ''' <summary>Open commission slot cards for merchants with capacity (drives the hub + marketplace sections).</summary>
        Public Function CommissionSlots() As List(Of CommissionSlotView)
            Dim slots As New List(Of CommissionSlotView)()
            Dim merchants As List(Of User) = _users.ListMerchants()
            Dim openCounts As Dictionary(Of Integer, Integer) = _commissions.OpenSlotCounts()
            For Each m As User In merchants
                Dim used As Integer = 0
                If openCounts.ContainsKey(m.Id) Then used = openCounts(m.Id)
                Dim openSlots As Integer = Math.Max(0, m.CommissionSlotCapacity - used)
                If m.CommissionSlotCapacity <= 0 Then Continue For
                Dim deposit As Decimal = Math.Round(m.CommissionStartingPrice * 0.2D, 0)
                slots.Add(New CommissionSlotView With {
                    .MerchantId = m.Id,
                    .MerchantName = m.FullName,
                    .MerchantTagline = m.CommissionTagline,
                    .MerchantAvatar = m.AvatarFile,
                    .SampleImage = m.CommissionSampleImage,
                    .StartingPrice = m.CommissionStartingPrice,
                    .Turnaround = m.CommissionTurnaround,
                    .Formats = m.CommissionFormats,
                    .Deposit = deposit,
                    .SlotsOpen = openSlots,
                    .SlotsTotal = m.CommissionSlotCapacity,
                    .CtaText = "Request Slot (Deposit " & Fmt.PHP(deposit) & ")"
                })
            Next
            Return slots
        End Function

        Public Function ListBundles() As List(Of Bundle)
            Return _products.ListBundles()
        End Function

        Public Function ListPromotions() As List(Of Promotion)
            Return _products.ListPromotions()
        End Function

    End Class

End Namespace