Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories
Imports System.Linq

Namespace STAR_DOM.Services

    Public Class CartService

        Private ReadOnly _cart As New CartRepository()
        Private ReadOnly _products As New ProductRepository()

        Public Function Count() As Integer
            If Not Session.IsAuthenticated Then Return 0
            Return _cart.GetCartCount(Session.CurrentUser.Id)
        End Function

        Public Function ListItems() As List(Of CartItem)
            If Not Session.IsAuthenticated Then Return New List(Of CartItem)()
            Return _cart.ListItems(Session.CurrentUser.Id)
        End Function

        Public Function Add(productId As Integer, Optional quantity As Integer = 1) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            If quantity < 1 Then Return ServiceResult.Fail("Quantity must be at least 1.")
            Dim err As String = _cart.AddItem(Session.CurrentUser.Id, productId, Nothing, quantity)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            Return ServiceResult.Ok("Added to cart.")
        End Function

        Public Function UpdateQuantity(cartItemId As Integer, quantity As Integer) As ServiceResult
            If Not Session.IsAuthenticated Then Return ServiceResult.Fail("Please log in first.")
            Dim err As String = _cart.UpdateQuantity(Session.CurrentUser.Id, cartItemId, quantity)
            If err IsNot Nothing Then Return ServiceResult.Fail(err)
            Return ServiceResult.Ok("Cart updated.")
        End Function

        Public Sub Remove(cartItemId As Integer)
            If Session.IsAuthenticated Then _cart.RemoveItem(Session.CurrentUser.Id, cartItemId)
        End Sub

        Public Sub Clear()
            If Session.IsAuthenticated Then _cart.ClearCart(Session.CurrentUser.Id)
        End Sub

        Public Function Subtotal() As Decimal
            Return ListItems().Sum(Function(i) i.LineTotal)
        End Function

        ' ----- Bundles ------------------------------------------------------------

        ''' <summary>
        ''' Total savings from complete "N for ₱M" bundle groups in the cart, e.g. any
        ''' 4 stickers ring up at ₱100 instead of 4 × ₱30. Leftover units outside a
        ''' full group stay at their regular price. Each complete group saves the gap
        ''' between the members' cart value and the bundle price (sale prices honoured).
        ''' </summary>
        Public Function BundleDiscount(items As List(Of CartItem)) As Decimal
            If items Is Nothing OrElse items.Count = 0 Then Return 0D
            Dim total As Decimal = 0D
            For Each g As BundleGroup In _cart.ListBundleGroups()
                Dim lines As List(Of CartItem) = items.Where(Function(i) g.ProductIds.Contains(i.ProductId)).ToList()
                If lines.Count = 0 Then Continue For
                Dim qty As Integer = lines.Sum(Function(i) i.Quantity)
                Dim groups As Integer = qty \ g.GroupSize
                If groups < 1 Then Continue For
                Dim unit As Decimal = lines.Sum(Function(i) i.LineTotal) / qty
                Dim per As Decimal = (g.GroupSize * unit) - g.GroupPrice
                If per > 0D Then total += groups * per
            Next
            Return Decimal.Round(total, 2)
        End Function

        ''' <summary>
        ''' Human label of the first active bundle containing the product, e.g.
        ''' "any 4 for ₱100" — shown on the product page so shoppers see the deal
        ''' before adding to cart. Empty when the product is not in an active bundle.
        ''' </summary>
        Public Function BundleLabelForProduct(productId As Integer) As String
            For Each g As BundleGroup In _cart.ListBundleGroups()
                If g.ProductIds.Contains(productId) Then
                    Return "any " & g.GroupSize.ToString() & " for " & Fmt.PHP(g.GroupPrice)
                End If
            Next
            Return ""
        End Function

        ''' <summary>
        ''' Shopper-facing bundle legend, e.g. "Stickers Bundle — 4 for ₱100 ·
        ''' Button pins Bundle — 3 for ₱100". Empty when no deal-style bundle is active.
        ''' </summary>
        Public Function BundleNote() As String
            Dim parts As New List(Of String)()
            For Each g As BundleGroup In _cart.ListBundleGroups()
                Dim label As String = System.Text.RegularExpressions.Regex.Replace(g.Name, "\s*\(.*\)\s*$", "").Trim()
                If label = "" Then label = g.Name
                parts.Add(label & " — " & g.GroupSize.ToString() & " for " & Fmt.PHP(g.GroupPrice))
            Next
            Return String.Join(" · ", parts)
        End Function

        ' ----- Wishlist ----------------------------------------------------------

        Public Function ListWishlist() As List(Of WishlistItem)
            If Not Session.IsAuthenticated Then Return New List(Of WishlistItem)()
            Return _cart.ListWishlist(Session.CurrentUser.Id)
        End Function

        Public Function ToggleWishlist(productId As Integer) As Boolean
            If Not Session.IsAuthenticated Then Return False
            If _cart.WishlistHas(Session.CurrentUser.Id, productId) Then
                _cart.RemoveWishlist(Session.CurrentUser.Id, productId)
                Return False
            Else
                _cart.AddWishlist(Session.CurrentUser.Id, productId)
                Return True
            End If
        End Function

        Public Function InWishlist(productId As Integer) As Boolean
            If Not Session.IsAuthenticated Then Return False
            Return _cart.WishlistHas(Session.CurrentUser.Id, productId)
        End Function

        Public Sub RemoveWishlist(productId As Integer)
            If Session.IsAuthenticated Then _cart.RemoveWishlist(Session.CurrentUser.Id, productId)
        End Sub

        Public Sub MoveToCart(productId As Integer)
            If Session.IsAuthenticated Then _cart.MoveWishlistToCart(Session.CurrentUser.Id, productId)
        End Sub

    End Class

End Namespace