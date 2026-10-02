# STAR:DOM — Artisan & Pop-up Hub (Website)

VB.NET **ASP.NET Web Forms (.NET Framework 4.8)** website on **PostgreSQL**
(`Npgsql`), deployable to Render (Mono/XSP4 Docker) against Supabase.

See [ARCHITECTURE-DIAGRAM.md](ARCHITECTURE-DIAGRAM.md) for the request path,
the layer rules and the checkout state machine.

```
D:\STARDOM
├── run-website.bat               <- ★ double-click this to run the site
├── share-website.bat             <- share the site publicly via a Cloudflare tunnel
│                                    (cloudflared ships in tools\cloudflared\)
├── start-db.bat                  <- local portable PostgreSQL helper (fallback)
├── STAR-DOM-Web.sln              <- open this in Visual Studio (optional)
├── Dockerfile / render.yaml      <- Render deploy (Mono + XSP4)
├── tools\                        <- PostgreSQL runtime (committed) + Supabase scripts
│   ├── supabase-env.bat          <- loads SUPABASE_DB_URL into the environment
│   ├── reseed-supabase.bat       <- wipes + reloads Supabase from schema/seed
│   ├── supabase-credentials.example.txt
│   ├── supabase\prod-ca-2021.crt <- Supabase root CA (must be installed)
│   └── pgsql\                    <- PostgreSQL server runtime (committed, fallback)
└── STAR-DOM-Web\                 <- the website (everything lives here)
    ├── STAR-DOM-Web.vbproj
    ├── web.config                <- localhost fallback connection string only
    ├── App\                      <- customer + merchant + admin pages
    │   └── PaymentQr.aspx        <- streams a wallet's QR image out of the database
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
| **A PostgreSQL database** | ✅ Supabase (shared, intended) or the portable copy in `tools\pgsql` (fallback) |
| **Supabase root CA** | one-time `certutil` per machine — see [Which database?](#which-database) |

`run-website.bat` and `share-website.bat` pick the database for you. If
`tools\supabase-credentials.txt` exists they load `SUPABASE_DB_URL` and use
**Supabase**; otherwise they fall back to the portable PostgreSQL in
`tools\pgsql`, which auto-initialises itself on first run. The site prefers
env vars (`SUPABASE_DB_URL` / `POSTGRES_URL` / `DB_*`) and falls back to the
`web.config` connection string (localhost only).

Force the local copy with `set SD_LOCAL_DB=1` before running a launcher.

## Which database?

There are **two**, and only one of them is real. The launchers decide
automatically.

| | **Supabase** (intended) | **Local** (fallback) |
|---|---|---|
| Enabled by | `tools\supabase-credentials.txt` existing | that file being absent |
| Data | **shared by every machine** | per-machine, seed data only |
| QR codes | travel with the database | travel with the database |
| Needs setup | the CA + a credentials file | none |
| Force it | *(default)* | `set SD_LOCAL_DB=1` |

Nothing is hardcoded either way. `Code\Db.vb` resolves the connection in this
order:

1. `SUPABASE_DB_URL` / `POSTGRES_URL` / `DATABASE_URL`
2. `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` / `DB_PASSWORD` (+ `DB_SSLMODE`)
3. the `web.config` connection string (localhost, development only)

`web.config` therefore never holds a real password.

### Pointing a machine at Supabase

**1. Trust the CA.** The pooler presents a certificate that chains to
`Supabase Root 2021 CA`, which Windows does not trust out of the box. Run this
once per machine — it installs into your *user* store, so no admin rights and
no system-wide change:

```bat
certutil -user -addstore -f Root D:\STARDOM\tools\supabase\prod-ca-2021.crt
```

Expected SHA-256 fingerprint (from Supabase's own dashboard download):

```
80:70:25:AD:50:D4:ED:21:9D:2C:9C:7D:29:9C:00:4F:82:4E:B0:0C:F7:F6:5A:FE:F6:07:D0:7B:72:E6:CA:FA
```

**2. Create the credentials file.** Copy the template and paste your connection
string (**Session pooler**, not Transaction pooler):

```bat
copy tools\supabase-credentials.example.txt tools\supabase-credentials.txt
```

**3. Run it.** `run-website.bat` now prints `Database: Supabase`.

<details>
<summary>Two Supabase details that cost real debugging time</summary>

- **Use the pooler, not `db.<ref>.supabase.co`.** The direct host resolves to
  an IPv6-only address; a Windows machine without IPv6 can never connect to it.
  Use `aws-0-<region>.pooler.supabase.com`.
- **Use the Session pooler (port 5432), not the Transaction pooler (6543).**
  Npgsql uses server-side prepared statements, which PgBouncer's transaction
  mode does not support — every query fails.
- The vendored **Npgsql is 4.1.10**, which neither parses a `postgresql://` URI
  nor knows an `Include Error Detail` key. `Db.UriToConnString` converts the
  URI, and builds the result through `NpgsqlConnectionStringBuilder` rather
  than by writing `Key=Value` text by hand.
</details>

### Resetting Supabase

`tools\reseed-supabase.bat` drops and rebuilds the `public` schema from the
committed schema + seed. It asks you to type `RESET` first, because it
**deletes every order, payment and user**. Re-adding Supabase's API grants is
part of the script — without them the tables exist but the REST API returns
nothing.

### What is *not* in Git

| Thing | In Git? | Consequence |
|---|---|---|
| PostgreSQL engine (`tools\pgsql`) | ✅ 1,627 files | a clone needs no download |
| Schema + seed SQL | ✅ | a fresh clone can build the tables |
| **Database contents** | ❌ | orders and users live only in Supabase |
| `tools\supabase-credentials.txt` | ❌ git-ignored | never commit it |
| `*.dump` | ❌ git-ignored | a dump is the whole database, QR images included |
| `STAR-DOM-Web\Uploads\` | ❌ git-ignored | legacy path only; QR images are in the DB now |

Because the QR codes are stored in the database, a backup or a fresh machine
gets them for free — there is no image folder to copy around.

## Run the site**Simplest:** double-click **`D:\STARDOM\run-website.bat`** — 
it picks the database for you (Supabase if `tools\supabase-credentials.txt`
exists, otherwise the local PostgreSQL in `tools\pgsql`), starts IIS Express on
port **8095**, and opens your browser.

Or from a terminal:

```bat
cd /d D:\STARDOM\STAR-DOM-Web
"C:\Program Files\IIS Express\iisexpress.exe" /path:"D:\STARDOM\STAR-DOM-Web" /port:8095 /clr:v4.0
```

Then open **http://localhost:8095** — the storefront landing page loads for
anonymous visitors, who can browse the marketplace, catalog, product pages, and
the pop-up tour schedule freely. Sign-in is only required to add to cart, check
out, or open any account page. Close the IIS Express window to stop.

> Note: always browse via `http://localhost:8095` (not `127.0.0.1:8095` — IIS
> Express only binds the `localhost` host name for this ad-hoc launch).

## The sign-in gate

Pressing **Add to cart** (or any account-only link) while signed out opens a
premium modal instead of a bare redirect — it shows the product, explains why an
account is needed, and offers **Sign In** / **Create free account**. Both carry
the clicked link as the return target, so the item lands in the cart the moment
the visitor signs in (`?r=` is threaded through `Login.aspx` *and*
`Register.aspx`).

It is a courtesy layer, not the security boundary. Every account page still
starts with `Guard.Require*`, so the gate is inert without JavaScript, when
someone types the URL directly, and for signed-in visitors (the master only
renders it when nobody is authenticated, which also makes the shell script a
no-op). Product buttons are tagged server-side by `WebUi.AuthGateAttrs`; the
remaining account-only links are matched by one `GATED` path list in the
`Site.master` script.

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
  **GCash / GOtyme** (reference number) or **Cash on Delivery/Claim**, order
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
  required for GCash/GOtyme), sales reports, review moderation, Admin Console
  (users & roles), and **Payment Settings**.
- **Admin:** the owner account IS the admin — STAR:DOM has exactly two roles:
  ADMIN (store owner, who is also the merchant/artist) and CUSTOMER. New
  registrations always become CUSTOMER; only the owner can manage accounts.

All business data is read from and written to PostgreSQL via parameterized
`Npgsql` calls; nothing is hardcoded.

## Payment Settings (GCash / GOtyme QR management)

**Admin → SYSTEM → Payment Settings** (`/App/Admin/PaymentSettings.aspx`) is
where the owner manages everything customers see when paying by e-wallet —
no code changes needed:

| Setting | What it controls |
|---|---|
| **QR code image** | Upload a PNG/JPG/WebP (max 5 MB) of each wallet's official QR; shown in the order payment popup. Without an upload, a stylized placeholder is shown. |
| **Account number** (QR number) | The GCash / GOtyme mobile number displayed beside the QR. |
| **Account name** (QR name) | The registered wallet name shown on the popup. |
| **Display mode** | What customers see: **Everything** (QR + number + name, default), **QR code only**, **Number + name only**, or **Account name only**. |
| **Show at checkout** | Toggle per channel — when off, that e-wallet disappears from the checkout payment options (COD is always offered). |
| **Caption** | Optional note under the QR (e.g. "Scan using the GCash app"). |

Each channel (GCash blue, GOtyme green) has its own card with a live
**customer-view preview**, and changes take effect immediately on the order
detail payment popup and at checkout.

**The image is stored in the database**, in `PaymentSettings.QrImageData` as
`BYTEA` — not as a file under `Uploads\`. `App\PaymentQr.aspx` streams it back
(`/App/PaymentQr.aspx?ch=GCASH`) with an `ETag`, so a backup, a restore, or a
brand-new machine has the QR without anyone copying an image folder around.
`QrImageFile` is kept only so rows uploaded by an older build, which still
point at a file, keep working; re-uploading moves them into the database and
clears the path.

The pre-rename channel key `MAYA` is still read and displayed as GOtyme, and
`supabase_schema.sql` carries an in-place rename for installs that still have
it.

## Checkout and the Scan to Pay popup

For an e-wallet, **pressing "Place Order" does not create the order yet.** It
re-renders the checkout form with the Scan to Pay popup on top, keeping every
field the customer typed. The order is written only when they press
**I Have Scanned & Sent Payment** inside the popup.

This makes the popup's **Back** button meaningful: it simply closes the popup,
leaving the customer on the same form with their address and notes intact, so
picking the wrong wallet costs them nothing and cannot leave an abandoned
order behind. COD has nothing to confirm and is recorded immediately.

The popup is shared by Checkout and Order Detail
(`WebUi.QrPaymentModal`), and Order Detail also has a **Scan to Pay / show QR
again** button for a customer who closed it.

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
- **The Supabase password lives in one git-ignored file.**
  `tools\supabase-credentials.txt` holds `SUPABASE_DB_URL` and is listed in
  `.gitignore`, together with `*.dump` / `*.backup` / `tools\backup\`. A dump
  is the entire database — users, orders and the wallet QR images — so treat
  one as a credential. Commit `tools\supabase-credentials.example.txt` instead;
  it has everything except the password. Before pushing, confirm the staged
  file list contains no `supabase-credentials.txt` and no `.dump`.
- **Rotate the Supabase keys.** Any password or `sb_secret_…` API key that was
  pasted into a chat, a screenshot or a log is compromised. Rotate it in the
  Supabase dashboard, then update the local credentials file — no code change
  is involved, the app only reads `SUPABASE_DB_URL`.
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
3. **A database** — pick ONE (see [Which database?](#which-database)):
   - **Local portable PostgreSQL (INCLUDED in the repo — nothing to download):**
     the PostgreSQL 16.4 server runtime is committed under `tools\pgsql\`
     (bin/lib/share, ~119 MB; pgAdmin and the data directory are excluded).
     Just run `start-db.bat` (or either launcher): on first run it
     auto-initialises the data directory, creates the `stardom` database and
     loads the schema + seed — no manual steps.
   - **Supabase (shared — the intended setup):** create a free project, run
     `Database\supabase_schema.sql` then `Database\supabase_seed.sql` in the SQL
     editor (or, on a machine that can already reach the database, run
     `tools\reseed-supabase.bat` instead — it does both). Then, two steps on
     **each** machine:
     ```bat
     certutil -user -addstore -f Root D:\STARDOM\tools\supabase\prod-ca-2021.crt
     copy tools\supabase-credentials.example.txt tools\supabase-credentials.txt
     ```
     and paste the **Session pooler** URL (port 5432,
     `aws-0-<region>.pooler.supabase.com`) plus `?sslmode=require` into
     `SUPABASE_DB_URL`. `run-website.bat` then prints `Database: Supabase`.
     Without the `certutil` step the connection fails with a certificate error,
     because the pooler chains to `Supabase Root 2021 CA`, which Windows does
     not trust out of the box.
4. **Run it:** double-click `run-website.bat` → http://localhost:8095 or double-click `share-website.bat` if you want to share it publicly (via Cloudflare tunnel).

Because the database is shared, the QR codes, users, products and orders are
already there — a new machine starts with the same data, and needs no dump.
Use `set SD_LOCAL_DB=1` in front of a launcher to work against the local
fallback copy instead.

Notes:
- You do NOT need Visual Studio to run the site — the committed `bin\` DLL
  works as-is. Build only after code changes (see "Rebuilding" above);
  `tools\refasm` ships in the repo, so the documented build command works as
  long as the clone is at `D:\STARDOM` (a few documented paths are absolute).
- The launchers and `start-db.bat` are written with `%~dp0`, so they work from
  any folder; only the build command hardcodes the repo location.
- **Render deploy needs nothing local:** push to GitHub, create a Render Web
  Service from the repo, set `SUPABASE_DB_URL` in the dashboard — Render builds
  the Docker image (Mono/XSP4) from source.
- **New machine, Supabase:** steps 1–4 above only. `tools\pgsql`, `packages\`
  and the prebuilt `bin\` all come from the clone, and `certutil -user` needs
  no administrator rights.
- `Database\legacy-mysql\` holds the original MySQL schema/seed scripts kept
  for history — the application no longer uses MySQL.
