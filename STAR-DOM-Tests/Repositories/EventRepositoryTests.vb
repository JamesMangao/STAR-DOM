Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Tests.Repositories

    <TestClass>
    Public Class EventRepositoryTests

        Private _repo As EventRepository

        <TestInitialize>
        Public Sub Setup()
            _repo = New EventRepository()
        End Sub

        <TestMethod>
        Public Sub ListLocations_ReturnsList()
            Dim result As List(Of StoreLocation) = _repo.ListLocations()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub ListEvents_ReturnsList()
            Dim result As List(Of PopUpEvent) = _repo.ListEvents()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub ListUpcoming_ReturnsList()
            Dim result As List(Of PopUpEvent) = _repo.ListUpcoming()
            Assert.IsNotNull(result)
        End Sub

        <TestMethod>
        Public Sub GetCurrentEvent_ReturnsEventOrNull()
            Dim result As PopUpEvent = _repo.GetCurrentEvent()
            ' Can be null if no current event
            Assert.IsNotNull(result, "GetCurrentEvent can be null, that's OK")
        End Sub

        <TestMethod>
        Public Sub GetEvent_WithInvalidId_ReturnsNull()
            Dim result As PopUpEvent = _repo.GetEvent(-1)
            Assert.IsNull(result, "GetEvent with invalid ID should return null")
        End Sub

        <TestMethod>
        Public Sub GetEvent_WithValidId_ReturnsEvent()
            Dim result As PopUpEvent = _repo.GetEvent(1)
            Assert.IsNotNull(result, "Event 1 should exist in seed data")
            Assert.AreEqual(1, result.Id)
        End Sub

        <TestMethod>
        Public Sub ListInventory_ReturnsList()
            Dim result As List(Of EventInventory) = _repo.ListInventory(1)
            Assert.IsNotNull(result)
        End Sub

    End Class

End Namespace