Imports System.Web

Namespace STAR_DOM.Web

    ''' <summary>Role-based access guards used at the top of every protected page.</summary>
    Public Module Guard

        Public Sub RequireLogin()
            If Not STAR_DOM.Helpers.Session.IsAuthenticated Then
                Dim r As String = HttpContext.Current.Request.RawUrl
                HttpContext.Current.Response.Redirect("/Login.aspx?r=" & HttpUtility.UrlEncode(r), True)
            End If
        End Sub

        Public Sub RequireCustomer()
            RequireLogin()
            If Not STAR_DOM.Helpers.Session.IsCustomer Then
                HttpContext.Current.Response.Redirect("/App/Marketplace.aspx", True)
            End If
        End Sub

        Public Sub RequireMerchant()
            RequireLogin()
            If Not STAR_DOM.Helpers.Session.CanManageStore Then
                HttpContext.Current.Response.Redirect("/App/Marketplace.aspx", True)
            End If
        End Sub

        Public Sub RequireAdmin()
            RequireLogin()
            If Not STAR_DOM.Helpers.Session.IsAdmin Then
                HttpContext.Current.Response.Redirect("/App/Marketplace.aspx", True)
            End If
        End Sub

        Public Function IsPost() As Boolean
            Return String.Equals(HttpContext.Current.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
        End Function

        ''' <summary>Accepts a same-origin rooted path, otherwise returns the fallback.</summary>
        ''' <remarks>
        ''' A leading "//" starts with "/" but browsers read it as a protocol-relative
        ''' host, so it is rejected explicitly to prevent open redirects.
        ''' </remarks>
        Public Function SafeReturnUrl(raw As String, fallback As String) As String
            Dim r As String = If(raw, "")
            If Not r.StartsWith("/", StringComparison.Ordinal) Then Return fallback
            If r.StartsWith("//", StringComparison.Ordinal) Then Return fallback
            Return r
        End Function

    End Module

End Namespace
