Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    Public Class SiteMaster
        Inherits MasterPage

        Protected livePillPh As PlaceHolder
        Protected bannerPh As PlaceHolder
        Protected userPh As PlaceHolder
        Protected navLiteral As Literal
        Protected cartCount As Literal
        Protected cartBtn As System.Web.UI.HtmlControls.HtmlAnchor
        Protected notifBtn As System.Web.UI.HtmlControls.HtmlAnchor
        Protected brandLink As System.Web.UI.HtmlControls.HtmlAnchor
        Protected authGate As System.Web.UI.HtmlControls.HtmlGenericControl
        Protected notifCount As Literal
        Protected footerTourLinks As Literal
        Protected toastWrap As System.Web.UI.HtmlControls.HtmlGenericControl

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            If Not IsPostBack Then
                Try
                    RenderShell()
                Catch ex As Exception
                    navLiteral.Text = WebUi.AlertBox("Could not load navigation: " & ex.Message)
                End Try
            End If
            RenderToast()
        End Sub

        ''' <summary>
        ''' Popup toast after add-to-cart. Cart.aspx redirects back with added=1 and
        ''' the fresh cart count; we render a small slide-in card here (master level,
        ''' so it shows over ANY page the add was made from) and let CSS animate it.
        ''' The count badge in the header is already re-rendered server-side with the
        ''' correct number, so the toast needs no data of its own.
        ''' </summary>
        Private Sub RenderToast()
            If toastWrap Is Nothing Then Return
            If Not String.Equals(Request.QueryString("added"), "1", StringComparison.Ordinal) Then Return

            Dim msg As String = Request.QueryString("msg")
            If msg Is Nothing OrElse msg.Length > 120 OrElse Not msg.StartsWith("Added ", StringComparison.Ordinal) Then
                msg = "Added to your cart!"
            End If

            Dim n As Integer = 0
            Integer.TryParse(Request.QueryString("cartN"), n)
            Dim countNote As String = If(n > 0, " · " & n.ToString() & If(n = 1, " item", " items") & " in cart", "")

            Dim sb As New StringBuilder()
            sb.Append("<div class=""toast"" id=""cartToast"" role=""status"" aria-live=""polite"">")
            sb.Append("<span class=""ms"">check_circle</span>")
            sb.Append("<div class=""toast-body""><b>" & WebUi.Esc(msg) & "</b><span class=""sub"">" &
                      countNote.TrimStart(" ·".ToCharArray()) & "</span></div>")
            sb.Append("<a class=""btn ghost sm"" href=""/App/Cart.aspx"">View cart</a>")
            sb.Append("<button type=""button"" class=""toast-x"" aria-label=""Dismiss"" onclick=""this.parentNode.parentNode.classList.add('hide')""><span class=""ms sm"">close</span></button>")
            sb.Append("</div>")
            sb.Append("<script>")
            sb.Append("(function(){var t=document.getElementById('cartToast');if(!t)return;" &
                      "requestAnimationFrame(function(){t.classList.add('show');});" &
                      "setTimeout(function(){t.classList.add('hide');},3800);" &
                      "var u=new URL(window.location.href);u.searchParams.delete('added');" &
                      "u.searchParams.delete('msg');u.searchParams.delete('cartN');" &
                      "window.history.replaceState({},'',u);})();")
            sb.Append("</" & "script>")
            toastWrap.Controls.Add(New LiteralControl(sb.ToString()))
        End Sub

        Private Sub RenderShell()
            RenderFooterLinks()

            If Not STAR_DOM.Helpers.Session.IsAuthenticated Then
                ' Public visitor: the brand leads to the storefront landing page, and
                ' the notifications bell is hidden (it points at an account page).
                If brandLink IsNot Nothing Then brandLink.HRef = "/"
                If notifBtn IsNot Nothing Then notifBtn.Visible = False
                ' The premium sign-in gate only exists for signed-out visitors; the
                ' script in the shell is inert when this markup is absent.
                If authGate IsNot Nothing Then authGate.Visible = True
                ' Viewing the cart needs an account too, so the bag icon gets the
                ' same gate instead of a silent redirect.
                If cartBtn IsNot Nothing Then cartBtn.Attributes("data-auth-gate") = "1"
                userPh.Controls.Add(New LiteralControl(WebUi.BtnHref("/Login.aspx", "Sign In", "ghost")))
                notifCount.Text = ""
                cartCount.Text = ""
                RenderNav(Nothing)
                Return
            End If

            ' Signed in: the brand leads to the shopper/merchant home.
            If brandLink IsNot Nothing Then brandLink.HRef = "/App/Marketplace.aspx"

            ' Header counts
            Dim cartN As Integer = 0
            Dim notifN As Integer = 0
            Try
                If STAR_DOM.Helpers.Session.IsCustomer Then cartN = New CartService().Count()
                notifN = New NotificationService().UnreadCount()
            Catch
            End Try

            cartCount.Text = If(cartN > 0, "<span class=""count"">" & cartN.ToString() & "</span>", "")
            notifCount.Text = If(notifN > 0, "<span class=""count"">" & notifN.ToString() & "</span>", "")

            ' Merchant/admin accounts never place customer orders — hide the cart
            ' button from the header entirely for them.
            If cartBtn IsNot Nothing Then cartBtn.Visible = Not STAR_DOM.Helpers.Session.CanManageStore

            ' Current user chip
            Dim chip As New StringBuilder()
            chip.Append("<div class=""user-chip"">")
            chip.Append("<span class=""avatar"">" & WebUi.Esc(Initials(STAR_DOM.Helpers.Session.DisplayName)) & "</span>")
            chip.Append("<span class=""who""><b>" & WebUi.Esc(STAR_DOM.Helpers.Session.DisplayName) & "</b><i>" &
                        WebUi.Esc(Subtitle()) & "</i></span>")
            chip.Append("<a class=""logout"" data-confirm=""Sign out of your STAR:DOM account?"" data-confirm-ok=""Sign Out"" title=""Sign out"" href=""/Logout.aspx"">" & WebUi.Ic("logout", "sm") & "</a>")
            chip.Append("</div>")
            userPh.Controls.Add(New LiteralControl(chip.ToString()))

            ' Live banner
            Try
                Dim current As Models.PopUpEvent = New EventService().CurrentEvent()
                If current IsNot Nothing Then
                    Dim shellForm As HtmlControls.HtmlForm = TryCast(FindControl("shell"), HtmlControls.HtmlForm)
                    If shellForm IsNot Nothing Then shellForm.Attributes("class") = "has-announce"
                    bannerPh.Controls.Add(New LiteralControl(
                        "<div class=""announce""><span class=""pulse""></span><div><b>LIVE POP-UP TOUR:</b> " &
                        "Visit STAR:DOM @ " & WebUi.Esc(current.Name) & " today! " &
                        WebUi.Esc(current.VenueDetail) & " &bull; " & WebUi.Esc(current.OpenTime) &
                        " – " & WebUi.Esc(current.CloseTime) & "</div>" &
                        WebUi.BtnHref("/App/PopupLocations.aspx", "View Booth Location & Map", "light", "map") & "</div>"))
                    livePillPh.Controls.Add(New LiteralControl(
                        "<span class=""live-pill""><span class=""pulse""></span> NOW OPEN: " &
                        WebUi.Esc(current.Name) & " (" & WebUi.Esc(current.OpenTime) & " – " &
                        WebUi.Esc(current.CloseTime) & ")</span>"))
                End If
            Catch
            End Try

            RenderNav(STAR_DOM.Helpers.Session.CurrentRoleName)
        End Sub

        Private Sub RenderFooterLinks()
            Try
                Dim evService As New EventService()
                Dim current As Models.PopUpEvent = evService.CurrentEvent()
                Dim upcoming As List(Of Models.PopUpEvent) = evService.ListUpcoming()
                Dim sb As New StringBuilder()

                If current IsNot Nothing Then
                    sb.Append("<li><a href=""/App/PopupLocations.aspx?id=" & current.Id.ToString() & """>" &
                              WebUi.Esc(current.Name) & " (Active)</a></li>")
                End If

                Dim shownCount As Integer = 0
                For Each ev As Models.PopUpEvent In upcoming
                    If current IsNot Nothing AndAlso ev.Id = current.Id Then Continue For
                    sb.Append("<li><a href=""/App/PopupLocations.aspx?id=" & ev.Id.ToString() & """>" &
                              WebUi.Esc(ev.Name) & " (Upcoming)</a></li>")
                    shownCount += 1
                    If shownCount >= 4 Then Exit For
                Next

                sb.Append("<li><a href=""/App/PopupLocations.aspx"">View All Pop-up Tour Dates &amp; Map</a></li>")
                footerTourLinks.Text = sb.ToString()
            Catch
                footerTourLinks.Text = "<li><a href=""/App/PopupLocations.aspx"">Tour Schedule &amp; Locations</a></li>"
            End Try
        End Sub

        Private Function Subtitle() As String
            If STAR_DOM.Helpers.Session.IsAdmin Then Return "Administrator"
            Return "Art Lover"
        End Function

        Private Function Initials(name As String) As String
            Dim parts As String() = name.Trim().Split(" "c)
            Dim s As String = ""
            For i As Integer = 0 To Math.Min(parts.Length - 1, 1)
                If parts(i).Length > 0 Then s &= Char.ToUpperInvariant(parts(i)(0))
            Next
            If s = "" Then s = "?"
            Return s
        End Function

        Private Structure NavItem
            Public Title As String
            Public Url As String
            Public Badge As String
            Public BadgeKind As String
            Public Icon As String
            Public Sub New(t As String, u As String, Optional b As String = "", Optional bk As String = "red", Optional i As String = "")
                Title = t : Url = u : Badge = b : BadgeKind = bk : Icon = i
            End Sub
        End Structure

        Private Function IsActive(url As String) As Boolean
            Dim cur As String = Request.AppRelativeCurrentExecutionFilePath.ToLowerInvariant().TrimStart("~"c, "/"c)
            Dim rawCur As String = Request.CurrentExecutionFilePath.ToLowerInvariant().TrimStart("/"c)
            Dim target As String = url.ToLowerInvariant().TrimStart("~"c, "/"c)
            
            If cur = target OrElse rawCur = target Then Return True
            
            Dim targetNoExt As String = target.Replace(".aspx", "")
            If targetNoExt <> "" AndAlso (cur.StartsWith(targetNoExt & "/", StringComparison.Ordinal) OrElse cur = targetNoExt) Then
                Return True
            End If
            
            ' Map specific sub-pages to their parent category nav links
            If target = "app/catalog.aspx" AndAlso (cur = "app/product.aspx" OrElse cur = "app/product") Then
                Return True
            End If
            ' The landing page is the storefront's front door, so Home lights up for it.
            If target = "app/marketplace.aspx" AndAlso (cur = "default.aspx" OrElse cur = "default") Then
                Return True
            End If
            ' The order list is the hub for the whole account area, so it stays lit for
            ' order detail, the rest of the purchase journey (cart and checkout), and the
            ' remaining account pages. Receipt.aspx is not listed: it is a standalone
            ' print page with no sidebar.
            If target = "app/orders.aspx" AndAlso (cur = "app/orderdetail.aspx" OrElse cur = "app/orderdetail" OrElse
               cur = "app/cart.aspx" OrElse cur = "app/cart" OrElse
               cur = "app/checkout.aspx" OrElse cur = "app/checkout" OrElse
               cur = "app/notifications.aspx" OrElse cur = "app/notifications" OrElse
               cur = "app/profile.aspx" OrElse cur = "app/profile") Then
                Return True
            End If
            ' Commission request/detail live behind the Commission Hub in the nav
            If target = "app/commissionhub.aspx" AndAlso
               (cur = "app/commissionrequest.aspx" OrElse cur = "app/commissionrequest" OrElse
                cur = "app/commissiondetail.aspx" OrElse cur = "app/commissiondetail") Then
                Return True
            End If
            ' The merchant editors are sub-pages of their list views, but their names are
            ' singular+edit, so the directory prefix rule above cannot catch them.
            If target = "app/merchant/events.aspx" AndAlso
               (cur = "app/merchant/eventedit.aspx" OrElse cur = "app/merchant/eventedit") Then
                Return True
            End If
            If target = "app/merchant/products.aspx" AndAlso
               (cur = "app/merchant/productedit.aspx" OrElse cur = "app/merchant/productedit") Then
                Return True
            End If
            ' Customers see the Commission Hub in their marketplace nav, but merchant and
            ' admin accounts only get the studio, where the pipeline is the same feature.
            If target = "app/merchant/pipeline.aspx" AndAlso
               (cur = "app/commissionhub.aspx" OrElse cur = "app/commissionhub" OrElse
                cur = "app/commissionrequest.aspx" OrElse cur = "app/commissionrequest" OrElse
                cur = "app/commissiondetail.aspx" OrElse cur = "app/commissiondetail") Then
                Return True
            End If
            
            Return False
        End Function

        Private Sub RenderNav(roleName As String)
            Dim sb As New StringBuilder()

            ' The marketplace is the customer's shopfront. Merchant/admin accounts do
            ' not place orders, so they get straight to the studio — no discovery nav.
            If Not STAR_DOM.Helpers.Session.CanManageStore Then
                sb.Append("<div class=""nav-group"">")
                sb.Append("<div class=""nav-head"">MARKETPLACE<span class=""pill small yellow"">DISCOVERY</span></div>")
                Dim marketplace As NavItem() = {
                    New NavItem("Marketplace (Home)", "/App/Marketplace.aspx", "", "", "storefront"),
                    New NavItem("Pop-up Locations", "/App/PopupLocations.aspx", "LIVE", "red", "pin_drop"),
                    New NavItem("Products Catalog", "/App/Catalog.aspx", "", "", "inventory_2"),
                    New NavItem("Commission Hub", "/App/CommissionHub.aspx", "", "", "brush"),
                    New NavItem("My Orders / Wishlist", "/App/Orders.aspx", "", "", "receipt_long")
                }
                For Each it As NavItem In marketplace
                    sb.Append(NavLink(it))
                Next
                sb.Append("</div>")
            End If

            ' MERCHANT STUDIO is gated on CanManageStore (IsMerchant OrElse IsAdmin) to match
            ' Guard.RequireMerchant() on the pages themselves — gating on IsAdmin alone hid
            ' these links from MERCHANT-role users who can still open the pages by URL.
            If STAR_DOM.Helpers.Session.CanManageStore Then
                sb.Append("<div class=""nav-group"">")
                sb.Append("<div class=""nav-head"">MERCHANT STUDIO<span class=""pill small yellow"">LIVE DOCK</span></div>")
                Dim studio As NavItem() = {
                    New NavItem("Merchant Dashboard", "/App/Merchant/Dashboard.aspx", "LIVE", "red", "space_dashboard"),
                    New NavItem("Event & Booth Manager", "/App/Merchant/Events.aspx", "", "", "event"),
                    New NavItem("Products & Stock", "/App/Merchant/Products.aspx", "", "", "inventory_2"),
                    New NavItem("Commission Pipeline", "/App/Merchant/Pipeline.aspx", "", "", "account_tree"),
                    New NavItem("Event Sales Reports", "/App/Merchant/Reports.aspx", "", "", "bar_chart"),
                    New NavItem("Orders & Payments", "/App/Merchant/Orders.aspx", "", "", "payments"),
                    New NavItem("Reviews", "/App/Merchant/Reviews.aspx", "", "", "reviews")
                }
                For Each it As NavItem In studio
                    sb.Append(NavLink(it))
                Next
                sb.Append("</div>")
            End If

            If STAR_DOM.Helpers.Session.IsAdmin Then
                sb.Append("<div class=""nav-group"">")
                sb.Append("<div class=""nav-head"">SYSTEM<span class=""pill small gray"">ADMIN</span></div>")
                sb.Append(NavLink(New NavItem("Admin Console", "/App/Admin/Users.aspx", "", "", "admin_panel_settings")))
                sb.Append(NavLink(New NavItem("Payment Settings", "/App/Admin/PaymentSettings.aspx", "", "", "qr_code_2")))
                sb.Append("</div>")
            End If



            navLiteral.Text = sb.ToString()
        End Sub

        Private Function NavLink(it As NavItem) As String
            Dim active As String = If(IsActive(it.Url), " class=""active""", "")
            Dim icon As String = If(it.Icon <> "", WebUi.Ic(it.Icon, "sm"), "")
            Dim badge As String = ""
            If it.Badge <> "" Then
                badge = "<span class=""badge nav-badge " & it.BadgeKind & """>" & WebUi.Esc(it.Badge) & "</span>"
            End If
            Return "<a" & active & " href=""" & it.Url & """>" & icon & "<span class=""nav-txt"">" & WebUi.Esc(it.Title) & "</span>" & badge & "</a>"
        End Function

    End Class

End Namespace
