Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    Public Class ReportService

        Private ReadOnly _repo As New ReportRepository()

        ' ----- Merchant dashboard aggregates ------------------------------------

        Public Function RevenueTotal() As Decimal
            Return _repo.RevenueTotal()
        End Function

        Public Function RevenueForEvent(eventId As Integer) As Decimal
            Return _repo.RevenueForEvent(eventId)
        End Function

        Public Function EventRevenueTotal() As Decimal
            Return _repo.EventRevenueTotal()
        End Function

        Public Function EventStats() As Dictionary(Of Integer, (Revenue As Decimal, SalesCount As Integer))
            Return _repo.EventStats()
        End Function

        Public Function OrdersCount(Optional status As String = "") As Integer
            Return _repo.OrdersCount(status)
        End Function

        Public Function OrdersForEvent(eventId As Integer) As Integer
            Return _repo.OrdersForEvent(eventId)
        End Function

        Public Function EventSalesCount(eventId As Integer) As Integer
            Return _repo.EventSalesCount(eventId)
        End Function

        Public Function PendingCommissions() As Integer
            Return _repo.PendingCommissions()
        End Function

        Public Function TotalCommissions() As Integer
            Return _repo.TotalCommissions()
        End Function

        Public Function LowStockCount() As Integer
            Return _repo.LowStockCount()
        End Function

        Public Function TotalProducts() As Integer
            Return _repo.TotalProducts()
        End Function

        Public Function TotalCustomers() As Integer
            Return _repo.TotalCustomers()
        End Function

        Public Function TotalCreators() As Integer
            Return _repo.TotalCreators()
        End Function

        Public Function CommissionRevenue() As Decimal
            Return _repo.CommissionRevenue()
        End Function

        Public Function AvgRating() As Decimal
            Return _repo.AvgRating()
        End Function

        Public Function ReviewCount() As Integer
            Return _repo.ReviewCount()
        End Function

        Public Function HotSeller() As (name As String, units As Integer, price As Decimal, stock As Integer)
            Return _repo.HotSeller()
        End Function

        Public Function RevenueLastNDays(days As Integer) As List(Of (day As Date, total As Decimal))
            Return _repo.RevenueLastNDays(days)
        End Function

        ' ----- Report series -----------------------------------------------------

        Public Function DailySales(days As Integer) As List(Of ChartSeries)
            Return _repo.DailySales(days)
        End Function

        Public Function MonthlySales(months As Integer) As List(Of ChartSeries)
            Return _repo.MonthlySales(months)
        End Function

        Public Function ProductSales(limit As Integer) As List(Of ChartSeries)
            Return _repo.ProductSales(limit)
        End Function

        Public Function CategorySales() As List(Of ChartSeries)
            Return _repo.CategorySales()
        End Function

        Public Function EventRevenue() As List(Of ChartSeries)
            Return _repo.EventRevenue()
        End Function

        Public Function PaymentMethodBreakdown() As List(Of ChartSeries)
            Return _repo.PaymentMethodBreakdown()
        End Function

        Public Function OmnichannelMix() As (eventTotal As Decimal, onlineTotal As Decimal)
            Return _repo.OmnichannelMix()
        End Function

        Public Function HourlyTraffic() As List(Of Decimal)
            Return _repo.HourlyTraffic()
        End Function

        Public Function RecentOrders(limit As Integer) As List(Of Order)
            Return _repo.RecentOrders(limit)
        End Function

        Public Function RecentCommissions(limit As Integer) As List(Of Commission)
            Return _repo.RecentCommissions(limit)
        End Function

    End Class

End Namespace