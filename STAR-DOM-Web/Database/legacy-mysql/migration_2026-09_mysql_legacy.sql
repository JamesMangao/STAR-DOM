-- ============================================================
-- STAR:DOM — live database migration (2026-09)
-- Adds missing FK/workload indexes and utf8mb4 alignment.
-- Idempotent: safe to re-run.
-- Apply with: mysql -u root stardom < migration_2026-09.sql
-- ============================================================

USE stardom;

DROP PROCEDURE IF EXISTS add_index_if_missing;
DELIMITER $$
CREATE PROCEDURE add_index_if_missing(IN tbl VARCHAR(64), IN idx VARCHAR(64), IN coldef VARCHAR(255))
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE() AND table_name = tbl AND index_name = idx
    ) THEN
        SET @ddl = CONCAT('CREATE INDEX ', idx, ' ON ', tbl, ' (', coldef, ')');
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

CALL add_index_if_missing('AppErrors', 'IDX_AppErrors_Created', 'CreatedAt');
CALL add_index_if_missing('Categories', 'IDX_Categories_Parent', 'ParentId');
CALL add_index_if_missing('ProductImages', 'IDX_ProductImages_Product', 'ProductId, IsPrimary');
CALL add_index_if_missing('ProductVariants', 'IDX_ProductVariants_Product', 'ProductId');
CALL add_index_if_missing('BundleItems', 'IDX_BundleItems_Bundle', 'BundleId');
CALL add_index_if_missing('BundleItems', 'IDX_BundleItems_Product', 'ProductId');
CALL add_index_if_missing('CartItems', 'IDX_CartItems_Product', 'ProductId');
CALL add_index_if_missing('OrderItems', 'IDX_OrderItems_Product', 'ProductId');
CALL add_index_if_missing('EventSales', 'IDX_EventSales_Product', 'ProductId');
CALL add_index_if_missing('EventSales', 'IDX_EventSales_Order', 'OrderId');
CALL add_index_if_missing('CommissionReferenceImages', 'IDX_CommRefs_Commission', 'CommissionId');
CALL add_index_if_missing('CommissionMessages', 'IDX_CommMessages_Commission', 'CommissionId');
CALL add_index_if_missing('CommissionMessages', 'IDX_CommMessages_Sender', 'SenderId');
CALL add_index_if_missing('CommissionStatusHistory', 'IDX_CommHistory_Commission', 'CommissionId');

DROP PROCEDURE IF EXISTS add_index_if_missing;

-- Align pre-existing tables with the utf8mb4 schema default (no-op if already aligned).
ALTER TABLE Categories CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Products CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE ProductImages CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE ProductVariants CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Cart CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE CartItems CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE WishlistItems CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE StoreLocations CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE PopUpEvents CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE EventInventory CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Orders CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE EventSales CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE OrderItems CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Payments CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Receipts CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Shipping CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Reviews CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Commissions CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE CommissionReferenceImages CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE CommissionMessages CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE CommissionStatusHistory CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE Notifications CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE AppErrors CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;