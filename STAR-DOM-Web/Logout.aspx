<%@ Page Language="VB" CodeBehind="Logout.aspx.vb" Inherits="STAR_DOM.Web.LogoutPage" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Signed out &mdash; STAR:DOM</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Sora:wght@600;700;800&amp;family=Plus+Jakarta+Sans:wght@400;500;600;700;800&amp;display=swap" rel="stylesheet" />
    <link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,500..700,0..1,-50..200&amp;display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="/css/site.css?v=20261005a" />
    <link rel="icon" type="image/webp" href="/Assets/stardom-logo.webp" />
    <%-- Returns to the public storefront, which needs no account. The visible
         button below is the fallback when the browser blocks this. --%>
    <meta http-equiv="refresh" content="4;url=/" />
</head>
<body>
    <div class="modal-backdrop open" id="signedOutModal" role="dialog" aria-modal="true" aria-labelledby="soTitle" aria-describedby="soMsg">
        <div class="modal-card" style="text-align:center">
            <span class="modal-ic ms" style="margin:0 auto 14px;background:rgba(22,163,74,.15);color:#15803d">check_circle</span>
            <h3 id="soTitle">You&rsquo;re signed out</h3>
            <p class="modal-msg" id="soMsg">Thanks for visiting STAR:DOM. Your session has ended &mdash; see you next time!</p>
            <div class="modal-actions" style="justify-content:center">
                <a class="btn primary" href="/"><span class="ms sm">home</span> Back to home</a>
            </div>
        </div>
    </div>
    <script>
        // Deliberately deferred, not an immediate replace(): the whole point of
        // this page is that the confirmation is actually seen before leaving.
        // replace() rather than assign() so the sign-out page is not an entry the
        // Back button can return to.
        setTimeout(function () { window.location.replace('/'); }, 2200);
    </script>
</body>
</html>