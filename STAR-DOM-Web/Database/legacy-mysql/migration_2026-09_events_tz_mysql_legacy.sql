-- STAR:DOM migration — timezone-aware pop-up events
-- Adds TimeZone column so stored event windows are explicitly Asia/Manila wall-clock.
-- Idempotent: safe to run more than once.

SET @has := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'PopUpEvents'
    AND COLUMN_NAME = 'TimeZone'
);

SET @sql := IF(@has = 0,
  'ALTER TABLE PopUpEvents ADD COLUMN TimeZone VARCHAR(32) NOT NULL DEFAULT ''Asia/Manila'' AFTER UpdatedAt',
  'SELECT ''TimeZone column already exists'' AS info');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE PopUpEvents SET TimeZone = 'Asia/Manila' WHERE TimeZone NOT IN ('Asia/Manila', '');