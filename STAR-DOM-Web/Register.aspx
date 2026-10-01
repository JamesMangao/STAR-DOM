<%@ Page Language="VB" CodeBehind="Register.aspx.vb" Inherits="STAR_DOM.Web.RegisterPage" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>STAR:DOM — Create Account</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Sora:wght@600;700;800&amp;family=Plus+Jakarta+Sans:wght@400;500;600;700;800&amp;display=swap" rel="stylesheet" />
    <link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,500..700,0..1,-50..200&amp;display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="/css/site.css?v=20261001" />
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
            <h2>Discover handcrafted art. <em style="font-style:normal;color:var(--yellow)">Made with love.</em></h2>
            <p>Create an account to shop artisan prints, stickers, keychains, and request custom commissions from the STAR:DOM studio.</p>
            <div class="auth-features">
                <div class="auth-feature"><%= STAR_DOM.Web.WebUi.Ic("inventory_2") %> Browse the full handcrafted product catalog</div>
                <div class="auth-feature"><%= STAR_DOM.Web.WebUi.Ic("brush") %> Request custom commissions directly from the artist</div>
                <div class="auth-feature"><%= STAR_DOM.Web.WebUi.Ic("local_shipping") %> Nationwide delivery via J&amp;T Express</div>
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
                <h2 style="font-size:22px;margin-bottom:4px">Create your account</h2>
                <p class="sub" style="margin:0 0 16px">Create your account to start shopping and collecting art.</p>
                <%= STAR_DOM.Web.WebUi.AlertBox(Me.Message, "err") %>
                <form method="post" action="/Register.aspx">
                    <%= STAR_DOM.Web.Csrf.HiddenField() %>
                    <div class="field">
                        <label for="fn">Full name</label>
                        <input id="fn" name="fullName" value="<%= STAR_DOM.Web.WebUi.Attr(Me.Prev("fullName")) %>" required />
                    </div>
                    <div class="form-grid2">
                        <div class="field">
                            <label for="em">Email</label>
                            <input id="em" name="email" type="email" value="<%= STAR_DOM.Web.WebUi.Attr(Me.Prev("email")) %>" required />
                        </div>
                        <div class="field">
                            <label for="un">Username</label>
                            <input id="un" name="username" value="<%= STAR_DOM.Web.WebUi.Attr(Me.Prev("username")) %>" required />
                        </div>
                    </div>
                    <div class="field">
                        <label for="ph">Phone</label>
                        <input id="ph" name="phone" value="<%= STAR_DOM.Web.WebUi.Attr(Me.Prev("phone")) %>" placeholder="09xx xxx xxxx" />
                    </div>
                    <div class="form-grid2">
                        <div class="field">
                            <label for="pw">Password</label>
                            <div class="input-pw-wrap">
                                <input id="pw" name="password" type="password" required autocomplete="new-password" />
                                <button type="button" class="pw-toggle-btn" id="pwToggle" aria-label="Toggle password visibility">
                                    <span class="ms" id="pwToggleIcon">visibility</span>
                                </button>
                            </div>
                        </div>
                        <div class="field">
                            <label for="pw2">Confirm password</label>
                            <div class="input-pw-wrap">
                                <input id="pw2" name="confirm" type="password" required autocomplete="new-password" />
                                <button type="button" class="pw-toggle-btn" id="pw2Toggle" aria-label="Toggle confirm password visibility">
                                    <span class="ms" id="pw2ToggleIcon">visibility</span>
                                </button>
                            </div>
                        </div>
                    </div>
                    <input type="hidden" name="role" value="CUSTOMER" />
                    <button class="btn primary" type="submit" style="width:100%;margin-top:6px"><%= STAR_DOM.Web.WebUi.Ic("how_to_reg") %> Create Account</button>
                </form>
                <script>
                    (function() {
                        function bindToggle(inputId, btnId, iconId) {
                            var inp = document.getElementById(inputId);
                            var btn = document.getElementById(btnId);
                            var ico = document.getElementById(iconId);
                            if (inp && btn && ico) {
                                btn.addEventListener('click', function() {
                                    if (inp.type === 'password') {
                                        inp.type = 'text';
                                        ico.textContent = 'visibility_off';
                                    } else {
                                        inp.type = 'password';
                                        ico.textContent = 'visibility';
                                    }
                                });
                            }
                        }
                        bindToggle('pw', 'pwToggle', 'pwToggleIcon');
                        bindToggle('pw2', 'pw2Toggle', 'pw2ToggleIcon');
                    })();
                </script>
                <p style="font-size:13px;margin:16px 0 0">Already have an account? <a href="/Login.aspx" style="color:var(--primary);font-weight:700">Sign in</a></p>
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
        </div>
    </main>
</div>
</body>
</html>