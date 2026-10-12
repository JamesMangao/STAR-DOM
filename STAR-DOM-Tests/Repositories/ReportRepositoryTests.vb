Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports STAR_DOM.Database
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Tests.Repositories

    <TestClass>
    Public Class ReportRepositoryTests

        Private _repo As ReportRepository

        <TestInitialize>
        Public Sub Setup()
            _repo = New ReportRepository()
        End Sub

        <TestMethod>
        Public Sub RevenueTotal_ReturnsDecimal()
            Dim result As Decimal = _repo.RevenueTotal()
            Assert.IsTrue(result >= 0D, "RevenueTotal should be non-negative")
        End Sub

        <TestMethod>
        Public Sub OrdersCount_ReturnsInteger()
            Dim result As Integer = _repo.OrdersCount()
            Assert.IsTrue(result >= 0, "OrdersCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub OrdersCount_WithStatus_ReturnsInteger()
            Dim result As Integer = _repo.OrdersCount("PENDING")
            Assert.IsTrue(result >= 0, "OrdersCount with status should be non-negative")
        End Sub

        <TestMethod>
        Public Sub PaidOrdersCount_ReturnsInteger()
            Dim result As Integer = _repo.PaidOrdersCount()
            Assert.IsTrue(result >= 0, "PaidOrdersCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalProducts_ReturnsInteger()
            Dim result As Integer = _repo.TotalProducts()
            Assert.IsTrue(result >= 0, "TotalProducts should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalCategories_ReturnsInteger()
            Dim result As Integer = _repo.TotalCategories()
            Assert.IsTrue(result >= 0, "TotalCategories should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalCustomers_ReturnsInteger()
            Dim result As Integer = _repo.TotalCustomers()
            Assert.IsTrue(result >= 0, "TotalCustomers should be non-negative")
        End Sub

        <TestMethod>
        Public Sub ReviewingCustomers_ReturnsInteger()
            Dim result As Integer = _repo.ReviewingCustomers()
            Assert.IsTrue(result >= 0, "ReviewingCustomers should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalCreators_ReturnsInteger()
            Dim result As Integer = _repo.TotalCreators()
            Assert.IsTrue(result >= 0, "TotalCreators should be non-negative")
        End Sub

        <TestMethod>
        Public Sub PendingCommissions_ReturnsInteger()
            Dim result As Integer = _repo.PendingCommissions()
            Assert.IsTrue(result >= 0, "PendingCommissions should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalCommissions_ReturnsInteger()
            Dim result As Integer = _repo.TotalCommissions()
            Assert.IsTrue(result >= 0, "TotalCommissions should be non-negative")
        End Sub

        <TestMethod>
        Public Sub LowStockCount_ReturnsInteger()
            Dim result As Integer = _repo.LowStockCount()
            Assert.IsTrue(result >= 0, "LowStockCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub AvgRating_ReturnsDecimal()
            Dim result As Decimal = _repo.AvgRating()
            Assert.IsTrue(result >= 0D, "AvgRating should be non-negative")
        End Sub

        <TestMethod>
        Public Sub ReviewCount_ReturnsInteger()
            Dim result As Integer = _repo.ReviewCount()
            Assert.IsTrue(result >= 0, "ReviewCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub CommissionRevenue_ReturnsDecimal()
            Dim result As Decimal = _repo.CommissionRevenue()
            Assert.IsTrue(result >= 0D, "CommissionRevenue should be non-negative")
        End Sub

        <TestMethod>
        Public Sub HotSeller_ReturnsTuple()
            Dim result = _repo.HotSeller()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub RevenueLastNDays_ReturnsList()
            Dim result = _repo.RevenueLastNDays(7)
            Assert.IsNotNull(result)
            Assert.AreEqual(7, result.Count, "Should return exactly 7 days")
        End Sub

        <TestMethod>
        Public Sub DailySales_ReturnsChartSeries()
            Dim result As List(Of ChartSeries) = _repo.DailySales(7)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 7, "Should return at most 7 days")
        End Sub

        <TestMethod>
        Public Sub MonthlySales_ReturnsChartSeries()
            Dim result As List(Of ChartSeries) = _repo.MonthlySales(3)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 3, "Should return at most 3 months")
        End Sub

        <TestMethod>
        Public Sub ProductSales_ReturnsChartSeries()
            Dim result As List(Of ChartSeries) = _repo.ProductSales(5)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 5, "Should return at most 5 products")
        End Sub

        <TestMethod>
        Public Sub CategorySales_ReturnsChartSeries()
            Dim result As List(Of ChartSeries) = _repo.CategorySales()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub PaymentMethodBreakdown_ReturnsChartSeries()
            Dim result As List(Of ChartSeries) = _repo.PaymentMethodBreakdown()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub HourlyTraffic_ReturnsList()
            Dim result As List(Of Decimal) = _repo.HourlyTraffic()
            Assert.IsNotNull(result)
            Assert.AreEqual(14, result.Count, "Should return 14 hours (8-21)")
        End Sub

        <TestMethod>
        Public Sub RecentOrders_ReturnsList()
            Dim result As List(Of Order) = _repo.RecentOrders(3)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 3, "Should return at most 3 orders")
        End Sub

        <TestMethod>
        Public Sub RecentCommissions_ReturnsList()
            Dim result As List(Of Commission) = _repo.RecentCommissions(3)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 3, "Should return at most 3 commissions")
        End Sub

    End Class

End Namespace