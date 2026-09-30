<%@ Page Language="VB" CodeBehind="Error.aspx.vb" Inherits="STAR_DOM.Web.ErrorPage" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>STAR:DOM — Error</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Sora:wght@600;700;800&amp;family=Plus+Jakarta+Sans:wght@400;500;600;700;800&amp;display=swap" rel="stylesheet" />
    <link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,500..700,0..1,-50..200&amp;display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="/css/site.css" />
</head>
<body>
<div class="auth-wrap">
    <div class="card" style="margin:auto">
        <h2 style="margin-bottom:6px">Something went wrong</h2>
        <p class="sub"><%= STAR_DOM.Web.WebUi.Esc(Me.Detail) %></p>
        <p class="sub" style="font-size:12px">The error was recorded to the AppErrors log table. Try going back or signing in again.</p>
        <div class="frow">
            <a class="btn primary" href="/App/Marketplace.aspx">Go to Marketplace</a>
            <a class="btn ghost" href="/Login.aspx">Back to Sign In</a>
        </div>
    </div>
</div>
</body>
</html>
