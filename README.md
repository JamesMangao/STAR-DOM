# STAR:DOM — Artisan & Pop-up Hub (Website)

VB.NET **ASP.NET Web Forms (.NET Framework 4.8)** website on **PostgreSQL**
(`Npgsql`), deployable to Render (Mono/XSP4 Docker) against Supabase.

```
D:\STARDOM
├── STAR-DOM-Web.sln              <- open this in Visual Studio (optional)
├── Dockerfile / render.yaml      <- Render deploy (Mono + XSP4)
├── start-db.bat                  <- local portable PostgreSQL helper
├── tools\                        <- PostgreSQL server runtime (committed) + build notes
└── STAR-DOM-Web\                 <- the website (everything lives here)
    ├── run-website.bat           <- ★ double-click this to run the site
    ├── share-website.bat         <- share the local site via a Cloudflare tunnel
    │                                (cloudflared ships in tools\cloudflared\)
    ├── STAR-DOM-Web.vbproj
    ├── web.config                <- fallback connection string (localhost dev)
    ├── App\                      <- customer + merchant + admin pages
    ├── Code\                     <- web helpers (Session, Guard, Db, WebUi, Csrf)
    ├── Shared\                   <- Models / Repositories / Services (all VB.NET)
    ├── Database\                 <- supabase_schema.sql + supabase_seed.sql
    │   └── legacy-mysql\         <- pre-migration MySQL scripts (history only)
    ├── css\site.css              <- STAR:DOM design system
    └── bin\                      <- committed build output (rebuild after changes)
```

## Requirements 

| Requirement | Status |
|---|---|
| **IIS Express 10** | ✅ `C:\Program Files\IIS Express` |
| **.NET Framework 4.8** runtime | ✅ Present |
| **PostgreSQL 16.4 (portable, in the repo)** | ✅ server runtime in `tools\pgsql`; data dir auto-created at `tools\pgdata` on first run; db `stardom` on localhost:5432 |

`run-website.bat` and `share-website.bat` start the local database for you
(they call `..\start-db.bat` one level up) before launching IIS Express.
The site prefers env vars (`SUPABASE_DB_URL` / `POSTGRES_URL` / `DB_*`) and
falls back to the `web.config` connection string (localhost dev placeholder).

## Run the site

**Simplest:** double-click **`D:\STARDOM\STAR-DOM-Web\run-website.bat`** —
it boots PostgreSQL, starts IIS Express on port **8095**, and opens your browser.

Or from a terminal:

```bat
cd /d D:\STARDOM\STAR-DOM-Web
"C:\Program Files\IIS Express\iisexpress.exe" /path:"D:\STARDOM\STAR-DOM-Web" /port:8095 /clr:v4.0
```

Then open **http://localhost:8095** — unauthenticated visitors are sent to the
sign-in page. Close the IIS Express window to stop.

> Note: always browse via `http://localhost:8095` (not `127.0.0.1:8095` — IIS
> Express only binds the `localhost` host name for this ad-hoc launch).

## Demo accounts

| Role | Username | Password | Lands on |
|---|---|---|---|
| Customer | `bella` | `customer123` | Marketplace |
| Store owner (admin = merchant) | `admin` | `admin123` | Merchant Dashboard |
| Store owner (legacy demo account) | `mika` | `admin123` | Merchant Dashboard |

## What you can do end-to-end

- **Customer:** browse/search the catalog (bundle deals shown and **applied
  automatically** — Stickers 4 for ₱100, Button pins 3 for ₱100), product
  detail, cart & checkout, choose **Delivery (J&T Express)** or **Pick-up at an
  active/upcoming pop-up stall** (stall hours shown, no shipping fee), pay via
  **GCash / Maya** (reference number) or **Cash on Delivery/Claim**, order
  tracking with the J&T status line + tracker link, two-sided pick-up claim
  ("Confirm order received" + the stall's "Confirm hand-over" closes the order),
  pop-up locations & schedules, commission requests (5-step wizard; commissioned
  products are delivery-only), reviews, notifications, profile.
- **Store owner (`admin`, or the legacy `mika`/`renzo`/`puffu` owner
  accounts):** dashboard KPIs, products & stock (online stock is
  adjusted manually after physical booth sales — the site sells online only),
  event & booth manager with per-event inventory, commission pipeline
  (accept / decline / clarify / offer / production), orders & payments:
  confirm orders, book J&T with the tracking number, confirm pick-up hand-over,
  and record payments behind a **password re-entry** (e-wallet reference number
  required for GCash/Maya), sales reports, review moderation, Admin Console
  (users & roles).
- **Admin:** the owner account IS the admin — STAR:DOM has exactly two roles:
  ADMIN (store owner, who is also the merchant/artist) and CUSTOMER. New
  registrations always become CUSTOMER; only the owner can manage accounts.

All business data is read from and written to PostgreSQL via parameterized
`Npgsql` calls; nothing is hardcoded.

## Security notes

- Every POST is CSRF-checked. Pages render raw `<form method="post">` markup from
  VB strings, so protection is a hand-rolled per-session token (`Code\Csrf.vb`)
  verified in one place — `Global.asax.vb Application_AcquireRequestState` — rather
  than per-page. The token rides in the `Site.master` shell form that wraps every
  App page, and is reissued on sign-in. A mismatch returns 403 and logs to
  `AppErrors` with `Context='Security'`.
- **No keys in `web.config`.** DB credentials come from the `SUPABASE_DB_URL` /
  `POSTGRES_URL` / `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` / `DB_PASSWORD`
  environment variables (`Code\Db.vb` prefers them over the connection string).
  Keep it that way — do not paste a real password into the file.
- `customErrors` is `RemoteOnly`, so stack traces are not served. The friendly
  `Error.aspx` page shows a short message and the full detail goes to the
  `AppErrors` table. Set it to `Off` only while debugging, then put it back.
- `bin\STAR_DOM_Web.dll` is committed because the Linux/Mono deploy runs the
  prebuilt binary — rebuild and commit `bin\` with any code change.

## Rebuilding after code changes

The project compiles with **MSBuild / Visual Studio**. On this machine there is
no Visual Studio IDE, so from a terminal (repo root):

```bat
cd /d D:\STARDOM
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" STAR-DOM-Web.sln -t:Build -p:Configuration=Debug -p:FrameworkPathOverride=D:\STARDOM\tools\refasm\build\.NETFramework\v4.8
```

(`tools\refasm` provides the .NET Framework 4.8 reference assemblies; the real
4.8 runtime is already installed. In Visual Studio you do not need that switch —
just open `STAR-DOM-Web.sln`, install the **ASP.NET and web development**
workload if prompted, and press **Ctrl+F5**.)

After a rebuild, refresh the browser (IIS Express picks up new `bin` DLLs
automatically; restart it if anything looks stale).

## Setting up a new machine (after cloning from GitHub)

The repo already ships everything the site needs to RUN: the compiled
`bin\STAR_DOM_Web.dll` + all vendored Npgsql DLLs, `packages\`, the
schema/seed SQL, the reference assemblies used by the build
(`tools\refasm`), and even the IIS Express installer
(`tools\downloads\iisexpress_amd64_en-US.msi`). What a fresh machine must
provide:

1. **Windows 10/11** — the .NET Framework 4.8 runtime is preinstalled.
2. **IIS Express 10** — double-click `tools\downloads\iisexpress_amd64_en-US.msi`,
   or `winget install Microsoft.IISExpress`.
3. **A PostgreSQL database** — pick ONE:
   - **Local portable PostgreSQL (INCLUDED in the repo — nothing to download):**
     the PostgreSQL 16.4 server runtime is committed under `tools\pgsql\`
     (bin/lib/share, ~119 MB; pgAdmin and the data directory are excluded).
     Just run `start-db.bat` (or either launcher): on first run it
     auto-initialises the data directory, creates the `stardom` database and
     loads the schema + seed — no manual steps.
   - **Supabase (for shared/online data):** create a free project, run
     `Database\supabase_schema.sql` + `Database\supabase_seed.sql` in the
     SQL editor, then point the app at it:
     `setx SUPABASE_DB_URL "postgresql://postgres:<password>@db.<ref>.supabase.co:5432/postgres"`
4. **Run it:** double-click `run-website.bat` → http://localhost:8095 or double-click `share-website.bat` if you want to share it publicly (via Cloudflare tunnel).

Notes:
- You do NOT need Visual Studio to run the site — the committed `bin\` DLL
  works as-is. Build only after code changes ("Rebuilding" below);
  `tools\refasm` ships in the repo, so the documented build command works as
  long as the clone is at `D:\STARDOM` (a few documented paths are absolute).
- The launchers and `start-db.bat` are written with `%~dp0`, so they work from
  any folder; only the build command hardcodes the repo location.
- **Render deploy needs nothing local:** push to GitHub, create a Render Web
  Service from the repo, set `SUPABASE_DB_URL` in the dashboard — Render builds
  the Docker image (Mono/XSP4) from source.
- `Database\legacy-mysql\` holds the original MySQL schema/seed scripts kept
  for history — the application no longer uses MySQL.
