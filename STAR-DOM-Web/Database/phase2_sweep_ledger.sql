-- ------------------------------------------------------------
-- Delivery sweep + studio earnings ledger (additive migration)
-- ------------------------------------------------------------
-- The same statements live in supabase_schema.sql, so a fresh install gets them
-- from the bootstrap. This file exists for a database that is already running:
-- CREATE TABLE IF NOT EXISTS is a no-op on an existing install, so the bootstrap
-- never re-adds anything, and a live database would otherwise be missing the
-- table the new code writes to.
--
-- Everything here is ADDITIVE and IDEMPOTENT: one new table, one new index, one
-- new nullable column. No existing row is read, rewritten or deleted, so running
-- it twice (or on a database that already has the table) changes nothing.

-- The studio's earning on one order. One row per order, held while the order sits
-- in DELIVERED or HELD, released when the customer confirms receipt, voided by a
-- refund. A refund after release also sets Reversed, so a clawback is visible as
-- a reversal rather than as a row that never existed.
CREATE TABLE IF NOT EXISTS OrderCommissions (
  Id SERIAL PRIMARY KEY,
  OrderId INT NOT NULL,
  Amount DECIMAL(12,2) NOT NULL DEFAULT 0,
  Basis VARCHAR(20) NOT NULL DEFAULT 'GOODS_TOTAL',
  Status VARCHAR(20) NOT NULL DEFAULT 'HELD',
  Reversed BOOLEAN NOT NULL DEFAULT FALSE,
  HeldAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ReleasedAt TIMESTAMPTZ NULL,
  VoidedAt TIMESTAMPTZ NULL,
  Reason VARCHAR(500) NOT NULL DEFAULT '',
  UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
  -- UNIQUE(OrderId) is what makes accrual idempotent: the delivery sweep is
  -- retried and re-run, and a second row for one order would double the books.
  CONSTRAINT UK_OrderCommissions_Order UNIQUE (OrderId),
  CONSTRAINT FK_OrderCommissions_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IDX_OrderCommissions_Status ON OrderCommissions (Status);

-- A refunded receipt keeps the OR number, the issue date and UNIQUE(PaymentId);
-- this stamp is what says the money is no longer held.
ALTER TABLE Receipts ADD COLUMN IF NOT EXISTS RefundedAt TIMESTAMPTZ NULL;

-- ------------------------------------------------------------
-- Row level security for the new table
-- ------------------------------------------------------------
-- The bootstrap loop at the end of supabase_schema.sql enables RLS on every
-- public table, but it has already run on a live database -- so a table created
-- after it would be the one table the PostgREST path could still read. Same
-- backend-only posture, same role detection, spelled out here for this table.
DO $do$
DECLARE
    backend_roles TEXT;
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'service_role') THEN
        backend_roles := 'service_role, postgres';
    ELSE
        backend_roles := 'postgres';
    END IF;
    EXECUTE 'ALTER TABLE public.OrderCommissions ENABLE ROW LEVEL SECURITY';
    EXECUTE 'DROP POLICY IF EXISTS ordercommissions_backend_access ON public.OrderCommissions';
    EXECUTE format(
        'CREATE POLICY %I ON public.OrderCommissions FOR ALL TO %s USING (true) WITH CHECK (true)',
        'ordercommissions_backend_access', backend_roles);
END
$do$;
