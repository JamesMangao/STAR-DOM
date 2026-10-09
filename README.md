# STAR:DOM — Artisan & Pop-up Hub (Website)

An online marketplace for a pop-up artisan brand. Customers browse a
catalog of handmade goods and pay online by **GCash or GOtyme e-wallet** —
shipped nationwide via **J&T Express** (delivery only; no cash on delivery
and no pick-up). The studio quotes the real J&T shipping fee once an order
is placed, the customer pays the final total against that quote, and the
studio verifies the e-wallet reference before anything ships. Artists take
custom work through a commission pipeline, and the store owner runs
everything from a merchant studio.

**Repository:** github.com/JamesMangao/STAR-DOM

## What this is

A single ASP.NET Web Forms application: one process, one .NET Framework
4.8 AppDomain, one PostgreSQL database. There is no API tier, no ORM and
no client-side framework — the browser receives HTML, a CSS
design system and a little vanilla JavaScript.

| | |
|---|---|
| **Language** | VB.NET |
| **Framework** | ASP.NET Web Forms, .NET Framework 4.8 |
| **Database** | PostgreSQL via Npgsql 4.1.10 — 30 tables, no ORM, parameterised SQL throughout |
| **Hosting** | IIS Express 10 locally, or Mono/XSP4 in a Docker image on Render |
| **Roles** | Only two: **ADMIN** (the store owner, who is also the artist) and **CUSTOMER** |
| **Payments** | GCash / GOtyme by QR, paid against a studio-quoted J&T fee — no COD |
| **Fulfilment** | J&T Express nationwide (delivery only) |
| **Receipts** | BIR-style receipts with a VAT breakdown |
| **External services** | None required — no payment gateway API, no shipping API, no third-party auth |

### How a request flows

Browser → an `.aspx` page → its `.aspx.vb` code-behind →
`Shared\Services` → `Shared\Repositories` → `Code\Db.vb` → PostgreSQL.

Rendering runs the other way, through `Code\WebUi.vb`, which
centralises the design system so a card, table, badge or modal renders
identically wherever it appears.

Pages never talk to Npgsql directly, and repositories never build HTML.
That one rule is what stops a change rippling across the whole site.

## What you can do end-to-end

- **Customer:** browse/search the catalog (bundle deals shown and **applied
  automatically** — Stickers 4 for ₱100, Button pins 3 for ₱100), product
  detail, cart & checkout (delivery address + phone — the J&T fee is quoted
  after you order), place the order unpaid, then pay the **final** total once
  the studio has returned it, via **GCash / GOtyme Scan-to-Pay** (e-wallet
  reference number) behind a **password re-entry** — then track the parcel
  through the J&T status line + tracker link and confirm it arrived ("Confirm
  order received") to unlock reviews, pop-up locations & schedules,
  commission requests (5-step wizard; commissioned products are
  delivery-only), reviews, notifications, profile.
- **Store owner (`admin`, or the legacy `mika`/`renzo`/`puffu` owner
  accounts):** dashboard KPIs, products & stock (online stock is
  adjusted manually after physical booth sales — the site sells online only),
  event & booth manager, commission pipeline
  (accept + quote / decline / production), orders & payments:
  **confirm & quote** an order (entering the J&T fee IS the confirmation),
  book J&T with the tracking number, and **verify submitted payments** behind
  a **password re-entry** (confirm → PAID + official receipt, or decline →
  customer is handed the support line), sales reports, review moderation,
  Admin Console (users & roles), and **Payment Settings**.
- **Admin:** the owner account IS the admin — STAR:DOM has exactly two roles:
  ADMIN (store owner, who is also the merchant/artist) and CUSTOMER. New
  registrations always become CUSTOMER; only the owner can manage accounts.

### Commission slots

The **Open Commission Slots** cards (Marketplace, home page, Commission Hub) are
generated from merchants who have `CommissionSlotCapacity > 0`, and each card
shows the artist name, turnaround, claim method and a link into the Hub. The
per-artist style tagline and the fixed formats list were removed as clutter:
the output is agreed per request, so a fixed list on the card was misleading.
`Users.CommissionTagline` and `Users.CommissionFormats` are still stored and
still editable — they are just not rendered on the card.

### What is real and what is seeded

All business data is read from and written to PostgreSQL through parameterised
`Npgsql` calls — there is no ORM and no string-built query anywhere. But be
clear about what is live and what is demonstration data:

| | |
|---|---|
| Cart, checkout, orders, payments, commissions, stock, reviews | **real** — real tables, real SQL, real writes |
| Products, users, events, booth inventory | seeded from `supabase_seed.sql` (100 products, 10 users, 7 events) |
| Wallet QR images | **real uploads**, stored in the database |
| Some dashboard KPI captions | **hardcoded sample text** — e.g. `All venues pre-cleared for mall merchant badges` |
| Orders & payments | **never seeded** — every order/payment row is written live. The shared Supabase currently holds a handful of demo rows from end-to-end verification (3 orders, 3 payments, 2 receipts at the time of writing) |

The revenue figures themselves *are* computed from `EventSales` and `Orders` —
it is the surrounding flavour text that is illustrative.


## How the code is organised

```
<your clone folder>
├── setup.bat                     <- ★ run ONCE on a new machine
├── run-website.bat               <- ★ run this to start the site
├── build.bat                     <- run after any code change
├── share-website.bat             <- share the site publicly via a Cloudflare tunnel
│                                    (cloudflared ships in tools\cloudflared\)
├── start-db.bat                  <- local portable PostgreSQL helper (fallback)
├── STAR-DOM-Web.sln              <- open this in Visual Studio (optional)
├── Dockerfile / render.yaml      <- Render deploy (Mono + XSP4)
├── tools\                        <- PostgreSQL runtime (committed) + Supabase scripts
│   ├── BUILD_STATE.md            <- build/runtime notes for whoever is next
│   ├── supabase-env.bat          <- loads SUPABASE_DB_URL into the environment
│   ├── set-credentials.ps1       <- masked password prompt used by setup.bat
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

### The layers

| Layer | Lives in | May call | Must not |
|---|---|---|---|
| Pages | `App\*.aspx.vb` | `Code\*`, `Shared\Services` | touch Npgsql directly |
| Web helpers | `Code\` | `Shared\*` | render business data |
| Services | `Shared\Services` | Repositories, Models | know about `HttpContext` |
| Repositories | `Shared\Repositories` | Models, `Code\Db.vb` | build HTML or hold business rules |
| Models | `Shared\Models` | nothing | carry behaviour beyond validation |

`Code\Db.vb` is the only place that decides which database to
talk to, which is why switching between Supabase and the local fallback is
an environment variable rather than a code change.

[ARCHITECTURE-DIAGRAM.md](ARCHITECTURE-DIAGRAM.md) draws the full request
path, the layer rules and the order & payment state machine.

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
| **Show at checkout** | Toggle per channel — when off, the customer cannot pick that wallet from the order payment options (there is no cash-on-delivery alternative). |
| **Caption** | Optional note under the QR (e.g. "Scan using the GCash app"). |

Each channel (GCash blue, GOtyme green) has its own card with a live
**customer-view preview**, and changes take effect immediately on the order
detail payment popup.

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


## Checkout and the payment flow

Checkout no longer asks how the customer will pay. It collects the delivery
address and contact phone, and **"Place Order" writes the order immediately**
in a single transaction — status `PENDING`, payment method parked as
`PENDING`, shipping fee *not* yet included. There is no popup at checkout:
the studio cannot know the J&T fee until the parcel is weighed, so there is
nothing final to pay yet, and no COD exists for a checkout to record.

The flow continues on the order pages:

1. **Studio quotes** — the merchant enters the J&T shipping fee behind a
   password on the order. That **is** the confirmation: the order becomes
   `CONFIRMED` and the total is final (goods + bundle savings + shipping) in
   the same statement.
2. **Customer pays** — on Order Detail the customer picks **GCash** or
   **GOtyme** (the channel list appears only once the fee is quoted) and the
   **Scan to Pay popup** opens with the admin-managed QR. After paying in
   their wallet app they enter the **reference number + their password**,
   which only parks the payment as **SUBMITTED** — *not* paid.
3. **Studio verifies** — the merchant sees the submitted reference beside a
   password box with **Confirm / Decline** buttons. Confirm moves the payment
   to **PAID**, finalises the order as `CONFIRMED` and issues the official
   receipt; decline marks it **FAILED** and hands the customer the support
   line, who may resubmit. Verification is password-gated, so a left-open
   merchant session cannot silently commit a customer's money.

The popup is rendered by `WebUi.QrPaymentModal`, shown from Order Detail on a
**Scan to Pay / show QR again** button for a customer who closed or reloaded
it. Nothing advances on an unpaid order: `PROCESSING`, `SHIPPED` and
`DELIVERED` all refuse until the payment is `PAID`, and booking J&T requires
the waybill number (a `DELIVERED` mark checks the *stored* waybill).


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


## How you actually work on this

Every script derives its paths from its own folder, so all of these work from
any drive, in any folder, under any name. No absolute path is typed or baked in.

**Once, on a new machine**
```bat
git clone https://github.com/JamesMangao/STAR-DOM.git
cd STAR-DOM
setup.bat
```
Installs IIS Express, trusts the Supabase CA, asks for your database password
behind a masked prompt, writes the credentials file, and proves the database
answers. Safe to re-run — it skips whatever is already done.

**Start the site**
```bat
run-website.bat
```
Prints which database it chose, starts IIS Express on **8095**, opens the
browser. Sign in as `admin` / `admin123`.

**After you change code**
```bat
build.bat
```
Then just refresh the browser. If anything looks stale, restart the site:
```bat
taskkill /F /IM iisexpress.exe
run-website.bat
```

**Publish your work**
```bat
git add -A
git status
git commit -m "Why this change, in one or two sentences"
git push origin main
```
Always run `git status` after the add. It must **not** list
`tools\supabase-credentials.txt` or any `*.dump`.

**On another machine, after you pushed**
```bat
cd /d <wherever-the-repo-lives>
git pull origin main
git show --stat HEAD
```
The `git show --stat` tells you what arrived. If it includes `bin\` or `App\` or
`Code\`, restart IIS Express afterwards. If it includes `Database\*.sql`, the
schema changed — see [Which database?](#which-database).

**Show it to someone outside your machine**
```bat
share-website.bat
```
A Cloudflare tunnel; the URL it prints works on any device.


## Requirements

**On a brand-new machine, run [`setup.bat`](setup.bat) once.** It installs IIS
Express from the bundled MSI, trusts the Supabase CA in your user store, asks
for the database password behind a masked prompt (so it never appears on
screen), writes the credentials file, and then proves Supabase answers.
Everything it needs is in the repo, and every path is derived from its own
folder, so it works from any drive or folder. Re-run it any time — it skips
whatever is already done.

| Requirement | Status |
|---|---|
| **Windows 10/11** | .NET Framework 4.8 runtime is preinstalled |
| **IIS Express 10** | installed by `setup.bat` from `tools\downloads\` |
| **A PostgreSQL database** | ✅ Supabase (shared, intended) or the portable copy in `tools\pgsql` (fallback) |
| **Supabase root CA** | trusted by `setup.bat` — see [Which database?](#which-database) |

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
no system-wide change. Run it from the repo folder:

```bat
certutil -user -addstore -f Root tools\supabase\prod-ca-2021.crt
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


## Run the site

**Simplest:** double-click **`run-website.bat`** in the repo root —
it picks the database for you (Supabase if `tools\supabase-credentials.txt`
exists, otherwise the local PostgreSQL in `tools\pgsql`), starts IIS Express on
port **8095**, and opens your browser. It works from any drive or folder
because it finds its own location.

Or from a terminal, if you want to launch IIS Express yourself:

```bat
cd STAR-DOM-Web
"C:\Program Files\IIS Express\iisexpress.exe" /path:"%CD%" /port:8095 /clr:v4.0
```

Then open **http://localhost:8095** — the storefront landing page loads for
anonymous visitors, who can browse the marketplace, catalog, product pages, and
the pop-up tour schedule freely. Sign-in is only required to add to cart, check
out, or open any account page. Close the IIS Express window to stop.

> Note: always browse via `http://localhost:8095` (not `127.0.0.1:8095` — IIS
> Express only binds the `localhost` host name for this ad-hoc launch).


## Rebuilding after code changes

**One command**, from the repo root:

```bat
build.bat
```

or `build.bat Release` for a Release build. The script resolves every path
from its own folder and finds MSBuild by itself, via `vswhere`, then the
Build Tools install, then whatever is on `PATH` — so **no absolute path is
typed and none is baked in**. The repo can sit on any drive, in any folder,
under any name.

Behind the scenes it runs:

```bat
MSBuild STAR-DOM-Web.sln -t:Build -p:Configuration=Debug -p:FrameworkPathOverride=<repo>\tools\refasm\build\.NETFramework\v4.8
```

`tools\refasm` provides the .NET Framework 4.8 reference assemblies, so the
build works on a machine that has the 4.8 *runtime* but not the developer
pack. `build.bat` skips that switch when `tools\refasm` is absent and lets a
real Visual Studio supply its own. In the IDE you need neither — just open
`STAR-DOM-Web.sln`, install the **ASP.NET and web development** workload if
prompted, and press **Ctrl+F5**.

> **Never use `-t:Rebuild`.** Clean runs first and deletes the four vendored
> Npgsql dependency DLLs out of `bin\`, because nothing in the project
> references them as build outputs. `build.bat` uses `-t:Build` and `bin\`
> keeps all 16 files.

After a rebuild, refresh the browser (IIS Express picks up new `bin` DLLs
automatically; restart it if anything looks stale).


## Setting up a new machine (after cloning from GitHub)

The repo already ships everything the site needs to RUN: the compiled
`bin\STAR_DOM_Web.dll` + all vendored Npgsql DLLs, `packages\`, the
schema/seed SQL, the reference assemblies used by the build
(`tools\refasm`), and even the IIS Express installer
(`tools\downloads\iisexpress_amd64_en-US.msi`).

### Start with `setup.bat`

```bat
git clone https://github.com/JamesMangao/STAR-DOM.git
cd STAR-DOM
setup.bat
```

It walks the four steps and skips whatever is already in place:

| Step | What it does | Admin? |
|---|---|---|
| 1 | Installs IIS Express from the bundled MSI | only if not already installed |
| 2 | Trusts the Supabase CA in your **user** store | no |
| 3 | Copies the credentials template and asks for the password **behind a masked prompt** | no |
| 4 | Connects to Supabase and runs `SELECT 1` to prove it works | no |

Exit code `0` means every step passed. If it reports INCOMPLETE, fix the named
step and run it again — it is safe to re-run.

### Or do it by hand

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
     **each** machine, from the repo folder:
     ```bat
     certutil -user -addstore -f Root tools\supabase\prod-ca-2021.crt
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
  works as-is. Build only after code changes, with `build.bat`.
- **Nothing in this repo depends on where it lives.** `run-website.bat`,
  `share-website.bat`, `start-db.bat`, `build.bat`, `tools\supabase-env.bat`
  and `tools\reseed-supabase.bat` all derive their paths from `%~dp0`, so the
  repo works from any drive, folder name or location — and there is no
  absolute path to edit when you move it.
- **Render deploy needs nothing local:** push to GitHub, create a Render Web
  Service from the repo, set `SUPABASE_DB_URL` in the dashboard — Render builds
  the Docker image (Mono/XSP4) from source.
- **New machine, Supabase:** steps 1–4 above only. `tools\pgsql`, `packages\`
  and the prebuilt `bin\` all come from the clone, and `certutil -user` needs
  no administrator rights.
- `Database\legacy-mysql\` holds the original MySQL schema/seed scripts kept
  for history — the application no longer uses MySQL.
