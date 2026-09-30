<%@ Page Language="VB" CodeBehind="Receipt.aspx.vb" Inherits="STAR_DOM.Web.ReceiptPage" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>STAR:DOM — Official Receipt</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Sora:wght@600;700;800&amp;family=Plus+Jakarta+Sans:wght@400;500;600;700;800&amp;display=swap" rel="stylesheet" />
    <link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,500..700,0..1,-50..200&amp;display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="/css/site.css" />
</head>
<body class="receipt-body">
    <div class="receipt-toolbar">
        <a class="btn ghost" href="/App/OrderDetail.aspx?id=<%= STAR_DOM.Web.WebUi.Attr(Me.BackOrderId) %>"><span class="ic ms">arrow_back</span><span>Back to Order</span></a>
        <button class="btn primary" type="button" onclick="window.print()"><span class="ic ms">print</span><span>Print Receipt</span></button>
    </div>
    <div class="receipt-stage">
        <div class="printer-head" aria-hidden="true">
            <div class="ph-slot"></div>
        </div>
        <asp:Literal ID="Out" runat="server" />
    </div>
</body>
</html>