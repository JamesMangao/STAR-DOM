# Archived MySQL 8.x scripts — reference only, DO NOT RUN

These are the original STAR:DOM database scripts. The application no longer
talks MySQL: `Code\Db.vb` uses **Npgsql** and connects to **Supabase
(PostgreSQL 15+)**. The MySQL `MySql.Data` reference has been removed from
`STAR-DOM-Web.vbproj`.

They are kept for provenance and to document what the port changed. None of them
will run against PostgreSQL — every file here uses `USE stardom`,
`ON DUPLICATE KEY UPDATE`, backtick identifiers, `TINYINT(1)`, `AUTO_INCREMENT`,
`ENGINE=InnoDB`, and `utf8mb4` collations. All of them were verified to fail
PostgreSQL parsing.

## What replaced them

| Purpose | Live file |
|---|---|
| Schema (28 tables) | [`../supabase_schema.sql`](../supabase_schema.sql) |
| Demo / seed data | [`../supabase_seed.sql`](../supabase_seed.sql) |

## Loading the live schema

Supabase SQL Editor, or from a shell:

```sh
psql "$SUPABASE_DB_URL" -f supabase_schema.sql
psql "$SUPABASE_DB_URL" -f supabase_seed.sql
```

Both are idempotent (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`,
`CREATE OR REPLACE FUNCTION`, `DROP TRIGGER IF EXISTS`), so re-running them is
safe.

## Dialect changes made during the port

| MySQL | PostgreSQL | Where it mattered |
|---|---|---|
| `AUTO_INCREMENT` | `SERIAL PRIMARY KEY` | 28 tables |
| `TINYINT(1)` | `BOOLEAN` | 17 columns — queries must compare to `TRUE`/`FALSE` and bind a real `Boolean`, not `1`/`0` |
| `DATETIME` | `TIMESTAMPTZ` | app reads these as `Asia/Manila` |
| `ENGINE=InnoDB` | dropped (FKs enforced by default) | all tables |
| `ON UPDATE CURRENT_TIMESTAMP` | `BEFORE UPDATE` trigger `trg_star_dom_touch_updated_at()` | 6 `UpdatedAt` columns (Users, Products, Cart, PopUpEvents, Orders, Commissions) |
| `LAST_INSERT_ID()` | `RETURNING Id` | `Db.ExecIdentity` |
| `IF(a,b,c)` | `CASE WHEN a THEN b ELSE c END` | 5 statements |
| `DATE_FORMAT(x,'%Y-%m')` | `TO_CHAR(x,'YYYY-MM')` | monthly sales report |
| `DATE_SUB(CURDATE(), INTERVAL n DAY)` | `DATE_TRUNC('day', NOW()) - make_interval(days => n)` | revenue + monthly reports |
| `DATE_SUB(NOW(), INTERVAL 72 HOUR)` | `NOW() - INTERVAL '72 hours'` | hourly traffic report |
| `DATE(col)` | `DATE_TRUNC('day', col)` | daily sales report |
| `HOUR(col)` | `EXTRACT(HOUR FROM col)` | hourly traffic report |
| `UPDATE t1 JOIN t2 ... SET` | `UPDATE t1 ... FROM t2 WHERE` | `OrderRepository.RestoreOrderStock` |
| inline `INDEX` in `CREATE TABLE` | separate `CREATE INDEX IF NOT EXISTS` | `AppErrors` |

## Security note

`supabase_schema.sql` enables **row level security** on every table with a
backend-only policy. That closes the Supabase Data API / PostgREST path: the
`anon` and `authenticated` keys see zero rows and cannot write, so a leaked
publishable key cannot read customer or commission data. The application itself
connects as the table owner over a direct Npgsql connection, so it is
unaffected — all authorisation lives in the VB.NET layer (role checks,
store-manager gating, CSRF tokens). No Supabase client-side code exists in this
project.
