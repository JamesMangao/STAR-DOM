Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Tests.Services

    <TestClass>
    Public Class EventServiceTests

        Private _svc As EventService

        <TestInitialize>
        Public Sub Setup()
            _svc = New EventService()
        End Sub

        <TestMethod>
        Public Sub ListLocations_ReturnsList()
            Dim result As List(Of StoreLocation) = _svc.ListLocations()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub ListEvents_ReturnsList()
            Dim result As List(Of PopUpEvent) = _svc.ListEvents()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub ListUpcoming_ReturnsList()
            Dim result As List(Of PopUpEvent) = _svc.ListUpcoming()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub CurrentEvent_ReturnsEventOrNull()
            Dim result As PopUpEvent = _svc.CurrentEvent()
            ' Can be null if no current event
            Assert.IsNotNull(result, "CurrentEvent can be null, that's OK")
        End Sub

        <TestMethod>
        Public Sub GetEvent_WithInvalidId_ReturnsNull()
            Dim result As PopUpEvent = _svc.GetEvent(-1)
            Assert.IsNull(result, "GetEvent with invalid ID should return null")
        End Sub

        <TestMethod>
        Public Sub SaveEvent_WithMissingName_Fails()
            Dim ev As New PopUpEvent() With {
                .Name = "",
                .BoothNumber = "A-1",
                .OpenTime = "10:00 AM",
                .CloseTime = "9:00 PM",
                .StartDate = Date.Today.AddDays(1),
                .EndDate = Date.Today.AddDays(3)
            }
            Dim result As ServiceResult = _svc.SaveEvent(ev)
            Assert.IsFalse(result.Success, "Should fail with missing name")
            Assert.IsTrue(result.Message.Contains("name") OrElse result.Message.Contains("Name"), "Error should mention name")
        End Sub

        <TestMethod>
        Public Sub SaveEvent_WithMissingBooth_Fails()
            Dim ev As New PopUpEvent() With {
                .Name = "Test Event",
                .BoothNumber = "",
                .OpenTime = "10:00 AM",
                .CloseTime = "9:00 PM",
                .StartDate = Date.Today.AddDays(1),
                .EndDate = Date.Today.AddDays(3)
            }
            Dim result As ServiceResult = _svc.SaveEvent(ev)
            Assert.IsFalse(result.Success, "Should fail with missing booth")
            Assert.IsTrue(result.Message.Contains("Booth") OrElse result.Message.Contains("booth"), "Error should mention booth")
        End Sub

        <TestMethod>
        Public Sub SaveEvent_WithInvalidDateRange_Fails()
            Dim ev As New PopUpEvent() With {
                .Name = "Test Event",
                .BoothNumber = "A-1",
                .OpenTime = "10:00 AM",
                .CloseTime = "9:00 PM",
                .StartDate = Date.Today.AddDays(3),
                .EndDate = Date.Today.AddDays(1) ' End before start
            }
            Dim result As ServiceResult = _svc.SaveEvent(ev)
            Assert.IsFalse(result.Success, "Should fail with invalid date range")
        End Sub

        <TestMethod>
        Public Sub SaveLocation_WithMissingName_Fails()
            Dim loc As New StoreLocation() With {
                .Name = "",
                .City = "Manila",
                .Region = "NCR"
            }
            Dim result As ServiceResult = _svc.SaveLocation(loc)
            Assert.IsFalse(result.Success, "Should fail with missing name")
            Assert.IsTrue(result.Message.Contains("name") OrElse result.Message.Contains("Name"), "Error should mention name")
        End Sub

        <TestMethod>
        Public Sub Inventory_ReturnsList()
            Dim result As List(Of EventInventory) = _svc.Inventory(1)
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub AddInventory_WithZeroStock_Fails()
            Dim result As ServiceResult = _svc.AddInventory(1, 1, 0)
            Assert.IsFalse(result.Success, "Should fail with zero stock")
        End Sub

    End Class

End Namespace