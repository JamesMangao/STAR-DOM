Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports STAR_DOM.Helpers

Namespace STAR_DOM.Tests.Helpers

    <TestClass>
    Public Class FmtTests

        <TestMethod>
        Public Sub PHP_WithDecimal_ReturnsFormattedString()
            Dim result As String = Fmt.PHP(1234.56D)
            Assert.IsTrue(result.StartsWith("₱"), "Should start with Peso sign")
            Assert.IsTrue(result.Contains("1,234.56"), "Should format with thousand separator")
        End Sub

        <TestMethod>
        Public Sub PHP_WithZero_ReturnsZero()
            Dim result As String = Fmt.PHP(0D)
            Assert.AreEqual("₱0.00", result)
        End Sub

        <TestMethod>
        Public Sub PHP_WithNothing_ReturnsZero()
            Dim result As String = Fmt.PHP(Nothing)
            Assert.AreEqual("₱0.00", result)
        End Sub

        <TestMethod>
        Public Sub Num_WithInteger_ReturnsFormattedString()
            Dim result As String = Fmt.Num(1234)
            Assert.AreEqual("1,234", result)
        End Sub

        <TestMethod>
        Public Sub Pct_WithDecimal_ReturnsPercentage()
            ' Default digits=0 rounds to whole percent
            Dim result As String = Fmt.Pct(0.1234D)
            Assert.AreEqual("12%", result)
        End Sub

        <TestMethod>
        Public Sub Pct_WithDigits_ReturnsPercentage()
            Dim result As String = Fmt.Pct(0.1234D, 1)
            Assert.AreEqual("12.3%", result)
        End Sub

        <TestMethod>
        Public Sub DateF_WithDate_ReturnsFormattedString()
            Dim result As String = Fmt.DateF(New Date(2026, 10, 12))
            Assert.IsTrue(result.Contains("Oct") OrElse result.Contains("12") OrElse result.Contains("2026"))
        End Sub

        <TestMethod>
        Public Sub DateF_WithNothing_ReturnsDash()
            Dim result As String = Fmt.DateF(Nothing)
            Assert.AreEqual("—", result)
        End Sub

        <TestMethod>
        Public Sub EventWindow_SameYear_ReturnsShortFormat()
            Dim result As String = Fmt.EventWindow(New Date(2026, 10, 10), New Date(2026, 10, 15))
            Assert.IsTrue(result.Contains("Oct 10"))
            Assert.IsTrue(result.Contains("15, 2026"))
        End Sub

        <TestMethod>
        Public Sub EventWindow_DifferentYear_ReturnsLongFormat()
            Dim result As String = Fmt.EventWindow(New Date(2025, 12, 20), New Date(2026, 1, 5))
            Assert.IsTrue(result.Contains("2025"))
            Assert.IsTrue(result.Contains("2026"))
        End Sub

        <TestMethod>
        Public Sub DaysToGo_Future_ReturnsDays()
            Dim result As String = Fmt.DaysToGo(Date.Today.AddDays(5))
            Assert.AreEqual("5 DAYS TO GO", result)
        End Sub

        <TestMethod>
        Public Sub DaysToGo_Tomorrow_ReturnsDay()
            Dim result As String = Fmt.DaysToGo(Date.Today.AddDays(1))
            Assert.AreEqual("1 DAY TO GO", result)
        End Sub

        <TestMethod>
        Public Sub DaysToGo_Today_ReturnsToday()
            Dim result As String = Fmt.DaysToGo(Date.Today)
            Assert.AreEqual("TODAY", result)
        End Sub

        <TestMethod>
        Public Sub DaysToGo_Past_ReturnsToday()
            Dim result As String = Fmt.DaysToGo(Date.Today.AddDays(-1))
            Assert.AreEqual("TODAY", result)
        End Sub

        <TestMethod>
        Public Sub Truncate_ShortString_ReturnsOriginal()
            Dim result As String = Fmt.Truncate("Hello", 10)
            Assert.AreEqual("Hello", result)
        End Sub

        <TestMethod>
        Public Sub Truncate_LongString_ReturnsTruncated()
            Dim result As String = Fmt.Truncate("Hello World", 8)
            Assert.AreEqual("Hello...", result)
        End Sub

        <TestMethod>
        Public Sub Truncate_Null_ReturnsEmpty()
            Dim result As String = Fmt.Truncate(Nothing, 10)
            Assert.AreEqual("", result)
        End Sub

        <TestMethod>
        Public Sub Initials_SingleName_ReturnsFirstLetter()
            Dim result As String = Fmt.Initials("Bella")
            Assert.AreEqual("B", result)
        End Sub

        <TestMethod>
        Public Sub Initials_TwoNames_ReturnsBothInitials()
            Dim result As String = Fmt.Initials("Bella Santos")
            Assert.AreEqual("BS", result)
        End Sub

        <TestMethod>
        Public Sub Initials_ThreeNames_ReturnsFirstAndLast()
            Dim result As String = Fmt.Initials("Juan Miguel Santos")
            Assert.AreEqual("JS", result)
        End Sub

        <TestMethod>
        Public Sub Initials_Empty_ReturnsBullet()
            Dim result As String = Fmt.Initials("")
            Assert.AreEqual("•.", result)
        End Sub

    End Class

End Namespace