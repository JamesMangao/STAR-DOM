# STAR:DOM build state (website only — desktop app removed on request)

Workspace root: the repo folder, wherever it is cloned. No script or documented
command hardcodes a drive or path any more, so this is not fixed to one location.

## What this is now
VB.NET **ASP.NET Web Forms** website + **Supabase (PostgreSQL 15+) via Npgsql**.
The WinForms desktop app and its project folder were deleted (user request); the
shared business layers (Models/Repositories/Services) were copied into the web
project under `STAR-DOM-Web/Shared/` and the vbproj repointed to them.

The database was ported from MySQL 8.x to Supabase/PostgreSQL — see
[Database engine](#database-engine-supabasepostgresql-via-npgsql) below for the
full dialect map and the traps that bite if you forget them.

```
<your clone folder>
├── STAR-DOM-Web.sln                 <- optional, for Visual Studio
├── Dockerfile, render.yaml          <- Render deploy (Mono/XSP4 in Ubuntu 20.04)
├── setup.bat                        <- one-time new-machine setup
├── build.bat                        <- compile; finds MSBuild and its own paths
├── start-db.bat                     <- local PostgreSQL start/stop (the launchers call it)
├── tools\                            <- build notes, refasm, scratch scripts
└── STAR-DOM-Web\                    <- the website project root
    ├── App\                         <- pages (customer/merchant/admin)
    ├── Code\                        <- web-only: Db, Session, Guard, Validators, WebUi
    ├── Shared\                      <- Models/Repositories/Services/Reports (self-contained)
    ├── Database\                    <- supabase_schema.sql + supabase_seed.sql
    │   └── legacy-mysql\            <- archived MySQL 8.x scripts (DO NOT RUN)
    ├── run-website.bat / share-website.bat <- double-click launchers (DB + IIS Express)
    ├── README.md                    <- run/build/install guide (rewritten 2026-10-01)
    ├── packages\                    <- Npgsql 4.1.10 + System.Memory / Unsafe /
    │                                   Tasks.Extensions (vendored, offline build)
    ├── bin\                         <- build output (committed on purpose, see below)
    └── css\site.css, web.config, *.aspx, Global.asax, STAR-DOM-Web.vbproj
```

Note the project root is `STAR-DOM-Web\`, **not** `STAR-DOM-Web\STAR-DOM-Web\`
— the tree was flattened in Sept 2026. The old nested path lingered in
`.gitignore` negations and in this file, which silently un-ignored nothing and
sent the Docker build looking for vendored DLLs in a directory that no longer
existed. Both are fixed.

## Toolchain (verified)
- Build: just run `build.bat` from the repo root. It derives every path from
  `%~dp0` and locates MSBuild by itself — `vswhere` first, then the Build Tools
  install, then whatever is on `PATH` — so no absolute path is needed anywhere.
  `build.bat Release` builds Release.
- Under the hood: `MSBuild STAR-DOM-Web.sln -t:Build -p:Configuration=Debug
  -p:FrameworkPathOverride=<repo>\tools\refasm\build\.NETFramework\v4.8`
  → compiles clean (0 errors) into `STAR-DOM-Web\bin\STAR_DOM_Web.dll`, leaving
  all 16 files in `bin\`.
- **Never `-t:Rebuild`.** Clean deletes the four vendored Npgsql dependency
  DLLs from `bin\` because nothing references them as build outputs.
- `tools\refasm` supplies the .NET Framework 4.8 reference assemblies so the
  build works with only the runtime installed. `build.bat` drops the override
  when that folder is absent, letting a real Visual Studio supply its own.
- ASP.NET runtime: IIS Express 10 installed at `C:\Program Files\IIS Express`
  (official MSI from download.microsoft.com GUID C/E/8/CE8D18F5-…; installer kept
  in tools/downloads). Machine also has .NET Framework 4.8 runtime + Build Tools.

## Runtime (verified end-to-end via curl, October 2026)
- `iisexpress.exe /path:"<repo>\STAR-DOM-Web" /port:8095 /clr:v4.0`
  (double-click `run-website.bat`, which resolves that path itself and boots the
  database first)
- Login POST → 302 → role home; marketplace + merchant dashboard return 200 with
  seeded data. Bind note: IIS Express ad-hoc binds `localhost` only — browse
  http://localhost:8095, NOT 127.0.0.1 (400).

### Local PostgreSQL (added October 2026 — replaces the XAMPP MySQL dev box)
The Supabase port left the app with no runnable database on this machine: the
web.config fallback points at `localhost:5432` and nothing listened there, so
every page that touched data died with "No connection could be made because the
target machine actively refused it". Fixed with a portable PostgreSQL install:

- **Binaries**: PostgreSQL 16.4 (EDB `windows-x64-binaries` zip) unzipped to
  `tools\pgsql\` — no installer, no admin rights, no Windows service.
- **Data dir**: `tools\pgdata\`, initialised with `initdb -U postgres` and the
  password `postgres`, matching the web.config fallback string exactly.
- **Database**: `stardom`, loaded from `Database/supabase_schema.sql` +
  `Database/supabase_seed.sql` (28 tables, 100 products, seeded users).
- **Start/stop**: `start-db.bat` (repo root) — `start-db.bat` starts,
  `start-db.bat stop` stops, `start-db.bat status` probes. `run-website.bat` and
  `share-website.bat` call it automatically before launching IIS Express.
  Both directories are git- and docker-ignored.
- The app itself still resolves its connection string the Supabase-first way
  (`SUPABASE_DB_URL` env → web.config → localhost default); nothing about the
  deploy path changed. Local dev simply lands on the web.config string.

### Npgsql's full dependency chain under .NET Framework (added October 2026)
Running against real PostgreSQL on .NET Framework exposed four missing/wrong
links in the Npgsql 4.1.10 dependency chain that Mono (the Render deploy)
masked. All are fixed in `web.config` + `bin\`:

1. `System.Memory` — bin\ ships 4.0.1.2, but Npgsql references **4.0.1.1**,
   outside the old `0.0.0.0-4.0.0.0` redirect range → 0x80131040. Range widened
   to `0.0.0.0-4.0.1.2` (same fix for `System.Threading.Tasks.Extensions`,
   referenced at 4.0.3.0 by Npgsql, now covered by the `0.0.0.0-4.2.0.1` range).
2. `System.Numerics.Vectors` **4.1.4.0** — System.Memory depends on it; the
   4.8 GAC only has 4.0.0.0. DLL copied into `bin\` + redirect added.
3. `System.Buffers` **4.0.3.0** — same story, same fix.
4. `Microsoft.Bcl.AsyncInterfaces` **1.0.0.0** + `System.Text.Json` **4.0.0.0**
   (from the System.Text.Json 4.6.0 net461 package) — Npgsql 4.1.10's net461
   build hard-references both; neither shipped in `bin\`, so the first
   connection open died in `Npgsql.TypeMapping.GlobalTypeMapper..cctor` with a
   ReflectionTypeLoadException. DLLs copied into `bin\` at exactly those
   versions. Do NOT upgrade Bcl.AsyncInterfaces past 1.x here: the Mono
   (Linux/Render) side has no binding redirects and cannot bind 6.0+/8.0+.

The merchant seed password: all three merchant accounts (mika/renzo/puffu)
share the admin's PBKDF2 hash, so `mika` signs in with **admin123**. The README
used to show `merchant123` — corrected 2026-10-01 (the seed itself still shares
the admin hash; rehashing in a seed pass is optional).

## Database engine: Supabase/PostgreSQL via Npgsql
Ported from MySQL 8.x. `Code/Db.vb` is the only file that names the provider;
every other file calls `Db.Exec/Query/Rows/Scalar*` with plain SQL and
`Db.P("@name", value)` parameters.

- **Provider**: `Npgsql` 4.1.10, `lib/net461` (not netstandard2.0). The native
  net461 IL matters — the same assembly has to load under Mono/XSP4 in the Linux
  container, and a netstandard shim adds a resolution hop Mono handles poorly.
  Vendored under `packages/`; there is no `nuget restore` step in the Dockerfile.
- **Also vendored** (Npgsql's own `net461` dependency group requires them):
  `System.Memory` 4.5.5, `System.Runtime.CompilerServices.Unsafe` 6.0.0,
  `System.Threading.Tasks.Extensions` 4.5.4. `System.ValueTuple` was already
  there. Dropping any one of them breaks the sync data-reader path at runtime,
  not at build time.
- **Connection string** is resolved in this order, so a deployed host never needs
  a password in source control: `SUPABASE_DB_URL` / `POSTGRES_URL` (used
  verbatim) → `SUPABASE_URL` + `SUPABASE_DB_PASSWORD` (derives
  `db.<ref>.supabase.co` from the project URL) → discrete `DB_HOST`/`DB_PORT`/
  `DB_NAME`/`DB_USER`/`DB_PASSWORD` → `web.config` → a localhost default.
  The project URL host is **not** a Postgres endpoint; only its `db.*` sibling
  serves the wire protocol. That is why the derivation exists.
- **Time zone**: the connection sets `Timezone=Asia/Manila` (override with
  `APP_TIMEZONE`). Npgsql spells this keyword `Timezone`, with no space: it is
  the name of a property on `NpgsqlConnectionStringBuilder`, and the
  connection string is parsed against that class. `Time Zone` looks like the
  right English but is rejected with `ArgumentException: Keyword not
  supported: time zone`, which surfaces as an unhandled error on the first
  page that touches the database rather than as a configuration error.
  Timestamps are stored `TIMESTAMPTZ`, but every `Fmt.*` call
  formats a local `DateTime`. Without this, Npgsql hands back UTC and the UI
  silently shows the wrong wall-clock time.
- **`Db.ExecIdentity`** appends `RETURNING Id` and reads it with
  `ExecuteScalar()` in one round trip, replacing `SELECT LAST_INSERT_ID()`.
  Every primary key in the schema is a plain serial `Id`, so the generic clause
  is safe for all 13 call sites; it strips a trailing `;` and skips appending if
  the statement already has its own `RETURNING`.
- **`RowReader.AsBool`** accepts a real `Boolean` *and* a 0/1, so it reads
  correctly against either the new `BOOLEAN` columns or the old `TINYINT(1)`.
- `Db.LogError` creates its `AppErrors` table on demand, so that DDL is
  PostgreSQL too (`SERIAL`, `TIMESTAMPTZ`, and a separate
  `CREATE INDEX IF NOT EXISTS` — PostgreSQL has no inline `INDEX` in
  `CREATE TABLE`).

### Dialect map applied
| MySQL | PostgreSQL | Count |
|---|---|---|
| `AUTO_INCREMENT` | `SERIAL PRIMARY KEY` | 28 tables |
| `TINYINT(1)` | `BOOLEAN` | 17 columns |
| `DATETIME` | `TIMESTAMPTZ` | all date columns |
| `ON UPDATE CURRENT_TIMESTAMP` | `BEFORE UPDATE` trigger | 6 `UpdatedAt` columns |
| `LAST_INSERT_ID()` | `RETURNING Id` | `Db.ExecIdentity` |
| `IF(a,b,c)` | `CASE WHEN a THEN b ELSE c END` | 5 statements |
| `DATE_FORMAT(c,'%Y-%m')` | `TO_CHAR(c,'YYYY-MM')` | 2 |
| `DATE_SUB(CURDATE(), INTERVAL @d DAY)` | `DATE_TRUNC('day',NOW()) - make_interval(days => @d)` | 2 |
| `DATE_SUB(NOW(), INTERVAL 72 HOUR)` | `NOW() - INTERVAL '72 hours'` | 1 |
| `DATE(c)` | `DATE_TRUNC('day', c)` | 2 |
| `HOUR(c)` | `EXTRACT(HOUR FROM c)` | 2 |
| `UPDATE t1 JOIN t2 … SET` | `UPDATE t1 … FROM t2 WHERE` | 1 (`RestoreOrderStock`) |
| `ON DUPLICATE KEY UPDATE` | `ON CONFLICT (…) DO UPDATE SET … = EXCLUDED.…` | seed file |
| inline `INDEX` in `CREATE TABLE` | separate `CREATE INDEX IF NOT EXISTS` | `AppErrors` |

### Three traps — two of them fail silently
1. **`BOOLEAN` is not an integer.** PostgreSQL has no `boolean = integer`
   operator, so `WHERE IsActive = 1` raises *"operator does not exist"* rather
   than returning wrong rows — at least that one is loud. The dangerous half is
   the parameter: `Db.P("@a", If(isActive, 1, 0))` sends CLR `Integer` as `int4`,
   and PostgreSQL refuses to store `int4` in a `boolean` column. 22 such sites
   became `Db.P("@a", isActive)` and 29 comparisons became `= TRUE`/`= FALSE`.
   **Never reintroduce the `If(flag, 1, 0)` idiom** — it type-checks fine in VB.
2. **`@name` placeholders are fine.** Npgsql accepts `@name` as well as `$1` and
   `:name`, so every existing call site and SQL string worked unchanged. This is
   why the port is small. It also means Npgsql's named-parameter parser is live:
   a `::type` cast inside SQL is the one construct that can confuse it. There are
   currently none — keep it that way, or use `CAST(x AS type)`.
3. **Multi-table `UPDATE` does not exist.** MySQL's
   `UPDATE Products p JOIN OrderItems oi … SET p.StockQuantity = …` is not valid
   PostgreSQL; the joined relation moves into `FROM` and the `SET` expressions
   are evaluated against the old row. This is in
   `OrderRepository.RestoreOrderStock`. The VB compiler accepts either form, so
   this class of bug is invisible until it runs against a live database.

### Verification
Because none of the above is caught by the compiler, the SQL is checked
mechanically against the real PostgreSQL grammar via `pglast` (libpg_query):
- `Database/supabase_schema.sql` — 81 statements parse
- `Database/supabase_seed.sql` — 37 statements parse
- 190 statements extracted from VB string literals parse (`@name` rewritten to
  `$n` first, since only `$n` parses)
- 18 queries assembled at runtime from `BaseSelect` / `OrderSelect` /
  `SelectSql` / `ReceiptSelect` parse after resolving those constants
- The 8 archived MySQL scripts were confirmed to **fail** parsing, which is how we
  know they are MySQL-only and cannot be run by accident.

`MySql.Data` is fully removed: no `MySql.*` type references remain, the
`MySql.Data` assembly reference is gone from the vbproj, and `MySql.Data.dll` no
longer appears in `bin\`.

## Legacy MySQL scripts
`Database/legacy-mysql/` holds the 8 original MySQL 8.x scripts, renamed with a
`_mysql_legacy.sql` suffix and a `README.md` documenting the port. They are kept
for provenance only. `Database/` itself now contains exactly two files:
`supabase_schema.sql` and `supabase_seed.sql`.

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

## Schema ordering gotcha (still valid)
`EventSales` has an FK to `Orders`, so `Orders` must be created first in
`supabase_schema.sql`. MySQL reported this as errno 150; PostgreSQL reports
`relation "orders" does not exist`. Keep that ordering.

## Row level security (added — backend-only posture, do not "fix" it)
`supabase_schema.sql` enables RLS on all 28 tables and installs a single
permissive policy per table for the backend roles only. The app connects as the
table owner over a direct Npgsql connection, so it is unaffected (owners bypass
RLS). The point is to close the Supabase Data API / PostgREST path: with RLS on
and no `anon`/`authenticated` policy, the publishable key sees zero rows and
cannot write, so a leaked key cannot read customer or commission data. The
policy loop checks whether the `service_role` role exists so the script also
runs against a plain local PostgreSQL.

## Conventions that still apply
- All SQL is parameterized; passwords PBKDF2-hashed; roles guard page access
  via `Code/Guard.vb` + `Code/Helpers/Session.vb`.
- Never hardcode business data — Categories/Products/Events/etc. come from the
  database.
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

## Transactions (added — do not remove or weaken)
Multi-step writes used to auto-commit one statement at a time, because every
`Db.*` call opened and disposed its own connection. A failure halfway through
left partial rows behind (an order with items inserted and stock taken but the
cart never cleared). `Db.InTransaction` now pins a whole block to one connection
and one transaction.

- `Code/Db.vb` — `InTransaction(Of T)(work As Func(Of T))` opens a connection,
  commits on normal return, rolls back on any throw, and always clears ambient
  state in a `Finally`. A nested call joins the outer transaction instead of
  opening a second connection, so the outer block still rolls everything back.
- Ambient connection/transaction are held in `[ThreadStatic]` module variables —
  ASP.NET serves requests concurrently, so a plain static would leak one
  request's transaction into another's. This is safe here **only because the app
  is entirely synchronous** (no `Async`/`Await`/`Task.Run` anywhere). If async is
  ever introduced, these must become `AsyncLocal` or be passed explicitly.
- `Exec`, `ExecIdentity`, `Query` and `RawScalar` route through
  `ResolveConnection()` and attach `cmd.Transaction` when a transaction is open,
  and dispose the connection only when they opened it themselves.
- **To roll back, let the exception escape the delegate.** A `Catch` *inside*
  the delegate that returns normally commits whatever was written so far. The
  business-rule failures that need a rollback (stock short, sale rejected) throw
  a small private exception type and are caught *outside* `InTransaction`.
- Best-effort side effects — receipt issuing and notifications — deliberately
  run **after** the commit, so a failure there can never destroy an order the
  customer already placed.

### Stock decrements must be conditional
A pre-flight "is there enough stock?" read is only a courtesy message; it cannot
prevent overselling because another order can take the unit in between. The
authoritative guard is a conditional UPDATE that the caller checks:

```sql
UPDATE Products SET StockQuantity = StockQuantity - @q, SoldCount = SoldCount + @q
 WHERE Id = @p AND StockQuantity >= @q     -- 0 rows affected = someone beat us
```

`OrderService.Checkout` throws `StockShortageException` on 0 rows so the whole
checkout rolls back. `EventRepository.RecordSale` additionally selects the
inventory row `FOR UPDATE` to hold a row lock for the rest of the
transaction, and guards its UPDATE the same way. (`FOR UPDATE` is valid in both
MySQL and PostgreSQL, so it needed no change.)

## Secrets and error pages
- No keys in `web.config`. The Supabase publishable/secret keys that were there
  were removed and are still not read by any code — set them as environment
  variables if a future feature needs them. Real database credentials come from
  the environment: `Code/Db.vb` prefers `SUPABASE_DB_URL` / `POSTGRES_URL`, then
  `SUPABASE_URL` + `SUPABASE_DB_PASSWORD`, then `DB_HOST` / `DB_PORT` /
  `DB_NAME` / `DB_USER` / `DB_PASSWORD`, and only falls back to the
  `web.config` connection string (which is a local-development placeholder).
  `render.yaml` declares the same variables.
- `customErrors` is `RemoteOnly`, not `Off`. Flip to `Off` only while debugging
  locally, then put it back — `Off` on the Render host leaks stack traces.
  Detailed errors are still written to the `AppErrors` table either way.
- Session cookie is `httpOnly` via `<httpCookies>`. `sameSite` is deliberately
  omitted: it is a .NET 4.7.2+ attribute and the Linux/Mono (xsp4) deploy in the
  Dockerfile may reject the whole config on it. `requireSSL` is `false` only
  because local dev is http://localhost — set it `true` behind HTTPS.
- `bin\` is committed on purpose. The Dockerfile builds with `msbuild` **without**
  a `|| true` — it is deliberately configured to fail the deploy on a compiler
  error rather than boot XSP4 against a half-built output — but the prebuilt
  DLL is still tracked so the host has a working binary. **Rebuild and commit
  `bin\`** after any code change; the vendored `Npgsql.dll`,
  `System.Memory.dll`, `System.Runtime.CompilerServices.Unsafe.dll` and
  `System.Threading.Tasks.Extensions.dll` must be committed too, because
  `.dockerignore` keeps `packages/` and the image has no restore step.


## Capstone workflow overhaul (2026-10-01)
Online-only ordering per the real capstone flow. Live DB + `supabase_schema.sql`
both carry the new Orders columns (`Fulfillment VARCHAR(10) DEFAULT 'DELIVERY'`,
`PickupEventId INT NULL -> PopUpEvents`, `PickupCustomerConfirmed`/`PickupMerchantConfirmed`
BOOLEAN; index on `PickupEventId` omitted, FK `FK_Orders_PickupEvent` added).

- **Checkout** (`App/Checkout.aspx.vb`): Delivery vs Pick-up radios; pick-up shows a
  stall dropdown sourced from `EventService.ListUpcoming()` (ended stalls drop out via
  the derived status), hours text inline. Pick-up hides the address field (JS disables
  it so it never posts), zeroes the shipping fee, and the summary recalculates live
  (subtotal, bundle savings, shipping, total). Card removed — GCash/Maya/COD only.
  Place Order icon fixed (`shopping_bag_checkout` ligature never existed; now `lock`).
- **OrderService.Checkout**: validates fulfillment; pick-up requires an event whose
  derived status is NOW OPEN/UPCOMING, stores the stall as the address of record,
  skips the Shipping row, writes `Fulfillment`/`PickupEventId`, and applies the bundle
  discount to `DiscountAmount`/total. Order number placeholder shortened to
  `SD-TMP-<20 hex>` — the old 47-char `SD-PLACEHOLDER-<32 hex>` blew past
  `OrderNumber VARCHAR(40)` (pre-existing bug, surfaced on first checkout).
- **Bundles** (`CartRepository.ListBundleGroups`, `CartService.BundleDiscount/BundleNote`):
  deal parsed from the bundle name "(N for M)" against `BundleItems` membership —
  Stickers 4-for-₱100, Button pins 3-for-₱100. Complete groups only; leftover units
  stay at list price. Cart shows savings + estimated total + the bundle legend;
  checkout repeats it.
- **Payment confirmation is password-gated** (`OrderService.ConfirmPayment(order, ref, password)`):
  verifies against a fresh `UserRepository.GetById` hash, not the session copy.
  GCash/Maya require the e-wallet reference number; COD/pay-on-claim need only the
  password (reference auto-generated). Customer "I've Paid" form and the merchant
  "Confirm pay" inline form are POSTs. Merchant can also record COD cash this way.
- **Pick-up claim**: `ConfirmPickup(orderId, customerSide)` sets one flag per side;
  when BOTH land the order closes DELIVERED + PAID + receipt. Customer button on
  OrderDetail ("Confirm order received"), merchant button ("Confirm hand-over") on
  Merchant Orders, which also shows the claim state per order. `UpdateOrderState`
  refuses SHIPPED/DELIVERED for pick-up orders.
- **J&T delivery status**: merchant books with a tracking number ("Book J&T" inline
  form on PROCESSING rows; blank auto-generates `JTyyMMddHHmm`). OrderDetail shows the
  courier sentence from `Order.DeliveryStatusLine` ("scheduled for booking" → "booked
  with J&T Express — tracking number X" + link to the J&T tracker → "delivered
  successfully"). `OrderRepository.OrderSelect` LEFT JOINs Shipping + PopUpEvents so
  `TrackingNumber`/`PickupEventName`/`PickupHoursText` map onto the model;
  `UpdateShipping` never clobbers a booked tracking number with an empty value.
- **Removed**: merchant POS sale form + handler (EventEdit; nav label now
  "Products & Stock"), marketplace nav group + header cart icon for
  `Session.CanManageStore` users, LBC everywhere (footer chip, Register), Google Maps
  button and SMS/App reminders (PopupLocations), "live sketch" mentions (Login,
  PopupLocations tags/guidelines/stamp card, CommissionHub), commission slot
  starting-price/deposit display (slot card now shows Turnaround/Formats/"Claim via
  Delivery only"). Commission Hub + Request pages note that commissioned products are
  delivery-only.
- **Pre-existing bugs fixed en route**: `Notifications` INSERTs wrote integer `0` into
  the boolean `IsRead` (42804 — every notification since the PostgreSQL migration
  failed; checkout looked broken because the notify threw after the order committed);
  `Convert.ToString(Request.Form(x))` yields Nothing for a missing field (pickup
  checkouts would violate `notes` NOT NULL) — coerced to ""; `Response.Redirect(…, True)`
  ThreadAbortExceptions are no longer swallowed by the generic catches on
  Checkout/OrderDetail/Merchant Orders (the redirect now stands).
- Verified end-to-end on the live DB: bundle discount (₱20 per complete 4-sticker
  group), pick-up checkout with no fee, GCash ref+password rejection cases (wrong
  password, missing ref), COD password-only confirm, two-sided claim → DELIVERED +
  receipt, J&T booking + tracking persistence through DELIVERED, staff nav/cart
  hiding, and the add-to-cart toast redirect.
- Bundle deal visibility (2026-10-01 follow-up): Catalog page shows the active-deal
  banner ("Bundle deals — applied automatically: …"); Product pages of bundle members
  show "Bundle deal: any N for ₱M" via `CartService.BundleLabelForProduct`. Pricing
  itself stays automatic at cart/checkout (verified: 4 × ₱30 sticker → ₱120 − ₱20 = ₱100).

## Repo restructure finished (2026-10-01)
The single-folder layout is now complete. `run-website.bat` + `share-website.bat`
moved from the repo root into `STAR-DOM-Web\` (git mv, history preserved); their
paths rewritten for the new location (`SITE=%~dp0`, DB helper via
`..\start-db.bat` — start-db.bat stays at the root because it drives the
gitignored `tools\pgsql`/`tools\pgdata`). The Dockerfile's `tools/vbnc-shim.sh`
+ `tools/xsp-warmup.sh` dependencies are untouched and still tracked at root.
`STAR-DOM-Web\README.md` rewritten to match reality: PostgreSQL/Npgsql stack,
final tree, correct run/rebuild/DB-install commands, fixed demo credentials
(mika = admin123), bundle/pick-up/password-confirm features, and the
`Database\legacy-mysql\` note. Nothing at the repo root belongs inside the site
folder anymore: root keeps only sln, Dockerfile, render.yaml, .git*/.dockerignore,
start-db.bat, tools\ and the user's .docx notes.
- Launcher fix (2026-10-01): the bats' new `set "SITE=%~dp0"` left a trailing
  backslash which broke IIS Express's `/path:"...\"` argument (silent exit) —
  now stripped via `set "SITE=%SITE:~0,-1%"`. Verified by executing
  `STAR-DOM-Web\run-website.bat` for real: DB check, IIS Express launch and
  HTTP 200 all pass. The bats briefly reappeared at the repo root (cut-paste
  back while testing?) — restored to STAR-DOM-Web\ per the target layout.

## Portable PostgreSQL committed to the repo (2026-10-01)
`tools\pgsql\bin + lib + share` (~119 MB, 1,627 files) are now TRACKED so a
fresh clone runs the site with zero downloads. Still excluded by .gitignore:
pgAdmin 4, include/, symbols/ (not needed to run the DB) and `tools\pgdata\`
(the live data dir). Largest single file: icudt67.dll 27 MB — under GitHub's
50 MB warning / 100 MB hard limits. `.dockerignore` still excludes all of
`tools\pgsql` — the Render image uses Supabase and never runs the local server.
`start-db.bat` gained a one-time auto-bootstrap: initdb when `tools\pgdata` is
missing (user postgres / pw postgres, scram-sha-256), then createdb + load
`supabase_schema.sql` + `supabase_seed.sql` when the `stardom` database is
absent. Verified end-to-end on an isolated instance (port 5433, temp data dir):
initdb → createdb → schema (28 tables) → seed → products=100, users=9,
bundles=3. The live 5432 instance was untouched.

## Two-role consolidation: ADMIN = the merchant (2026-10-01)
STAR:DOM is a single-owner brand, so the roles are now exactly: CUSTOMER and
ADMIN (the owner, who is also the merchant/artist). MERCHANT remains as a
legacy alias, honoured everywhere by the guards.
- `Session.IsMerchant` now returns true for ADMIN too (so `CanManageStore`,
  `Guard.RequireMerchant`, the Merchant Studio nav and every merchant page
  work for the owner without any other change).
- Owner notifications: OrderService.NotifyRole("MERCHANT", ...) calls switched
  to "ADMIN" (new order, customer pick-up confirm). Registration still always
  creates CUSTOMER.
- Admin Users page: KPIs are now TOTAL USERS / CUSTOMERS / STORE OWNERS-STAFF,
  and the change-role dropdown offers only CUSTOMER and ADMIN.
- Live DB migration: mika/renzo/puffu moved MERCHANT -> ADMIN (all products
  keep their MerchantId ownership; commission slots intact). Role descriptions
  updated.
- Seed SQL updated to match (legacy note on the MERCHANT row; demo logins
  comment now says the owner account is admin/admin123).
- Latent bug fixed en route: CommissionService.PrimaryMerchantId used MySQL's
  FIELD() which PostgreSQL does not have (CommissionRequest would 500 on a
  fresh DB) - rewritten as CASE WHEN, now also filters Status='ACTIVE' and
  prefers the ADMIN account. UserRepository.ListMerchants (commission slot
  cards) now also filters Status='ACTIVE'.
- Verified: mika (now ADMIN) logs in straight to Merchant Dashboard and can
  open Dashboard/Admin Users/CommissionRequest (200s); the role dropdown shows
  only CUSTOMER/ADMIN; a fresh bella order generated "New order" notifications
  landing on ADMIN users.

## cloudflared vendored for share-website.bat (2026-10-01)
The other machine hit "cloudflared was not found" — the tunnel binary was not
in the repo and not installed there. Fixed by vendoring the official
cloudflared-windows-amd64.exe 2026.9.3 (52.8 MB) at `tools\cloudflared\` and
rewriting the share-website.bat locator: repo copy first, then PATH, then
Program Files — with sequential `goto :cfd_found` checks instead of nested
parentheses (the old block read %CFD% at parse time, a latent batch bug that
made the Program Files fallback unreliable). 52.8 MB exceeds GitHub's 50 MB
advisory but is well under the 100 MB hard limit (expect a push warning).
Tunnel verified live: quick tunnel issued a trycloudflare.com URL against the
local 8095 site.
