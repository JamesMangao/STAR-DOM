<%@ Page Language="VB" CodeBehind="Login.aspx.vb" Inherits="STAR_DOM.Web.LoginPage" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>STAR:DOM — Sign In</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Sora:wght@600;700;800&amp;family=Plus+Jakarta+Sans:wght@400;500;600;700;800&amp;display=swap" rel="stylesheet" />
    <link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,500..700,0..1,-50..200&amp;display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="/css/site.css?v=20261003b" />
    <link rel="icon" type="image/webp" href="/Assets/stardom-logo.webp" />
    <link rel="apple-touch-icon" href="/Assets/stardom-logo.webp" />
</head>
<body>
<div class="auth-shell">
    <aside class="auth-art">
        <div class="auth-brand">
            <img src="/Assets/stardom-logo.webp" alt="STAR:DOM logo" style="height:44px;width:auto;object-fit:contain" />
            <div>
                <div class="brand-logo">STAR:DOM<span class="brand-dot"></span></div>
                <div class="brand-sub">ARTISAN &amp; POP-UP HUB</div>
            </div>
        </div>
        <div>
            <h2>Turn your ideas into <em style="font-style:normal;color:var(--yellow)">living art.</em></h2>
            <p>Shop artisan prints, handcrafted stickers, limited merch, and custom commissions — online, or in person at our pop-up tours across the Philippines.</p>
            <div class="auth-features">
                <div class="auth-feature"><%= STAR_DOM.Web.WebUi.Ic("storefront") %> Shop original art straight from a solo PH artist</div>
                <div class="auth-feature"><%= STAR_DOM.Web.WebUi.Ic("qr_code_2") %> Pay with GCash / GOtyme / QR Ph at every booth</div>
                <div class="auth-feature"><%= STAR_DOM.Web.WebUi.Ic("draw") %> Request commission slots online</div>
            </div>
        </div>
        <div style="font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:rgba(255,255,255,.6)">Tour 2026 · South Luzon &amp; Metro Manila</div>
    </aside>
    <main class="auth-panel">
        <div class="auth-card">
            <div class="card">
                <div style="margin-bottom:14px">
                    <div class="brand-logo" style="font-weight:800;font-size:24px">STAR:DOM<span class="brand-dot"></span></div>
                </div>
                <h2 style="font-size:22px;margin-bottom:4px">Welcome back</h2>
                <p class="sub" style="margin:0 0 16px">Sign in to shop the marketplace or run your Merchant Studio.</p>
                <%= STAR_DOM.Web.WebUi.AlertBox(Me.Message, "err") %>
                <form method="post" action="/Login.aspx">
                    <%= STAR_DOM.Web.Csrf.HiddenField() %>
                    <div class="field">
                        <label for="id">Email or username</label>
                        <input id="id" name="identifier" value="<%= STAR_DOM.Web.WebUi.Attr(Me.Identifier) %>" placeholder="e.g. mika or mika@stardom.ph" required />
                    </div>
                    <div class="field">
                        <label for="pw">Password</label>
                        <div class="input-pw-wrap">
                            <input id="pw" name="password" type="password" required autocomplete="current-password" />
                            <button type="button" class="pw-toggle-btn" id="pwToggle" aria-label="Toggle password visibility">
                                <span class="ms" id="pwToggleIcon">visibility</span>
                            </button>
                        </div>
                    </div>
                    <input type="hidden" name="r" value="<%= STAR_DOM.Web.WebUi.Attr(Me.ReturnUrl) %>" />
                    <button class="btn primary" type="submit" style="width:100%;margin-top:6px"><%= STAR_DOM.Web.WebUi.Ic("login") %> Sign In</button>
                </form>
                <script>
                    (function() {
                        var pw = document.getElementById('pw');
                        var btn = document.getElementById('pwToggle');
                        var icon = document.getElementById('pwToggleIcon');
                        if (pw && btn && icon) {
                            btn.addEventListener('click', function() {
                                if (pw.type === 'password') {
                                    pw.type = 'text';
                                    icon.textContent = 'visibility_off';
                                } else {
                                    pw.type = 'password';
                                    icon.textContent = 'visibility';
                                }
                            });
                        }
                    })();
                </script>
                <p style="font-size:13px;margin:16px 0 0">New to STAR:DOM? <a href="/Register.aspx<%= Me.ReturnQuery %>" style="color:var(--primary);font-weight:700">Create an account</a></p>
                <script>
                    (function () {
                        document.addEventListener('click', function (e) {
                            var link = e.target && e.target.closest ? e.target.closest('a') : null;
                            if (!link || !link.href || link.target === '_blank') return;
                            if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || e.button !== 0) return;
                            var url;
                            try { url = new URL(link.href, window.location.href); } catch (err) { return; }
                            if (url.origin !== window.location.origin) return;
                            e.preventDefault();
                            document.body.classList.add('page-leaving');
                            setTimeout(function () { window.location.href = link.href; }, 140);
                        });
                        window.addEventListener('pageshow', function () {
                            document.body.classList.remove('page-leaving');
                        });
                    })();
                </script>
            </div>

            <div class="auth-demo">
                <b>Demo accounts</b><br />
                Customer — <b>bella</b> / customer123<br />
                Admin &nbsp;&nbsp;&nbsp;&nbsp;— <b>admin</b> / admin123
            </div>
        </div>
    </main>
</div>
</body>

</html>