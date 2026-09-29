# STAR:DOM — Merchant Art Marketplace & Pop-up Tour System (Website)

VB.NET **ASP.NET Web Forms** website + **MySQL**. This is the website version of
STAR:DOM (the desktop WinForms app was removed on request). The HTML/PNG mockups
are reproduced as web screens with the yellow/red STAR:DOM branding.

```
D:\STARDOM
├── STAR-DOM-Web.sln              <- open this in Visual Studio (optional)
└── STAR-DOM-Web
    ├── run-website.bat           <- ★ double-click this to run the site
    └── STAR-DOM-Web              <- the website project (open folder in IIS)
        ├── App\                  <- customer + merchant + admin pages
        ├── Code\                 <- web helpers (Session, Guard, Db, WebUi)
        ├── Shared\               <- Models / Repositories / Services (all VB.NET)
        ├── Database\             <- schema.sql + seed.sql (fresh-install only)
        ├── css\site.css          <- STAR:DOM design system
        └── web.config            <- MySQL connection string lives here
```

## Requirements (already set up on this machine)

| Requirement | Status |
|---|---|
| **IIS Express 10** | ✅ Installed at `C:\Program Files\IIS Express` |
| **MySQL** (XAMPP) | ✅ Running on `localhost:3306` |
| **`stardom` database** (27 tables + seed data) | ✅ Loaded |
| **.NET Framework 4.8** runtime | ✅ Present |

## Run the site

**Simplest:** double-click **`D:\STARDOM\STAR-DOM-Web\run-website.bat`** —
it starts IIS Express on port **8095** and opens your browser automatically.

Or from a terminal:

```bat
cd /d D:\STARDOM\STAR-DOM-Web
"C:\Program Files\IIS Express\iisexpress.exe" /path:"D:\STARDOM\STAR-DOM-Web\STAR-DOM-Web" /port:8095 /clr:v4.0
```

Then open **http://localhost:8095** — unauthenticated visitors are sent to the
sign-in page. Close the IIS Express window to stop.

> Note: always browse via `http://localhost:8095` (not `127.0.0.1:8095` — IIS
> Express only binds the `localhost` host name for this ad-hoc launch).

## Demo accounts (seeded in MySQL)

| Role | Username | Password | Lands on |
|---|---|---|---|
| Customer | `bella` | `customer123` | Marketplace |
| Merchant | `mika` | `merchant123` | Merchant Dashboard |
| Admin | `admin` | `admin123` | Admin Console |

## What you can do end-to-end

- **Customer:** browse categories / search the catalog, product detail, cart &
  checkout (GCash / Maya / Card / Cash on Delivery — records only, no real
  payment), order tracking, pop-up locations + reminders, commission requests
  (5-step wizard) with the full status workflow, reviews, notifications, profile.
- **Merchant (Mika):** dashboard KPIs, product & stock manager, event & booth
  manager with per-event inventory and in-person sales, commission pipeline
  (accept / decline / request clarification / offer / production), orders &
  payments management, sales reports (daily/monthly/product/category/event/payment
  charts), review moderation.
- **Admin:** user list, role reassignment, activate/suspend accounts.

All business data (products, categories, events, orders, commissions) is read
from and written to MySQL via parameterized ADO.NET (`MySql.Data`); nothing is
hardcoded.

## Security notes

- Every POST is CSRF-checked. Pages render raw `<form method="post">` markup from
  VB strings, so protection is a hand-rolled per-session token (`Code\Csrf.vb`)
  verified in one place — `Global.asax.vb Application_AcquireRequestState` — rather
  than per-page. The token rides in the `Site.master` shell form that wraps every
  App page, and is reissued on sign-in. A mismatch returns 403 and logs to
  `AppErrors` with `Context='Security'`.
- **No keys in `web.config`.** DB credentials come from the `MYSQL_URL` /
  `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` / `DB_PASSWORD` / `DB_SSLMODE`
  environment variables (`Code\Db.vb` prefers them over the connection string).
  Keep it that way — do not paste a real password into the file.
- `customErrors` is `RemoteOnly`, so stack traces are not served. The friendly
  `Error.aspx` page shows a short message and the full detail goes to the
  `AppErrors` table. Set it to `Off` only while debugging, then put it back.
- `bin\STAR_DOM_Web.dll` is committed because the Linux/Mono deploy runs the
  prebuilt binary — rebuild and commit `bin\` with any code change.

## Rebuilding after code changes

The project compiles with **MSBuild / Visual Studio**. On this machine there is
no Visual Studio IDE, so from a terminal:

```bat
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" STAR-DOM-Web.sln -t:Build -p:Configuration=Debug -p:FrameworkPathOverride=D:\STARDOM\tools\refasm\build\.NETFramework\v4.8
```

(`tools\refasm` provides the .NET Framework 4.8 reference assemblies; the real
4.8 runtime is already installed. In Visual Studio you do not need that switch —
just open `STAR-DOM-Web.sln`, install the **ASP.NET and web development**
workload if prompted, and press **Ctrl+F5**.)

After a rebuild, refresh the browser (IIS Express picks up new `bin` DLLs
automatically; restart it if anything looks stale).

## Fresh database install (only needed on a new machine)

```bat
cd /d D:\STARDOM\STAR-DOM-Web\STAR-DOM-Web\Database
"C:\xampp\mysql\bin\mysql" -u root < schema.sql
"C:\xampp\mysql\bin\mysql" -u root < seed.sql
"C:\xampp\mysql\bin\mysql" -u root < seed_products_artshop.sql
```

`seed.sql` seeds identity, categories, events, commissions and notifications;
`seed_products_artshop.sql` seeds the official 100-item ARTSHOP catalog
(products, event inventory, bundles, event sales). On an existing database you
can refresh just the catalog by re-running `seed_products_artshop.sql` alone.

Then set the connection string in `web.config` if your MySQL user/password
differs (XAMPP root here has an empty password).
