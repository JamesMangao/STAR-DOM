-- ------------------------------------------------------------
-- IG reference columns for dispute/resolution audit trail
-- ------------------------------------------------------------
-- The locked spec requires recording an Instagram reference with every
-- dispute resolution and refund outcome. That reference lives on the
-- order row itself (not only in the history Reason) so it can be
-- filtered and searched later.
--
-- HoldIgRef is written when the studio resolves a dispute (OpenDispute
-- writes nothing, ResolveDispute writes the IG ref here).
-- RefundIgRef is written when the studio records a refund via the
-- "Record a refund" action.
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS HoldIgRef VARCHAR(200) NOT NULL DEFAULT '';
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS RefundIgRef VARCHAR(200) NOT NULL DEFAULT '';

-- The timeline strings are fixed per the locked spec. The history rows
-- already carry the pieces; the panel just renders them in the exact
-- wording below. No new columns needed for the timeline itself.