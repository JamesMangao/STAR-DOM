Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports STAR_DOM.Helpers
Imports STAR_DOM.Models
Imports STAR_DOM.Services

Namespace STAR_DOM.Web

    ''' <summary>
    ''' Public storefront landing page. Anonymous visitors may browse the catalog,
    ''' view products and read this page without signing in; the cart, checkout and
    ''' every account page stay behind Guard.RequireLogin().
    ''' </summary>
    Public Class DefaultPage
        Inherits Page

        Protected Out As Literal

        Private ReadOnly _catalog As New CatalogService()
        Private ReadOnly _events As New EventService()

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Try
                RenderPage()
            Catch ex As Exception
                STAR_DOM.Database.Db.LogError("Home", ex)
                Out.Text = WebUi.AlertBox("Could not load the home page: " & ex.Message)
            End Try
        End Sub

        Private Sub RenderPage()
            Dim sb As New StringBuilder()
            Dim featured As List(Of Product) = _catalog.Featured(8)
            Dim categories As List(Of Category) = _catalog.ListCategories()
            Dim current As PopUpEvent = _events.CurrentEvent()
            Dim slots As List(Of CommissionSlotView) = _catalog.CommissionSlots()

            sb.Append(Hero(featured))
            sb.Append(StatStrip(categories.Count))
            sb.Append(HowItWorks())
            sb.Append(CategoryTiles(categories))
            sb.Append(FeaturedProducts(featured))
            If current IsNot Nothing Then sb.Append(CurrentBooth(current))
            If slots.Count > 0 Then sb.Append(CommissionTeaser(slots))
            sb.Append(PaymentPanel())
            sb.Append(FinalCta())

            Out.Text = sb.ToString()
        End Sub

        ' ---------- hero ----------

        Private Function Hero(featured As List(Of Product)) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""hero"">")
            sb.Append("<div class=""row space-between"" style=""align-items:stretch;gap:26px"">")
            sb.Append("<div style=""flex:1.4;min-width:320px"">")
            sb.Append(WebUi.Pill("FILIPINO ARTISAN BRAND &bull; MADE BY HAND", "yellow"))
            sb.Append("<h1 style=""font-size:42px;line-height:1.1;margin:12px 0 10px;letter-spacing:-1.2px"">" &
                      "Original art, crafted by hand and " &
                      "<span class=""grad-text"">delivered to your door.</span></h1>")
            sb.Append("<p class=""sub"" style=""font-size:16px;max-width:560px"">Stickers, prints, pins and limited merch " &
                      "from one solo artist. Order online for nationwide J&amp;T Express delivery, " &
                      "nationwide.</p>")
            sb.Append("<div class=""frow"">")
            sb.Append(WebUi.BtnHref("/App/Catalog.aspx", "Explore the Catalog"))
            sb.Append(WebUi.BtnHref("/Register.aspx", "Create Free Account", "secondary", "person_add"))
            sb.Append(WebUi.BtnHref("/App/CommissionHub.aspx", "Request a Commission", "ghost", "draw"))
            sb.Append("</div>")
            sb.Append("<div class=""sub"" style=""font-size:12.5px;margin-top:2px"">" & WebUi.Ic("lock", "sm") &
                      " No account needed to browse &mdash; sign in only when you check out.</div>")
            sb.Append("</div>")

            ' collage from the first two featured products
            sb.Append("<div style=""flex:1;min-width:280px;max-width:430px"">")
            If featured.Count >= 2 Then sb.Append(Spotlight(featured(0), featured(1)))
            sb.Append("</div></div></div>")
            Return sb.ToString()
        End Function

        Private Function Spotlight(a As Product, b As Product) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card"" style=""overflow:hidden;padding:0;position:relative"">")
            sb.Append("<div class=""badge"" style=""background:var(--primary);color:#fff;position:absolute;margin:10px;z-index:2"">FEATURED</div>")
            sb.Append(WebUi.ProductImg(a.PrimaryImageFile, a.Id, a.Name, "height:210px"))
            sb.Append("<div style=""padding:12px 14px;display:flex;justify-content:space-between;align-items:center;gap:8px"">" &
                      "<b>" & WebUi.Esc(a.Name) & "</b> <span style=""color:var(--primary);font-weight:800"">" &
                      WebUi.Money(a.BasePrice) & "</span></div>")
            sb.Append("</div>")
            sb.Append("<div class=""card"" style=""padding:0;overflow:hidden;margin-top:12px"">" & WebUi.ProductImg(b.PrimaryImageFile, b.Id, b.Name, "height:150px") &
                      "<div style=""padding:8px 10px;font-size:12px""><b>" & WebUi.Esc(b.Name) & "</b><br>" &
                      WebUi.Money(b.BasePrice) & "</div></div>")
            Return sb.ToString()
        End Function

        ' ---------- live numbers ----------

        Private Function StatStrip(categoryCount As Integer) As String
            Dim rep As New ReportService()
            Dim productCount As Integer = _catalog.ListProducts().Count
            ' RevenueTotal already spans paid orders and event takings, so adding
            ' EventRevenueTotal here would count every booth sale twice.
            Dim sales As Decimal = rep.RevenueTotal()
            Dim vol As String = If(sales >= 1000D, "₱" & (sales / 1000D).ToString("0.#") & "K+", "₱" & sales.ToString("N0"))

            Dim sb As New StringBuilder()
            sb.Append("<div class=""grid kpis"" style=""grid-template-columns:repeat(auto-fit,minmax(150px,1fr));margin:24px 0 0"">")
            sb.Append(Kpi("inventory_2", productCount.ToString(), "Handcrafted SKUs", "var(--yellow-soft)", "var(--on-yellow)"))
            sb.Append(Kpi("palette", categoryCount.ToString(), "Art Categories", "#ffe0de", "var(--primary)"))
            sb.Append(Kpi("paid", vol, "Bazaar Sales Volume", "var(--tertiary-fixed)", "var(--tertiary)"))
            sb.Append(Kpi("local_shipping", "1 way", "Nationwide J&T delivery", "#e5f6e7", "var(--green)"))
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function Kpi(icon As String, value As String, label As String, bg As String, fg As String) As String
            Return "<div class=""kpi k-icon""><span class=""k-ic"" style=""background:" & bg & ";color:" & fg & """>" &
                   WebUi.Ic(icon) & "</span><div class=""k-value"" style=""font-size:26px"">" & WebUi.Esc(value) &
                   "</div><div class=""k-label"">" & WebUi.Esc(label) & "</div></div>"
        End Function

        ' ---------- how it works ----------

        Private Function HowItWorks() As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
            sb.Append("<div><div class=""eyebrow"">HOW ORDERING WORKS</div><h2>From Browsing to Your Doorstep</h2></div>")
            sb.Append("<div class=""sub"">Three steps &mdash; browse freely, sign in to check out</div>")
            sb.Append("</div>")
            sb.Append("<div class=""grid"" style=""grid-template-columns:repeat(auto-fit,minmax(240px,1fr))"">")
            sb.Append(StepCard("1", "search", "Browse the catalog",
                               "Stickers, prints, pins and limited merch, every piece made by the artist. Look around with no sign-up."))
            sb.Append(StepCard("2", "local_shipping", "Enter your delivery address",
                               "Nationwide J&amp;T Express: Luzon ₱0&ndash;100, Visayas &amp; Mindanao ₱100&ndash;250. Every order ships to your door."))
            sb.Append(StepCard("3", "account_balance_wallet", "Pay your way",
                               "GCash or GOtyme. Sign in once and your cart carries straight through to checkout."))
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function StepCard(n As String, icon As String, title As String, body As String) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card"" style=""display:flex;flex-direction:column;gap:8px"">")
            sb.Append("<div style=""display:flex;align-items:center;gap:10px"">")
            sb.Append("<span style=""display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;" &
                      "border-radius:11px;background:var(--primary);color:#fff;font-family:var(--font-display);font-weight:800"">" &
                      WebUi.Esc(n) & "</span>")
            sb.Append("<span style=""color:var(--primary)"">" & WebUi.Ic(icon) & "</span>")
            sb.Append("</div>")
            sb.Append("<b style=""font-family:var(--font-display);font-size:16px"">" & title & "</b>")
            sb.Append("<span class=""sub"">" & body & "</span>")
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ' ---------- categories ----------

        Private Function CategoryTiles(categories As List(Of Category)) As String
            If categories.Count = 0 Then Return ""
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
            sb.Append("<div><div class=""eyebrow"">SHOP BY CATEGORY</div><h2>Find Your Next Piece</h2></div>")
            sb.Append("<div class=""sub"">" & categories.Count.ToString() & " categories</div>")
            sb.Append("</div>")
            sb.Append("<div class=""grid cards4"">")
            For Each c As Category In categories
                sb.Append("<a class=""card"" href=""/App/Catalog.aspx?cat=" & c.Id.ToString() & """" &
                          " style=""text-decoration:none;color:inherit;display:flex;align-items:center;gap:12px"">")
                sb.Append("<span style=""display:inline-flex;align-items:center;justify-content:center;width:40px;height:40px;flex:0 0 40px;" &
                          "border-radius:12px;background:var(--surface-hi);color:var(--primary)"">" & WebUi.Ic("palette") & "</span>")
                sb.Append("<span style=""min-width:0""><b style=""font-family:var(--font-display);display:block"">" & WebUi.Esc(c.Name) & "</b>")
                sb.Append("<span class=""sub"" style=""font-size:12px"">Browse category</span></span>")
                sb.Append("</a>")
            Next
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ' ---------- featured products ----------

        Private Function FeaturedProducts(featured As List(Of Product)) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
            sb.Append("<div><div class=""eyebrow"">FRESH FROM THE STUDIO</div><h2>Featured Pieces</h2></div>")
            sb.Append("<div class=""sub"">" & featured.Count.ToString() & " picks from the catalog</div>")
            sb.Append("</div>")

            ' Same bundle legend the signed-in catalog shows. Anonymous visitors get
            ' it too: CartService.BundleNote only reads the Bundles tables, so it
            ' needs no session, and the strip renders empty when nothing is active.
            sb.Append(WebUi.BundleNoteStrip())

            If featured.Count = 0 Then
                sb.Append(WebUi.EmptyRow("No products available yet - check back soon."))
                Return sb.ToString()
            End If

            sb.Append("<div class=""grid cards4"">")
            For Each p As Product In featured
                sb.Append(ProductCard(p))
            Next
            sb.Append("</div>")
            sb.Append("<div style=""text-align:center;margin:18px 0 6px"">")
            sb.Append(WebUi.BtnHref("/App/Catalog.aspx", "Browse the Full Catalog", "secondary"))
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function ProductCard(p As Product) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""pcard"">")
            sb.Append("<div class=""artwrap"" style=""position:relative"">")
            sb.Append(WebUi.ProductImg(p.PrimaryImageFile, p.Id, p.Name, "height:185px"))
            sb.Append("<div class=""badges"">")
            sb.Append(If(p.BadgeLabel <> "", "<span class=""badge warn"">" & WebUi.Esc(p.BadgeLabel) & "</span>", ""))
            sb.Append("</div></div>")
            sb.Append("<div class=""pbody"">")
            sb.Append("<span class=""brand"">" & WebUi.Esc(p.BrandName) & "</span>")
            sb.Append("<a class=""pname"" href=""/App/Product.aspx?id=" & p.Id.ToString() & """ style=""color:inherit"">" & WebUi.Esc(p.Name) & "</a>")
            sb.Append("<span class=""pdesc"">" & WebUi.Esc(p.MaterialDetails) & "</span>")
            If p.RatingCount > 0 Then
                sb.Append("<span class=""stars"">" & WebUi.Stars(CInt(Math.Round(p.RatingAvg))) &
                          " <small style=""color:var(--ink-soft)"">(" & p.RatingCount.ToString() & ")</small></span>")
            End If
            sb.Append("<div class=""pfoot"">")
            sb.Append(WebUi.Money(p.BasePrice))
            ' Anonymous shoppers who press Add are bounced to Login with this URL as
            ' the return target, so the add completes the moment they sign in. The
            ' gate attributes let the premium popup explain that instead of just
            ' redirecting out from under them.
            sb.Append(WebUi.AddCartButton(p, "/"))
            sb.Append("</div>")
            sb.Append(If(p.StockQuantity <= p.LowStockThreshold, "<span class=""stockline"" style=""color:var(--primary)"">Only " &
                        p.StockQuantity.ToString() & " remaining!</span>", "<span class=""stockline"">" &
                        p.StockQuantity.ToString() & " in booth stock</span>"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

        ' ---------- current booth ----------

        Private Function CurrentBooth(ev As PopUpEvent) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
            sb.Append("<div><div class=""eyebrow"">FIND US IN PERSON</div><h2>Pop-up Booth Today</h2></div>")
            sb.Append("<div class=""sub"">Walk-ups welcome &mdash; come see the work in person</div>")
            sb.Append("</div>")
            sb.Append("<div class=""card"" style=""display:flex;align-items:center;gap:18px;flex-wrap:wrap;border-color:#eec200"">")
            sb.Append("<span class=""pulse""></span>")
            sb.Append("<div style=""flex:1;min-width:240px"">")
            sb.Append("<div class=""eyebrow"" style=""display:block"">" & WebUi.Ic("pin_drop", "sm") & " CURRENT PHYSICAL LOCATION</div>")
            sb.Append("<b style=""font-size:19px;display:block"">STAR:DOM @ " & WebUi.Esc(ev.Name) & "</b>")
            sb.Append("<div class=""sub"">" & WebUi.Esc(ev.VenueDetail) & " &middot; " & WebUi.Esc(ev.HoursText) & " &middot; Booth " &
                      WebUi.Esc(ev.BoothNumber) & "</div>")
            sb.Append("</div>")
            sb.Append(WebUi.Badge("NOW OPEN"))
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ' ---------- commissions ----------

        Private Function CommissionTeaser(slots As List(Of CommissionSlotView)) As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
            sb.Append("<div><div class=""eyebrow"">CUSTOM CREATIONS</div><h2>Commission Something Original</h2></div>")
            sb.Append("<div class=""sub"">Open studio slots &mdash; 5-step request, 3&ndash;5 day turnaround</div>")
            sb.Append("</div>")
            sb.Append("<div class=""grid cards"">")
            For Each s As CommissionSlotView In slots
                sb.Append(WebUi.CommissionSlotCard(s, "", "/App/CommissionHub.aspx", "View on Commission Hub"))
            Next
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        ' ---------- payments + closing CTA ----------

        Private Function PaymentPanel() As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""sec-head"" style=""margin-top:30px"">")
            sb.Append("<div><div class=""eyebrow"">PAYMENTS &amp; DELIVERY</div><h2>Trusted Nationwide Checkout</h2></div>")
            sb.Append("</div>")
            sb.Append("<div class=""grid"" style=""grid-template-columns:repeat(auto-fit,minmax(220px,1fr))"">")
            sb.Append(TrustCard("storefront", "GCash &amp; GOtyme",
                                "Pay from your e-wallet and record the reference number &mdash; no card details stored."))
            sb.Append(TrustCard("local_shipping", "J&amp;T Express",
                                "Studio-quoted nationwide shipping on every order. Track every parcel from My Orders."))
            sb.Append(TrustCard("verified", "Paid in full first",
                                "Every order is paid in full against its final total before it ships. No card details are ever stored."))
            sb.Append("</div>")
            Return sb.ToString()
        End Function

        Private Function TrustCard(icon As String, title As String, body As String) As String
            Return "<div class=""card"" style=""display:flex;gap:12px;align-items:flex-start"">" &
                   "<span style=""color:var(--primary);flex:0 0 auto"">" & WebUi.Ic(icon) & "</span>" &
                   "<span><b style=""font-family:var(--font-display);display:block;margin-bottom:2px"">" & title & "</b>" &
                   "<span class=""sub"">" & body & "</span></span></div>"
        End Function

        Private Function FinalCta() As String
            Dim sb As New StringBuilder()
            sb.Append("<div class=""card"" style=""margin:30px 0 6px;background:var(--yellow);border-color:#eec200;" &
                      "display:flex;align-items:center;justify-content:space-between;gap:18px;flex-wrap:wrap"">")
            ' The copy sits in its own block, not directly in the flex row: .eyebrow is
            ' display:inline-block, which used to put it and the headline on one line
            ' ("READY WHEN YOU ARECreate a free account..."). flex:1 lets the text take
            ' the space it needs and the buttons keep their own width when it wraps.
            sb.Append("<div style=""flex:1;min-width:300px"">")
            sb.Append("<div class=""eyebrow"" style=""display:block;color:var(--on-yellow)"">READY WHEN YOU ARE</div>")
            sb.Append("<b style=""display:block;font-family:var(--font-display);font-size:21px;margin-bottom:4px"">Create a free account to start your cart</b>")
            sb.Append("<div class=""sub"" style=""max-width:520px"">Browsing is open to everyone. An account lets you check out, " &
                      "track orders, save a wishlist and request commissions.</div>")
            sb.Append("</div>")
            sb.Append("<div class=""frow"" style=""margin:0;flex-shrink:0"">")
            sb.Append(WebUi.BtnHref("/Register.aspx", "Create Free Account", "primary", "person_add"))
            sb.Append(WebUi.BtnHref("/Login.aspx", "Sign In", "ghost"))
            sb.Append("</div></div>")
            Return sb.ToString()
        End Function

    End Class

End Namespace
