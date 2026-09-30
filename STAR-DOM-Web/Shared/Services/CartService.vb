Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

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