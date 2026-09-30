# STAR:DOM build state (website only — desktop app removed on request)

Workspace root (bash cwd): `/d/STARDOM`  (Windows: `D:\STARDOM`)

## What this is now
VB.NET **ASP.NET Web Forms** website + **Supabase (PostgreSQL 15+) via Npgsql**.
The WinForms desktop app and its project folder were deleted (user request); the
shared business layers (Models/Repositories/Services) were copied into the web
project under `STAR-DOM-Web/Shared/` and the vbproj repointed to them.

The database was ported from MySQL 8.x to Supabase/PostgreSQL — see
[Database engine](#database-engine-supabasepostgresql-via-npgsql) below for the
full dialect map and the traps that bite if you forget them.

```
D:\STARDOM
├── STAR-DOM-Web.sln                 <- optional, for Visual Studio
├── Dockerfile, render.yaml          <- Render deploy (Mono/XSP4 in Ubuntu 20.04)
├── tools\                            <- build notes, refasm, scratch scripts
└── STAR-DOM-Web\                    <- the website project root
    ├── App\                         <- pages (customer/merchant/admin)
    ├── Code\                        <- web-only: Db, Session, Guard, Validators, WebUi
    ├── Shared\                      <- Models/Repositories/Services/Reports (self-contained)
    ├── Database\                    <- supabase_schema.sql + supabase_seed.sql
    │   └── legacy-mysql\            <- archived MySQL 8.x scripts (DO NOT RUN)
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
- MSBuild: `"C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe"`
- Build command (no VS installed on the machine):
  `MSBuild STAR-DOM-Web.sln -t:Rebuild -p:Configuration=Debug -p:FrameworkPathOverride=D:/STARDOM/tools/refasm/build/.NETFramework/v4.8`
  → compiles clean (0 errors) into `STAR-DOM-Web\STAR-DOM-Web\bin\STAR_DOM_Web.dll`
- ASP.NET runtime: IIS Express 10 installed at `C:\Program Files\IIS Express`
  (official MSI from download.microsoft.com GUID C/E/8/CE8D18F5-…; installer kept
  in tools/downloads). Machine also has .NET Framework 4.8 runtime + Build Tools.

## Runtime (verified end-to-end via curl against the old live MySQL/XAMPP host)
- `iisexpress.exe /path:"D:\STARDOM\STAR-DOM-Web" /port:8095 /clr:v4.0`
- Login POST → 302 → role home; all 14 app screens return 200 with seeded data
  (bella=customer, mika=merchant, admin=admin). Bind note: IIS Express ad-hoc
  binds `localhost` only — browse http://localhost:8095, NOT 127.0.0.1 (400).
- The end-to-end run above was performed against **XAMPP MySQL**, before the
  Supabase port. After the port the app needs a real PostgreSQL to exercise:
  either a Supabase project or a local `psql`. The provider swap is compile- and
  parse-verified (see Verification below) but has not yet been run against a
  live PostgreSQL server.

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
- **Time zone**: the connection sets `Time Zone=Asia/Manila` (override with
  `APP_TIMEZONE`). Timestamps are stored `TIMESTAMPTZ`, but every `Fmt.*` call
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

