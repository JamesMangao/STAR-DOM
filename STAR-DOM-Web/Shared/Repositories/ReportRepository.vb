Imports STAR_DOM.Database
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models

Namespace STAR_DOM.Repositories

    ''' <summary>All aggregation queries for dashboards and the sales report screens.</summary>
    Public Class ReportRepository

        ' ----- Merchant dashboard KPIs ------------------------------------------

        Public Function RevenueTotal() As Decimal
            Return Db.ScalarDec("SELECT COALESCE(SUM(TotalAmount),0) FROM Orders WHERE PaymentStatus = 'PAID'")
        End Function

        Public Function RevenueForEvent(eventId As Integer) As Decimal
            Return Db.ScalarDec(
                "SELECT COALESCE(SUM(TotalAmount),0) FROM EventSales WHERE EventId = @e", Db.P("@e", eventId))
        End Function

        ''' <summary>Sum of all in-person event sales (single query, replaces per-event loops).</summary>
        Public Function EventRevenueTotal() As Decimal
            Return Db.ScalarDec("SELECT COALESCE(SUM(TotalAmount),0) FROM EventSales")
        End Function

        ''' <summary>Revenue + sales-count per event in one grouped query (kills dashboard N+1).</summary>
        Public Function EventStats() As Dictionary(Of Integer, (Revenue As Decimal, SalesCount As Integer))
            Dim stats As New Dictionary(Of Integer, (Revenue As Decimal, SalesCount As Integer))()
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT EventId, COALESCE(SUM(TotalAmount),0) AS Rev, COUNT(*) AS Cnt FROM EventSales GROUP BY EventId")
            For Each r As DataRow In rows
                Dim id As Integer = RowReader.AsInt(r, "EventId")
                stats(id) = (RowReader.AsDec(r, "Rev"), RowReader.AsInt(r, "Cnt"))
            Next
            Return stats
        End Function

        Public Function OrdersCount(Optional status As String = "") As Integer
            If status.Length = 0 Then Return Db.ScalarInt("SELECT COUNT(*) FROM Orders")
            Return Db.ScalarInt("SELECT COUNT(*) FROM Orders WHERE Status = @s", Db.P("@s", status))
        End Function

        Public Function OrdersForEvent(eventId As Integer) As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Orders WHERE EventId = @e", Db.P("@e", eventId))
        End Function

        Public Function EventSalesCount(eventId As Integer) As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM EventSales WHERE EventId = @e", Db.P("@e", eventId))
        End Function

        Public Function TotalProducts() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Products WHERE IsActive = TRUE")
        End Function

        Public Function TotalCategories() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Categories WHERE IsActive = TRUE")
        End Function

        Public Function TotalCustomers() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Users u JOIN Roles r ON r.Id = u.RoleId WHERE r.Name = 'CUSTOMER'")
        End Function

        ''' <summary>
        ''' Shoppers who actually left an approved review.
        ''' The merchant report prints this as "Customers", sitting directly under
        ''' the review count: the demo has no Orders rows, so reviews are the only
        ''' evidence of who has bought anything, and counting CUSTOMER-role accounts
        ''' instead made the two numbers on the same card contradict each other
        ''' (6 accounts, 1 reviewer). TotalCustomers above stays the raw account
        ''' count for anywhere that really means "registered accounts".
        ''' </summary>
        Public Function ReviewingCustomers() As Integer
            Return Db.ScalarInt("SELECT COUNT(DISTINCT UserId) FROM Reviews WHERE IsApproved = TRUE AND UserId IS NOT NULL")
        End Function

        Public Function TotalCreators() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Users u JOIN Roles r ON r.Id = u.RoleId " &
                                "WHERE (r.Name = 'ADMIN' OR r.Name = 'MERCHANT') AND u.Status = 'ACTIVE'")
        End Function

        Public Function TotalCommissions() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Commissions")
        End Function

        Public Function PendingCommissions() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Commissions WHERE Status IN ('SUBMITTED','PENDING REVIEW')")
        End Function

        Public Function LowStockCount() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Products WHERE IsActive = TRUE AND StockQuantity <= LowStockThreshold")
        End Function

        Public Function AvgRating() As Decimal
            Return Db.ScalarDec("SELECT COALESCE(AVG(RatingAvg),0) FROM Products WHERE RatingCount > 0")
        End Function

        Public Function ReviewCount() As Integer
            Return Db.ScalarInt("SELECT COUNT(*) FROM Reviews WHERE IsApproved = TRUE")
        End Function

        Public Function CommissionRevenue() As Decimal
            Return Db.ScalarDec(
                "SELECT COALESCE(SUM(COALESCE(FinalPrice,0)),0) FROM Commissions WHERE Status IN ('PAID','IN PRODUCTION','REVISION','FINALIZED','COMPLETED')")
        End Function

        Public Function HotSeller() As (name As String, units As Integer, price As Decimal, stock As Integer)
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT Name, SoldCount, BasePrice, SalePrice, StockQuantity FROM Products " &
                "WHERE IsActive = TRUE ORDER BY SoldCount DESC LIMIT 1")
            If rows.Count = 0 Then Return ("", 0, 0D, 0)
            Dim r As DataRow = rows(0)
            Dim saleP As Decimal? = RowReader.AsNullableDec(r, "SalePrice")
            Dim price As Decimal = If(saleP.HasValue AndAlso saleP.Value > 0, saleP.Value, RowReader.AsDec(r, "BasePrice"))
            Return (RowReader.AsStr(r, "Name"), RowReader.AsInt(r, "SoldCount"), price, RowReader.AsInt(r, "StockQuantity"))
        End Function

        Public Function RevenueLastNDays(days As Integer) As List(Of (day As Date, total As Decimal))
            Dim dt As DataTable = Db.Query(
                "SELECT DATE_TRUNC('day', CreatedAt) AS Day, COALESCE(SUM(TotalAmount),0) AS Total " &
                "FROM Orders WHERE PaymentStatus = 'PAID' AND CreatedAt >= DATE_TRUNC('day', NOW()) - make_interval(days => @d) " &
                "GROUP BY DATE_TRUNC('day', CreatedAt) ORDER BY Day", Db.P("@d", days))
            Dim result As New List(Of (d As Date, total As Decimal))()
            For i As Integer = days - 1 To 0 Step -1
                Dim d As Date = Date.Today.AddDays(-i)
                Dim total As Decimal = 0D
                For Each row As DataRow In dt.Rows
                    If RowReader.AsDate(row, "Day").Date = d.Date Then
                        total = RowReader.AsDec(row, "Total")
                        Exit For
                    End If
                Next
                result.Add((d, total))
            Next
            Return result
        End Function

        ' ----- Report screen series ---------------------------------------------

        Public Function DailySales(days As Integer) As List(Of ChartSeries)
            Return RevenueLastNDays(days).Select(Function(x)
                Return New ChartSeries(x.day.ToString("MMM d"), x.total, Helpers.AppColors.Primary)
            End Function).ToList()
        End Function

        Public Function MonthlySales(months As Integer) As List(Of ChartSeries)
            Dim dt As DataTable = Db.Query(
                "SELECT TO_CHAR(CreatedAt, 'YYYY-MM') AS Ym, COALESCE(SUM(TotalAmount),0) AS Total " &
                "FROM Orders WHERE PaymentStatus = 'PAID' AND CreatedAt >= DATE_TRUNC('day', NOW()) - make_interval(months => @m) " &
                "GROUP BY TO_CHAR(CreatedAt, 'YYYY-MM') ORDER BY Ym", Db.P("@m", months))
            Dim result As New List(Of ChartSeries)()
            For i As Integer = months - 1 To 0 Step -1
                Dim ym As String = Date.Today.AddMonths(-i).ToString("yyyy-MM")
                Dim label As String = Date.Today.AddMonths(-i).ToString("MMM")
                Dim total As Decimal = 0D
                For Each row As DataRow In dt.Rows
                    If RowReader.AsStr(row, "Ym") = ym Then
                        total = RowReader.AsDec(row, "Total")
                        Exit For
                    End If
                Next
                result.Add(New ChartSeries(label, total, Helpers.AppColors.Primary))
            Next
            Return result
        End Function

        Public Function ProductSales(limit As Integer) As List(Of ChartSeries)
            Return Db.Rows(
                "SELECT p.Name, SUM(oi.Quantity) AS Qty, SUM(oi.LineTotal) AS Total FROM OrderItems oi " &
                "JOIN Products p ON p.Id = oi.ProductId GROUP BY p.Id, p.Name " &
                "ORDER BY Total DESC LIMIT @l", Db.P("@l", limit)).Select(Function(r)
                Return New ChartSeries(RowReader.AsStr(r, "Name"), RowReader.AsDec(r, "Total"), Helpers.AppColors.Primary)
            End Function).ToList()
        End Function

        Public Function CategorySales() As List(Of ChartSeries)
            Dim colors As Color() = {
                Helpers.AppColors.Primary, Helpers.AppColors.SecondaryContainer,
                Helpers.AppColors.TertiaryContainer, Helpers.AppColors.PrimaryFixedDim,
                Helpers.AppColors.Secondary, Helpers.AppColors.Tertiary,
                Helpers.AppColors.Outline, Helpers.AppColors.PrimaryContainer
            }
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT c.Name, COALESCE(SUM(oi.LineTotal),0) AS Total FROM OrderItems oi " &
                "JOIN Products p ON p.Id = oi.ProductId " &
                "LEFT JOIN Categories c ON c.Id = p.CategoryId " &
                "GROUP BY c.Id, c.Name ORDER BY Total DESC")
            Dim result As New List(Of ChartSeries)()
            Dim i As Integer = 0
            For Each r As DataRow In rows
                result.Add(New ChartSeries(RowReader.AsStr(r, "Name"), RowReader.AsDec(r, "Total"), colors(i Mod colors.Length)))
                i += 1
            Next
            Return result
        End Function

        Public Function EventRevenue() As List(Of ChartSeries)
            Dim colors As Color() = {Helpers.AppColors.Primary, Helpers.AppColors.SecondaryContainer,
                                     Helpers.AppColors.TertiaryContainer, Helpers.AppColors.Outline}
            Dim rows As List(Of DataRow) = Db.Rows(
                "SELECT e.Name, COALESCE(SUM(s.TotalAmount),0) AS Total FROM PopUpEvents e " &
                "LEFT JOIN EventSales s ON s.EventId = e.Id GROUP BY e.Id, e.Name ORDER BY Total DESC")
            Dim result As New List(Of ChartSeries)()
            Dim i As Integer = 0
            For Each r As DataRow In rows
                result.Add(New ChartSeries(RowReader.AsStr(r, "Name"), RowReader.AsDec(r, "Total"), colors(i Mod colors.Length)))
                i += 1
            Next
            Return result
        End Function

        Public Function PaymentMethodBreakdown() As List(Of ChartSeries)
            ' MAYA is still mapped: payments recorded before the GOtyme rename keep
            ' grouping under that key, so they hold the brand colour instead of
            ' falling back to grey. (No comments inside the initializer below — VB
            ' joins those lines and a comment would swallow the rest of it.)
            Dim colors As New Dictionary(Of String, Color)() From {
                {"GCASH", ColorTranslator.FromHtml("#007DFE")},
                {"GOTYME", ColorTranslator.FromHtml("#00A651")},
                {"MAYA", ColorTranslator.FromHtml("#00A651")},
                {"CARD", Helpers.AppColors.TertiaryContainer},
                {"COD", Helpers.AppColors.SecondaryContainer}
            }
            Return Db.Rows(
                "SELECT PaymentMethod, COALESCE(SUM(Amount),0) AS Total FROM Payments WHERE Status = 'PAID' " &
                "GROUP BY PaymentMethod ORDER BY Total DESC").Select(Function(r)
                Dim m As String = RowReader.AsStr(r, "PaymentMethod").ToUpperInvariant()
                Dim c As Color = If(colors.ContainsKey(m), colors(m), Helpers.AppColors.Outline)
                Return New ChartSeries(m, RowReader.AsDec(r, "Total"), c)
            End Function).ToList()
        End Function

        Public Function OmnichannelMix() As (eventTotal As Decimal, onlineTotal As Decimal)
            Dim evt As Decimal = Db.ScalarDec("SELECT COALESCE(SUM(TotalAmount),0) FROM EventSales")
            Dim online As Decimal = Db.ScalarDec(
                "SELECT COALESCE(SUM(TotalAmount),0) FROM Orders WHERE PaymentStatus = 'PAID' " &
                "AND EventId IS NULL")
            Return (evt, online)
        End Function

        Public Function HourlyTraffic() As List(Of Decimal)
            Dim dt As DataTable = Db.Query(
                "SELECT EXTRACT(HOUR FROM CreatedAt) AS Hr, COUNT(*) AS Cnt FROM Orders " &
                "WHERE CreatedAt >= NOW() - INTERVAL '72 hours' GROUP BY EXTRACT(HOUR FROM CreatedAt)")
            Dim result As New List(Of Decimal)()
            For h As Integer = 8 To 21
                Dim cnt As Decimal = 0D
                For Each row As DataRow In dt.Rows
                    If RowReader.AsInt(row, "Hr") = h Then
                        cnt = RowReader.AsDec(row, "Cnt")
                        Exit For
                    End If
                Next
                result.Add(cnt)
            Next
            Return result
        End Function

        Public Function EventInventorySnapshot(eventId As Integer) As List(Of EventInventory)
            Dim repo As New EventRepository()
            Return repo.ListInventory(eventId)
        End Function

        Public Function RecentOrders(Optional limit As Integer = 6) As List(Of Order)
            Dim repo As New OrderRepository()
            Return repo.ListRecent(limit)
        End Function

        Public Function RecentCommissions(Optional limit As Integer = 5) As List(Of Commission)
            Return (New CommissionRepository()).ListRecent(limit)
        End Function

    End Class

End Namespace