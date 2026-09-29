# STAR:DOM build state (website only — desktop app removed on request)

Workspace root (bash cwd): `/d/STARDOM`  (Windows: `D:\STARDOM`)

## What this is now
VB.NET **ASP.NET Web Forms** website + MySQL. The WinForms desktop app and its
project folder were deleted (user request); the shared business layers
(Models/Repositories/Services) were copied into the web project under
`STAR-DOM-Web/STAR-DOM-Web/Shared/` and the vbproj repointed to them.

```
D:\STARDOM
├── STAR-DOM-Web.sln                 <- optional, for Visual Studio
├── STAR-DOM-Web\
│   ├── run-website.bat              <- double-click to run (IIS Express)
│   ├── README.md
│   └── STAR-DOM-Web\                <- website project
│       ├── App\                     <- pages (customer/merchant/admin)
│       ├── Code\                    <- web-only: Db, Session, Guard, Validators, WebUi
│       ├── Shared\                  <- Models/Repositories/Services/Reports (self-contained)
│       ├── Database\                <- schema.sql + seed.sql (copies)
│       ├── packages\                <- MySql.Data 8.0.33 (local, offline build)
│       ├── css\site.css, web.config, *.aspx, Global.asax
├── tools\
│   ├── refasm\build\.NETFramework\v4.8\   <- .NET Framework 4.8 reference assemblies
│   └── downloads\iisexpress_amd64_en-US.msi
```

## Toolchain (verified)
- MSBuild: `"C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe"`
- Build command (no VS installed on the machine):
  `MSBuild STAR-DOM-Web.sln -t:Rebuild -p:Configuration=Debug -p:FrameworkPathOverride=D:/STARDOM/tools/refasm/build/.NETFramework/v4.8`
  → compiles clean (0 errors) into `STAR-DOM-Web\STAR-DOM-Web\bin\STAR_DOM_Web.dll`
- ASP.NET runtime: IIS Express 10 installed at `C:\Program Files\IIS Express`
  (official MSI from download.microsoft.com GUID C/E/8/CE8D18F5-…; installer kept
  in tools/downloads). Machine also has .NET Framework 4.8 runtime + Build Tools.

## Runtime (verified end-to-end via curl against live MySQL/XAMPP)
- `iisexpress.exe /path:"D:\STARDOM\STAR-DOM-Web\STAR-DOM-Web" /port:8095 /clr:v4.0`
- Login POST → 302 → role home; all 14 app screens return 200 with seeded data
  (bella=customer, mika=merchant, admin=admin). Bind note: IIS Express ad-hoc
  binds `localhost` only — browse http://localhost:8095, NOT 127.0.0.1 (400).
- MySQL: XAMPP (`C:\xampp\mysql\bin`), db `stardom`, 27 tables seeded, root has
  NO password → web.config connection string uses `Password=`.

## Abandoned approach (do not resurrect): custom HttpListener + System.Web host
A Cassini-style self-host (`STAR-DOM-WebHost.exe`) was built and got as far as
serving pages + parsing POST bodies, but **ASP.NET InProc session state never
persisted across requests** (every request created a new session, so logins
never "stuck"). Root cause not found after extensive instrumentation (domain
stable, cookies/forms forwarded correctly, response header index space was
corrected empirically: http.sys style Content-Type=12/Content-Length=13 for
request AND response sides). Not worth more time — IIS Express hosts natively
and everything (sessions, forms, default documents, static files) just works.
All custom-host code was deleted; this note prevents re-attempting it.

## SQL gotcha (still valid)
`EventSales` FK → `Orders` requires Orders to be created first in schema.sql
(MySQL errno 150). Keep that ordering.

## Conventions that still apply
- All SQL is parameterized; passwords PBKDF2-hashed; roles guard page access
  via `Code/Guard.vb` + `Code/Helpers/Session.vb`.
- Never hardcode business data — Categories/Products/Events/etc. come from MySQL.
- VB gotchas (kept from desktop build): module members can't be `Shared`;
  case-insensitive identifiers shadow types (`Path` param vs `Path.Combine`);
  `Select Case` names; keyword collisions; use `Step -1` not `DownTo`.
  Two more hit while adding CSRF: a method named `NewToken()` is parsed as
  `New Token()` (use `MakeToken()`), and `Imports System` is rejected as a
  duplicate because the vbproj already imports it project-wide.

## CSRF protection (added — do not remove or weaken)
Pages render raw `<form method="post">` markup from VB strings, so there are no
WebForms buttons to hang `[ValidateAntiForgeryToken]` on. Protection is a
hand-rolled synchronizer token instead:

- `Code/Csrf.vb` — per-session token (32 random bytes, base64url) kept in ASP.NET
  Session; `HiddenField()` emits the hidden input, `IsValidRequest()` compares it
  in constant time, `Rotate()` reissues it.
- Enforcement is **only** in `Global.asax.vb Application_AcquireRequestState`, so
  no individual page can forget to check it. Every POST in the site goes through it.
- The token is emitted once in the `Site.master` shell form, which wraps every App
  page, plus `Login.aspx` and `Register.aspx` which stand alone.
- `Login.aspx.vb` calls `Csrf.Rotate()` after a successful sign-in.
- Expired/absent session → 302 to `/Login.aspx?r=...`. Session present but token
  missing or mismatched → 403 page + a row in `AppErrors` with `Context='Security'`.

Two traps, both already hit once and fixed — don't "simplify" back into them:
1. The check must run on `AcquireRequestState`, **not** `BeginRequest`.
   `SessionStateModule` has not loaded the session yet in `BeginRequest`, so
   `HttpContext.Session` is `Nothing` there and *every* POST looks token-less.
2. For the expired-session redirect, set `Response.StatusCode`/`RedirectLocation`
   and call `CompleteRequest()`. `Response.Redirect(url, True)` does not reliably
   stop the pipeline on .NET 4 — execution falls through into the 403 branch,
   which then `Clear()`s the 302 and turns it into a 403 on a valid request.

## Secrets and error pages
- No keys in `web.config`. The Supabase publishable/secret keys that were there
  were removed (nothing read them — the port is still on MySql.Data). Real
  credentials come from environment variables; `Code/Db.vb` already prefers
  `MYSQL_URL` / `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` / `DB_PASSWORD` /
  `DB_SSLMODE` over the connection string.
- `customErrors` is `RemoteOnly`, not `Off`. Flip to `Off` only while debugging
  locally, then put it back — `Off` on the Render host leaks stack traces.
  Detailed errors are still written to the `AppErrors` table either way.
- Session cookie is `httpOnly` via `<httpCookies>`. `sameSite` is deliberately
  omitted: it is a .NET 4.7.2+ attribute and the Linux/Mono (xsp4) deploy in the
  Dockerfile may reject the whole config on it. `requireSSL` is `false` only
  because local dev is http://localhost — set it `true` behind HTTPS.
- `bin\STAR_DOM_Web.dll` is committed on purpose (the Dockerfile builds with
  `|| true` and xsp4 runs the prebuilt binary), so **rebuild and commit `bin\`**
  after any code change or the cloud host keeps serving the old build.

