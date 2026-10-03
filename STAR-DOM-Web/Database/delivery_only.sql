-- Delivery-only migration: drop the pick-up fulfilment columns from Orders and
-- add ReceivedAt for the customer mark-as-received step.
--
-- Idempotent, and safe on a database with no orders (the shipped demo state):
-- every statement is guarded on the column actually being there.
--
-- Before running this, confirm the orders table has no pick-up rows that would
-- lose their stall:
--   SELECT count(*) FROM Orders WHERE Fulfillment = 'PICKUP';
-- If that is non-zero, migrate those to DELIVERY first.

DO $$
DECLARE
    dropped TEXT;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'orders'
          AND column_name IN ('fulfillment', 'pickupeventid',
                              'pickupcustomerconfirmed', 'pickupmerchantconfirmed')
    ) THEN
        -- The FK on PickupEventId goes with the column (DROP COLUMN CASCADE on a
        -- dependent constraint would be fine here, but dropping the named
        -- constraint first keeps the intent obvious).
        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_orders_pickupevent') THEN
            ALTER TABLE Orders DROP CONSTRAINT fk_orders_pickupevent;
        END IF;

        -- DROP COLUMN takes exactly one column per action, so the list is
        -- looped rather than comma-joined into a single ALTER.
        FOR dropped IN
            SELECT column_name
              FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = 'orders'
               AND column_name IN ('fulfillment', 'pickupeventid',
                                   'pickupcustomerconfirmed', 'pickupmerchantconfirmed')
             ORDER BY column_name
        LOOP
            EXECUTE 'ALTER TABLE Orders DROP COLUMN ' || dropped;
            RAISE NOTICE 'Dropped Orders.%', dropped;
        END LOOP;
    ELSE
        RAISE NOTICE 'Orders already delivery-only - nothing to drop.';
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'orders'
          AND column_name = 'receivedat'
    ) THEN
        ALTER TABLE Orders
            ADD COLUMN ReceivedAt TIMESTAMPTZ NULL;
        RAISE NOTICE 'Added Orders.ReceivedAt.';
    ELSE
        RAISE NOTICE 'Orders.ReceivedAt already present.';
    END IF;
END $$;