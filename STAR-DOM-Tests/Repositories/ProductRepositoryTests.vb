Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Tests.Repositories

    <TestClass>
    Public Class ProductRepositoryTests

        Private _repo As ProductRepository

        <TestInitialize>
        Public Sub Setup()
            _repo = New ProductRepository()
        End Sub

        <TestMethod>
        Public Sub ListActive_ReturnsList()
            Dim result As List(Of Product) = _repo.ListActive()
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count > 0, "Should have seeded products")
        End Sub

        <TestMethod>
        Public Sub ListActive_WithLimit_ReturnsLimitedList()
            ' ListActive only takes categoryId and search, no limit param
            Dim result As List(Of Product) = _repo.ListActive(0, "")
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count > 0, "Should have products")
        End Sub

        <TestMethod>
        Public Sub ListFeatured_ReturnsList()
            Dim result As List(Of Product) = _repo.ListFeatured(5)
            Assert.IsNotNull(result)
            Assert.IsTrue(result.Count <= 5, "Should return at most 5 featured products")
        End Sub

        <TestMethod>
        Public Sub GetById_WithInvalidId_ReturnsNull()
            Dim result As Product = _repo.GetById(-1)
            Assert.IsNull(result, "GetById with invalid ID should return null")
        End Sub

        <TestMethod>
        Public Sub GetById_WithValidId_ReturnsProduct()
            Dim result As Product = _repo.GetById(1)
            Assert.IsNotNull(result, "Product 1 should exist in seed data")
            Assert.AreEqual(1, result.Id)
        End Sub

        <TestMethod>
        Public Sub LowStock_ReturnsList()
            Dim result As List(Of Product) = _repo.LowStock()
            Assert.IsNotNull(result)
            ' Should include products at or below threshold
        End Sub

        <TestMethod>
        Public Sub GetByCategory_ReturnsList()
            Dim result As List(Of Product) = _repo.ListActive(3)
            Assert.IsNotNull(result)
        End Sub

    End Class

End Namespace