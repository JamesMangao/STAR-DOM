Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports STAR_DOM.Database
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories
Imports STAR_DOM.Services

Namespace STAR_DOM.Tests.Services

    <TestClass>
    Public Class ReportServiceTests

        Private _repo As ReportRepository
        Private _svc As ReportService

        <TestInitialize>
        Public Sub Setup()
            _repo = New ReportRepository()
            _svc = New ReportService()
        End Sub

        <TestMethod>
        Public Sub RevenueTotal_ReturnsDecimal()
            Dim result As Decimal = _svc.RevenueTotal()
            Assert.IsTrue(result >= 0D, "RevenueTotal should be non-negative")
        End Sub

        <TestMethod>
        Public Sub OrdersCount_ReturnsInteger()
            Dim result As Integer = _svc.OrdersCount()
            Assert.IsTrue(result >= 0, "OrdersCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub OrdersCount_WithStatus_ReturnsInteger()
            Dim result As Integer = _svc.OrdersCount("PENDING")
            Assert.IsTrue(result >= 0, "OrdersCount with status should be non-negative")
        End Sub

        <TestMethod>
        Public Sub PaidOrdersCount_ReturnsInteger()
            Dim result As Integer = _svc.PaidOrdersCount()
            Assert.IsTrue(result >= 0, "PaidOrdersCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub PendingCommissions_ReturnsInteger()
            Dim result As Integer = _svc.PendingCommissions()
            Assert.IsTrue(result >= 0, "PendingCommissions should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalCommissions_ReturnsInteger()
            Dim result As Integer = _svc.TotalCommissions()
            Assert.IsTrue(result >= 0, "TotalCommissions should be non-negative")
        End Sub

        <TestMethod>
        Public Sub CommissionRevenue_ReturnsDecimal()
            Dim result As Decimal = _svc.CommissionRevenue()
            Assert.IsTrue(result >= 0D, "CommissionRevenue should be non-negative")
        End Sub

        <TestMethod>
        Public Sub LowStockCount_ReturnsInteger()
            Dim result As Integer = _svc.LowStockCount()
            Assert.IsTrue(result >= 0, "LowStockCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalProducts_ReturnsInteger()
            Dim result As Integer = _svc.TotalProducts()
            Assert.IsTrue(result >= 0, "TotalProducts should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalCustomers_ReturnsInteger()
            Dim result As Integer = _svc.TotalCustomers()
            Assert.IsTrue(result >= 0, "TotalCustomers should be non-negative")
        End Sub

        <TestMethod>
        Public Sub TotalCreators_ReturnsInteger()
            Dim result As Integer = _svc.TotalCreators()
            Assert.IsTrue(result >= 0, "TotalCreators should be non-negative")
        End Sub

        <TestMethod>
        Public Sub AvgRating_ReturnsDecimal()
            Dim result As Decimal = _svc.AvgRating()
            Assert.IsTrue(result >= 0D, "AvgRating should be non-negative")
        End Sub

        <TestMethod>
        Public Sub ReviewCount_ReturnsInteger()
            Dim result As Integer = _svc.ReviewCount()
            Assert.IsTrue(result >= 0, "ReviewCount should be non-negative")
        End Sub

        <TestMethod>
        Public Sub HotSeller_ReturnsTuple()
            Dim result = _svc.HotSeller()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub DailySales_ReturnsList()
            Dim result As List(Of ChartSeries) = _svc.DailySales(7)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 7, "Should return at most 7 days")
        End Sub

        <TestMethod>
        Public Sub MonthlySales_ReturnsList()
            Dim result As List(Of ChartSeries) = _svc.MonthlySales(3)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 3, "Should return at most 3 months")
        End Sub

        <TestMethod>
        Public Sub ProductSales_ReturnsList()
            Dim result As List(Of ChartSeries) = _svc.ProductSales(5)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 5, "Should return at most 5 products")
        End Sub

        <TestMethod>
        Public Sub CategorySales_ReturnsList()
            Dim result As List(Of ChartSeries) = _svc.CategorySales()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub PaymentMethodBreakdown_ReturnsList()
            Dim result As List(Of ChartSeries) = _svc.PaymentMethodBreakdown()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub HourlyTraffic_ReturnsList()
            Dim result As List(Of Decimal) = _svc.HourlyTraffic()
            Assert.IsNotNull(result)
            Assert.AreEqual(14, result.Count, "Should return 14 hours (8-21)")
        End Sub

        <TestMethod>
        Public Sub RecentOrders_ReturnsList()
            Dim result As List(Of Order) = _svc.RecentOrders(3)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 3, "Should return at most 3 orders")
        End Sub

        <TestMethod>
        Public Sub RecentCommissions_ReturnsList()
            Dim result As List(Of Commission) = _svc.RecentCommissions(3)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 3, "Should return at most 3 commissions")
        End Sub

    End Class

End Namespace