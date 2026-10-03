-- ============================================================
-- STAR:DOM — Artisan Marketplace & Pop-up Tour System
-- PostgreSQL 15+ / Supabase schema (ported from the MySQL 8.x original)
--
-- Run this in the Supabase SQL Editor, or against the database directly:
--     psql "$SUPABASE_DB_URL" -f supabase_schema.sql
-- Load the demo data afterwards with supabase_seed.sql.
--
-- Porting notes (MySQL -> PostgreSQL):
--   * AUTO_INCREMENT        -> SERIAL PRIMARY KEY
--   * TINYINT(1) flags      -> BOOLEAN, so queries must compare to TRUE/FALSE
--                            and must bind a real Boolean, not 1/0
--   * DATETIME             -> TIMESTAMPTZ (the app reads these as Asia/Manila)
--   * ENGINE=InnoDB        -> dropped; referential integrity is on by default
--   * ON UPDATE CURRENT_TIMESTAMP -> trg_star_dom_touch_updated_at() below,
--                            because PostgreSQL has no per-column equivalent
--
-- Idempotent: safe to re-run.
-- ============================================================




-- ------------------------------------------------------------
-- Identity & roles
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Roles (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL
);

CREATE TABLE IF NOT EXISTS Users (
    Id SERIAL PRIMARY KEY,
    Email VARCHAR(190) NOT NULL UNIQUE,
    Username VARCHAR(60) NOT NULL UNIQUE,
    FullName VARCHAR(120) NOT NULL,
    Phone VARCHAR(30) NOT NULL DEFAULT '',
    PasswordHash VARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    AvatarFile VARCHAR(255) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    EmailVerified BOOLEAN NOT NULL DEFAULT FALSE,
    -- Merchant commission-atelier profile (drives the Commission Hub cards)
    CommissionSlotCapacity INT NOT NULL DEFAULT 5,
    CommissionStartingPrice DECIMAL(12,2) NOT NULL DEFAULT 0,
    CommissionTurnaround VARCHAR(120) NOT NULL DEFAULT '3-5 business days',
    CommissionFormats VARCHAR(160) NOT NULL DEFAULT 'High-Res PNG + Print',
    CommissionSampleImage VARCHAR(255) NOT NULL DEFAULT '',
    CommissionTagline VARCHAR(160) NOT NULL DEFAULT '',
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP ,
    LastLoginAt TIMESTAMPTZ NULL,
    CONSTRAINT FK_Users_Role FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

-- ------------------------------------------------------------
-- Catalog
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Categories (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(80) NOT NULL,
    Slug VARCHAR(100) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    ParentId INT NULL,
    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId) REFERENCES Categories(Id)
);

CREATE TABLE IF NOT EXISTS Products (
    Id SERIAL PRIMARY KEY,
    MerchantId INT NOT NULL,
    CategoryId INT NOT NULL,
    Name VARCHAR(160) NOT NULL,
    Slug VARCHAR(190) NOT NULL UNIQUE,
    Description TEXT NULL,
    BasePrice DECIMAL(12,2) NOT NULL,
    SalePrice DECIMAL(12,2) NULL,
    StockQuantity INT NOT NULL DEFAULT 0,
    LowStockThreshold INT NOT NULL DEFAULT 5,
    Sku VARCHAR(60) NOT NULL UNIQUE,
    BrandName VARCHAR(120) NOT NULL DEFAULT '',
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    IsFeatured BOOLEAN NOT NULL DEFAULT FALSE,
    IsBoothExclusive BOOLEAN NOT NULL DEFAULT FALSE,
    IsEventExclusive BOOLEAN NOT NULL DEFAULT FALSE,
    BadgeLabel VARCHAR(60) NOT NULL DEFAULT '',
    MaterialDetails VARCHAR(255) NOT NULL DEFAULT '',
    RatingAvg DECIMAL(3,2) NOT NULL DEFAULT 0,
    RatingCount INT NOT NULL DEFAULT 0,
    SoldCount INT NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP ,
    CONSTRAINT FK_Products_Merchant FOREIGN KEY (MerchantId) REFERENCES Users(Id),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);

CREATE TABLE IF NOT EXISTS ProductImages (
    Id SERIAL PRIMARY KEY,
    ProductId INT NOT NULL,
    ImageFile VARCHAR(255) NOT NULL,
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    SortOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductImages_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ProductVariants (
    Id SERIAL PRIMARY KEY,
    ProductId INT NOT NULL,
    Name VARCHAR(120) NOT NULL,
    Sku VARCHAR(60) NOT NULL,
    PriceAdjustment DECIMAL(12,2) NOT NULL DEFAULT 0,
    StockQuantity INT NOT NULL DEFAULT 0,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT FK_ProductVariants_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Bundles (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(160) NOT NULL,
    Description VARCHAR(255) NULL,
    DiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    -- The "any N for PHP M" deal as real data, NOT parsed out of Name. Keeping it
    -- in columns means merchandising can rename a bundle freely without silently
    -- disabling its pricing. GroupSize = how many items form one bundle group;
    -- BundlePrice = what that whole group pays. Both 0 means "not a deal bundle"
    -- and the cart pricing engine skips it.
    GroupSize INT NOT NULL DEFAULT 0,
    BundlePrice DECIMAL(12,2) NOT NULL DEFAULT 0,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS BundleItems (
    Id SERIAL PRIMARY KEY,
    BundleId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    CONSTRAINT FK_BundleItems_Bundle FOREIGN KEY (BundleId) REFERENCES Bundles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BundleItems_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

CREATE TABLE IF NOT EXISTS Promotions (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(140) NOT NULL,
    Description VARCHAR(255) NULL,
    DiscountType VARCHAR(20) NOT NULL DEFAULT 'PERCENT', -- PERCENT | FIXED
    DiscountValue DECIMAL(12,2) NOT NULL DEFAULT 0,
    StartsAt TIMESTAMPTZ NOT NULL,
    EndsAt TIMESTAMPTZ NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

-- ------------------------------------------------------------
-- Cart & wishlist
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Cart (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL UNIQUE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP ,
    CONSTRAINT FK_Cart_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE IF NOT EXISTS CartItems (
    Id SERIAL PRIMARY KEY,
    CartId INT NOT NULL,
    ProductId INT NOT NULL,
    VariantId INT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    AddedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_CartItems_Cart FOREIGN KEY (CartId) REFERENCES Cart(Id) ON DELETE CASCADE,
    CONSTRAINT FK_CartItems_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

CREATE TABLE IF NOT EXISTS WishlistItems (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL,
    ProductId INT NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Wishlist_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Wishlist_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);

-- ------------------------------------------------------------
-- Pop-up tour & store locations
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS StoreLocations (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(160) NOT NULL,
    Venue VARCHAR(255) NOT NULL DEFAULT '',
    Address VARCHAR(255) NOT NULL DEFAULT '',
    City VARCHAR(80) NOT NULL DEFAULT '',
    Region VARCHAR(80) NOT NULL DEFAULT '',
    Latitude DECIMAL(10,6) NULL,
    Longitude DECIMAL(10,6) NULL,
    Contact VARCHAR(80) NOT NULL DEFAULT '',
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS PopUpEvents (
    Id SERIAL PRIMARY KEY,
    LocationId INT NOT NULL,
    Name VARCHAR(190) NOT NULL,
    Description TEXT NULL,
    StartDate TIMESTAMPTZ NOT NULL,
    EndDate TIMESTAMPTZ NOT NULL,
    OpenTime VARCHAR(20) NOT NULL DEFAULT '10:00 AM',
    CloseTime VARCHAR(20) NOT NULL DEFAULT '9:00 PM',
    BoothNumber VARCHAR(40) NOT NULL DEFAULT '',
    VenueDetail VARCHAR(255) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'UPCOMING', -- UPCOMING/NOW OPEN/ENDED/CANCELLED
    FeaturedGuest VARCHAR(160) NOT NULL DEFAULT '',
    IsCurrent BOOLEAN NOT NULL DEFAULT FALSE,
    ImageFile VARCHAR(255) NOT NULL DEFAULT '',
    LineupText VARCHAR(160) NOT NULL DEFAULT '',
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP ,
    CONSTRAINT FK_PopUpEvents_Location FOREIGN KEY (LocationId) REFERENCES StoreLocations(Id)
);

CREATE TABLE IF NOT EXISTS EventInventory (
    Id SERIAL PRIMARY KEY,
    EventId INT NOT NULL,
    ProductId INT NOT NULL,
    StartingStock INT NOT NULL DEFAULT 0,
    SoldQuantity INT NOT NULL DEFAULT 0,
    RemainingStock INT NOT NULL DEFAULT 0,
    IsEventExclusive BOOLEAN NOT NULL DEFAULT FALSE,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT FK_EventInventory_Event FOREIGN KEY (EventId) REFERENCES PopUpEvents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_EventInventory_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

-- ------------------------------------------------------------
-- Commerce (orders reference pop-up events)
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Orders (
    Id SERIAL PRIMARY KEY,
    OrderNumber VARCHAR(40) NOT NULL UNIQUE,
    UserId INT NOT NULL,
    EventId INT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'PENDING', -- PENDING/CONFIRMED/PROCESSING/SHIPPED/DELIVERED/CANCELLED
    Subtotal DECIMAL(12,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    ShippingFee DECIMAL(12,2) NOT NULL DEFAULT 0,
    TotalAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    PaymentMethod VARCHAR(20) NOT NULL DEFAULT 'COD', -- GCASH/GOTYME/CARD/COD
    PaymentStatus VARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING/PAID/REFUNDED/FAILED
    ShippingAddress VARCHAR(255) NOT NULL DEFAULT '',
    ContactPhone VARCHAR(30) NOT NULL DEFAULT '',
    Notes VARCHAR(500) NOT NULL DEFAULT '',
    -- Fulfilment: DELIVERY (J&T) or PICKUP (claim at an active/upcoming stall).
    Fulfillment VARCHAR(10) NOT NULL DEFAULT 'DELIVERY',
    PickupEventId INT NULL,
    -- A pick-up order is complete only when BOTH sides confirm the handover.
    PickupCustomerConfirmed BOOLEAN NOT NULL DEFAULT FALSE,
    PickupMerchantConfirmed BOOLEAN NOT NULL DEFAULT FALSE,
    -- Delivery shipping is quoted by the merchant, not computed: the courier fee is
    -- only known once J&T weighs the parcel, so the order cannot reach CONFIRMED
    -- until someone enters it. ShippingFee stays 0 and ShippingFeeConfirmed FALSE
    -- from checkout until then, and TotalAmount is finalised at that moment.
    -- Pick-up orders never set this and never carry a fee.
    ShippingFeeConfirmed BOOLEAN NOT NULL DEFAULT FALSE,
    ShippingFeeConfirmedBy INT NULL,
    ShippingFeeConfirmedAt TIMESTAMPTZ NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP ,
    CONSTRAINT FK_Orders_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_Orders_Event FOREIGN KEY (EventId) REFERENCES PopUpEvents(Id),
    CONSTRAINT FK_Orders_PickupEvent FOREIGN KEY (PickupEventId) REFERENCES PopUpEvents(Id),
    CONSTRAINT FK_Orders_ShippingFeeBy FOREIGN KEY (ShippingFeeConfirmedBy) REFERENCES Users(Id)
);

CREATE TABLE IF NOT EXISTS EventSales (
    Id SERIAL PRIMARY KEY,
    EventId INT NOT NULL,
    OrderId INT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(12,2) NOT NULL,
    TotalAmount DECIMAL(12,2) NOT NULL,
    SaleType VARCHAR(20) NOT NULL DEFAULT 'IN_PERSON', -- IN_PERSON/QR/PREORDER/ONLINE
    PaymentMethod VARCHAR(20) NOT NULL DEFAULT 'GCASH',
    SaleDate TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Notes VARCHAR(255) NOT NULL DEFAULT '',
    CONSTRAINT FK_EventSales_Event FOREIGN KEY (EventId) REFERENCES PopUpEvents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_EventSales_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id),
    CONSTRAINT FK_EventSales_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

CREATE TABLE IF NOT EXISTS OrderItems (
    Id SERIAL PRIMARY KEY,
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    VariantId INT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(12,2) NOT NULL,
    LineTotal DECIMAL(12,2) NOT NULL,
    CONSTRAINT FK_OrderItems_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

CREATE TABLE IF NOT EXISTS Payments (
    Id SERIAL PRIMARY KEY,
    OrderId INT NOT NULL,
    PaymentMethod VARCHAR(20) NOT NULL,
    Amount DECIMAL(12,2) NOT NULL,
    ReferenceNumber VARCHAR(80) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING/PAID/FAILED/REFUNDED
    PaidAt TIMESTAMPTZ NULL,
    GatewayResponse VARCHAR(255) NOT NULL DEFAULT '',
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Payments_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Receipts (
    Id SERIAL PRIMARY KEY,
    PaymentId INT NOT NULL,
    OrderId INT NOT NULL,
    ReceiptNumber VARCHAR(40) NOT NULL UNIQUE,
    ReceiptType VARCHAR(10) NOT NULL DEFAULT 'OR', -- OR (official receipt)
    IssuerName VARCHAR(190) NOT NULL DEFAULT '',
    IssuerTin VARCHAR(40) NOT NULL DEFAULT '',
    IssuerAddress VARCHAR(255) NOT NULL DEFAULT '',
    IssuerAccreditation VARCHAR(90) NOT NULL DEFAULT '',
    SoldToName VARCHAR(120) NOT NULL DEFAULT '',
    SoldToAddress VARCHAR(255) NOT NULL DEFAULT '',
    Subtotal DECIMAL(12,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    ShippingFee DECIMAL(12,2) NOT NULL DEFAULT 0,
    VatableAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    VatAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    VatExemptAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    TotalAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    ItemsSnapshot TEXT NULL,
    IssuedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Receipts_Payment FOREIGN KEY (PaymentId) REFERENCES Payments(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Receipts_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Shipping (
    Id SERIAL PRIMARY KEY,
    OrderId INT NOT NULL UNIQUE,
    Courier VARCHAR(60) NOT NULL DEFAULT '',
    TrackingNumber VARCHAR(80) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    Address VARCHAR(255) NOT NULL DEFAULT '',
    ShippedAt TIMESTAMPTZ NULL,
    DeliveredAt TIMESTAMPTZ NULL,
    CONSTRAINT FK_Shipping_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Reviews (
    Id SERIAL PRIMARY KEY,
    ProductId INT NOT NULL,
    OrderId INT NULL,
    UserId INT NOT NULL,
    Rating INT NOT NULL,
    Comment VARCHAR(1000) NOT NULL DEFAULT '',
    IsApproved BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Reviews_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Reviews_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- ------------------------------------------------------------
-- Commission atelier
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Commissions (
    Id SERIAL PRIMARY KEY,
    CommissionNumber VARCHAR(40) NOT NULL UNIQUE,
    CustomerId INT NOT NULL,
    MerchantId INT NOT NULL,
    CategoryId INT NOT NULL,
    Title VARCHAR(160) NOT NULL,
    Description TEXT NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    PreferredSize VARCHAR(120) NOT NULL DEFAULT '',
    PreferredDeadline TIMESTAMPTZ NULL,
    BudgetMin DECIMAL(12,2) NULL,
    BudgetMax DECIMAL(12,2) NULL,
    AdditionalNotes VARCHAR(1000) NOT NULL DEFAULT '',
    FinalPrice DECIMAL(12,2) NULL,
    EstimatedCompletionDate TIMESTAMPTZ NULL,
    MerchantNotes VARCHAR(1000) NOT NULL DEFAULT '',
    DepositAmount DECIMAL(12,2) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'SUBMITTED',
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP ,
    CONSTRAINT FK_Commissions_Customer FOREIGN KEY (CustomerId) REFERENCES Users(Id),
    CONSTRAINT FK_Commissions_Merchant FOREIGN KEY (MerchantId) REFERENCES Users(Id),
    CONSTRAINT FK_Commissions_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);

CREATE TABLE IF NOT EXISTS CommissionReferenceImages (
    Id SERIAL PRIMARY KEY,
    CommissionId INT NOT NULL,
    ImageFile VARCHAR(255) NOT NULL DEFAULT '',
    FileName VARCHAR(160) NOT NULL DEFAULT '',
    FileSizeKb INT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_CommRefs_Commission FOREIGN KEY (CommissionId) REFERENCES Commissions(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS CommissionMessages (
    Id SERIAL PRIMARY KEY,
    CommissionId INT NOT NULL,
    SenderId INT NOT NULL,
    Message TEXT NOT NULL,
    IsRead BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_CommMessages_Commission FOREIGN KEY (CommissionId) REFERENCES Commissions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_CommMessages_Sender FOREIGN KEY (SenderId) REFERENCES Users(Id)
);

CREATE TABLE IF NOT EXISTS CommissionStatusHistory (
    Id SERIAL PRIMARY KEY,
    CommissionId INT NOT NULL,
    FromStatus VARCHAR(30) NOT NULL DEFAULT '',
    ToStatus VARCHAR(30) NOT NULL,
    ChangedBy VARCHAR(120) NOT NULL DEFAULT '',
    Note VARCHAR(500) NOT NULL DEFAULT '',
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_CommHistory_Commission FOREIGN KEY (CommissionId) REFERENCES Commissions(Id) ON DELETE CASCADE
);

-- ------------------------------------------------------------
-- Notifications & diagnostics
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Notifications (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL,
    Title VARCHAR(160) NOT NULL,
    Message TEXT NULL,
    NotificationType VARCHAR(20) NOT NULL DEFAULT 'SYSTEM', -- ORDER/COMMISSION/EVENT/SYSTEM
    LinkPath VARCHAR(80) NOT NULL DEFAULT '',
    IsRead BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS AppErrors (
    Id SERIAL PRIMARY KEY,
    Context VARCHAR(100) NOT NULL DEFAULT '',
    Message TEXT NULL,
    StackTrace TEXT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ------------------------------------------------------------
-- Payment settings (admin-managed e-wallet QR details)
-- ------------------------------------------------------------
-- One row per accepted e-wallet channel (GCASH, GOTYME). The admin edits
-- everything — QR code image, account number, and account name — from
-- App/Admin/PaymentSettings.aspx, and the order-detail QR popup renders
-- whatever is configured here.
CREATE TABLE IF NOT EXISTS PaymentSettings (
    Id SERIAL PRIMARY KEY,
    Channel VARCHAR(20) NOT NULL UNIQUE,            -- 'GCASH' | 'GOTYME'
    AccountName VARCHAR(120) NOT NULL DEFAULT '',
    AccountNumber VARCHAR(60) NOT NULL DEFAULT '',
    QrImageFile VARCHAR(255) NOT NULL DEFAULT '',   -- uploaded QR image, root-relative path
    QrCaption VARCHAR(120) NOT NULL DEFAULT '',     -- admin's note shown under the QR
    -- What the customer-facing QR popup shows:
    --   'BOTH'   = QR image + number + name (default)
    --   'QR_ONLY' = QR image only
    --   'NUMBER_NAME' = number + name, no QR image
    --   'NAME_ONLY' = account name only (pure "send to" flow)
    QrDisplayMode VARCHAR(20) NOT NULL DEFAULT 'BOTH',
    IsEnabled BOOLEAN NOT NULL DEFAULT TRUE,        -- shown as a payment option when TRUE
    UpdatedBy VARCHAR(120) NOT NULL DEFAULT '',
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Older installs: create the table if it is missing, then bring any existing
-- table up to date (fresh installs already have the columns above).
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS AccountName VARCHAR(120) NOT NULL DEFAULT '';
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS AccountNumber VARCHAR(60) NOT NULL DEFAULT '';
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS QrImageFile VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS QrCaption VARCHAR(120) NOT NULL DEFAULT '';
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS QrDisplayMode VARCHAR(20) NOT NULL DEFAULT 'BOTH';
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS IsEnabled BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS UpdatedBy VARCHAR(120) NOT NULL DEFAULT '';

-- The QR image itself lives in the database as BYTEA, not as a file under
-- Uploads\. Files are not in Git, so a file-backed QR is missing on any other
-- machine and needs re-uploading; a pg_dump carries the bytes with everything
-- else and the image restores itself. QrImageFile is kept only so a pre-migration
-- row that still points at an uploaded file keeps working.
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS QrImageData BYTEA;
ALTER TABLE PaymentSettings ADD COLUMN IF NOT EXISTS QrImageMime VARCHAR(50) NOT NULL DEFAULT '';

-- Pre-rename installs stored the second e-wallet as 'MAYA'; it is Gotyme now.
-- Renamed in place so the configured account number and uploaded QR image carry
-- over. Channel is UNIQUE, so this only lands while no 'GOTYME' row exists yet.
-- Historical Orders/Payments rows keep their stored method and are labelled
-- Gotyme at display time. PaymentSettingRepository runs the same statement on
-- demand, so either path ends with one row per channel.
UPDATE PaymentSettings SET Channel = 'GOTYME' WHERE Channel = 'MAYA'
    AND NOT EXISTS (SELECT 1 FROM PaymentSettings WHERE Channel = 'GOTYME');

-- PaymentSettings trigger lives with the other UpdatedAt triggers further below,
-- after trg_star_dom_touch_updated_at() is defined.

-- Mall / venue photography, stored as bytes for the same reason the QR image
-- above is: Assets\Malls ships in Git, but a stray folder delete (or a machine
-- that never cloned the images) leaves the itinerary grid with nothing to draw.
-- One row per root-relative path, so PopUpEvents.ImageFile keeps holding the
-- path it always has and only the byte source moves -- App/AssetImg.aspx serves
-- the copy held here and falls back to the file when a row is missing, which is
-- exactly the PaymentQr arrangement.
--
-- NOT in the seed's TRUNCATE list: the rows are the durable copy of artwork,
-- not demo data, and re-seeding the catalogue must not delete them. Deleting a
-- row on purpose is how you retire a photo.
CREATE TABLE IF NOT EXISTS AssetImages (
    Path VARCHAR(255) NOT NULL PRIMARY KEY,   -- root-relative, '/Assets/Malls/x.webp'
    Data BYTEA NOT NULL,
    Mime VARCHAR(50) NOT NULL DEFAULT 'image/webp',
    ByteSize INT NOT NULL DEFAULT 0,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Indexes & Constraints
CREATE INDEX IF NOT EXISTS IDX_Users_Role ON Users (RoleId);
CREATE INDEX IF NOT EXISTS IDX_Users_Status ON Users (Status);
CREATE INDEX IF NOT EXISTS IDX_Categories_Parent ON Categories (ParentId);
CREATE INDEX IF NOT EXISTS IDX_Products_Category ON Products (CategoryId);
CREATE INDEX IF NOT EXISTS IDX_Products_Merchant ON Products (MerchantId);
CREATE INDEX IF NOT EXISTS IDX_Products_Active ON Products (IsActive, IsFeatured);
CREATE INDEX IF NOT EXISTS IDX_ProductImages_Product ON ProductImages (ProductId, IsPrimary);
CREATE INDEX IF NOT EXISTS IDX_ProductVariants_Product ON ProductVariants (ProductId);
CREATE INDEX IF NOT EXISTS IDX_BundleItems_Bundle ON BundleItems (BundleId);
CREATE INDEX IF NOT EXISTS IDX_BundleItems_Product ON BundleItems (ProductId);
CREATE INDEX IF NOT EXISTS IDX_Promotions_Live ON Promotions (IsActive, StartsAt, EndsAt);
CREATE INDEX IF NOT EXISTS IDX_CartItems_Cart ON CartItems (CartId);
CREATE INDEX IF NOT EXISTS IDX_CartItems_Product ON CartItems (ProductId);
-- Unquoted identifiers fold to lowercase, so these constraints exist in
-- pg_constraint as uk_wishlist / uk_eventinventory regardless of the case
-- used here. The lowercase checks keep the script truly re-runnable.
DO $do$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uk_wishlist') THEN
        ALTER TABLE WishlistItems ADD CONSTRAINT UK_Wishlist UNIQUE (UserId, ProductId);
    END IF;
END
$do$;
CREATE INDEX IF NOT EXISTS IDX_Events_Status ON PopUpEvents (Status, StartDate);
CREATE INDEX IF NOT EXISTS IDX_Events_Current ON PopUpEvents (IsCurrent);
-- Lookups are by exact Path (the primary key), so no extra index is needed;
-- ByteSize exists only so the streaming endpoint can answer HEAD without
-- pulling the blob out of storage.
DO $do$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uk_eventinventory') THEN
        ALTER TABLE EventInventory ADD CONSTRAINT UK_EventInventory UNIQUE (EventId, ProductId);
    END IF;
END
$do$;
CREATE INDEX IF NOT EXISTS IDX_Orders_User ON Orders (UserId);
CREATE INDEX IF NOT EXISTS IDX_Orders_Status ON Orders (Status);
CREATE INDEX IF NOT EXISTS IDX_Orders_Created ON Orders (CreatedAt);
CREATE INDEX IF NOT EXISTS IDX_EventSales_Event ON EventSales (EventId);
CREATE INDEX IF NOT EXISTS IDX_EventSales_Product ON EventSales (ProductId);
CREATE INDEX IF NOT EXISTS IDX_EventSales_Order ON EventSales (OrderId);
CREATE INDEX IF NOT EXISTS IDX_OrderItems_Order ON OrderItems (OrderId);
CREATE INDEX IF NOT EXISTS IDX_OrderItems_Product ON OrderItems (ProductId);
CREATE INDEX IF NOT EXISTS IDX_Payments_Order ON Payments (OrderId);
CREATE INDEX IF NOT EXISTS IDX_Payments_Method ON Payments (PaymentMethod);
CREATE INDEX IF NOT EXISTS IDX_Receipts_Order ON Receipts (OrderId);
DO $do$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uk_receipts_payment') THEN
        ALTER TABLE Receipts ADD CONSTRAINT UK_Receipts_Payment UNIQUE (PaymentId);
    END IF;
END
$do$;
CREATE INDEX IF NOT EXISTS IDX_Reviews_Product ON Reviews (ProductId);
CREATE INDEX IF NOT EXISTS IDX_Reviews_User ON Reviews (UserId);
CREATE INDEX IF NOT EXISTS IDX_Commissions_Merchant ON Commissions (MerchantId, Status);
CREATE INDEX IF NOT EXISTS IDX_Commissions_Customer ON Commissions (CustomerId, Status);
CREATE INDEX IF NOT EXISTS IDX_CommRefs_Commission ON CommissionReferenceImages (CommissionId);
CREATE INDEX IF NOT EXISTS IDX_CommMessages_Commission ON CommissionMessages (CommissionId);
CREATE INDEX IF NOT EXISTS IDX_CommMessages_Sender ON CommissionMessages (SenderId);
CREATE INDEX IF NOT EXISTS IDX_CommHistory_Commission ON CommissionStatusHistory (CommissionId);
CREATE INDEX IF NOT EXISTS IDX_Notifications_User ON Notifications (UserId, IsRead);
CREATE INDEX IF NOT EXISTS IDX_AppErrors_Created ON AppErrors (CreatedAt);

-- ------------------------------------------------------------
-- Column upgrades for installs created before these columns
-- ------------------------------------------------------------
-- CREATE TABLE IF NOT EXISTS is a no-op on a database that already has the
-- table, so a pre-existing install never picks up new columns from the CREATE
-- blocks above. These ADD COLUMN IF NOT EXISTS statements are what bring an
-- older database up to date; they are no-ops on a fresh one.
ALTER TABLE Bundles ADD COLUMN IF NOT EXISTS GroupSize INT NOT NULL DEFAULT 0;
ALTER TABLE Bundles ADD COLUMN IF NOT EXISTS BundlePrice DECIMAL(12,2) NOT NULL DEFAULT 0;

ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingFeeConfirmed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingFeeConfirmedBy INT NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingFeeConfirmedAt TIMESTAMPTZ NULL;
-- The CREATE TABLE block above already declares FK_Orders_ShippingFeeBy, and
-- because identifiers fold to lowercase, pg_constraint stores it as
-- fk_orders_shippingfeeby. Comparing conname to the mixed-case literal
-- 'FK_Orders_ShippingFeeBy' never matches, so the ADD CONSTRAINT fired again
-- and aborted a FRESH bootstrap (this is what broke start-db.bat's one-time
-- seed on a newly cloned machine: schema failed -> seed never ran -> 0
-- products). PostgreSQL has no ADD CONSTRAINT IF NOT EXISTS, so the guard
-- stays a DO block — just comparing against the lowercase stored name.
DO $do$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_orders_shippingfeeby') THEN
        ALTER TABLE Orders ADD CONSTRAINT FK_Orders_ShippingFeeBy
            FOREIGN KEY (ShippingFeeConfirmedBy) REFERENCES Users(Id);
    END IF;
END
$do$;

CREATE INDEX IF NOT EXISTS IDX_PaymentSettings_Channel ON PaymentSettings (Channel);

-- ------------------------------------------------------------
-- UpdatedAt maintenance
-- ------------------------------------------------------------
-- The MySQL schema declared UpdatedAt TIMESTAMP ... ON UPDATE CURRENT_TIMESTAMP.
-- PostgreSQL has no column-level equivalent, so the behaviour that was silently
-- lost in the port is restored here with a BEFORE UPDATE trigger. One shared
-- function is reused by every table that owns an UpdatedAt column.
CREATE OR REPLACE FUNCTION public.trg_star_dom_touch_updated_at()
RETURNS TRIGGER AS $fn$
BEGIN
    -- Identifiers are unquoted in this schema, so they fold to lower case.
    NEW.updatedat := now();
    RETURN NEW;
END;
$fn$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_users_updatedat ON Users;
CREATE TRIGGER trg_users_updatedat
    BEFORE UPDATE ON Users
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

DROP TRIGGER IF EXISTS trg_products_updatedat ON Products;
CREATE TRIGGER trg_products_updatedat
    BEFORE UPDATE ON Products
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

DROP TRIGGER IF EXISTS trg_cart_updatedat ON Cart;
CREATE TRIGGER trg_cart_updatedat
    BEFORE UPDATE ON Cart
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

DROP TRIGGER IF EXISTS trg_popupevents_updatedat ON PopUpEvents;
CREATE TRIGGER trg_popupevents_updatedat
    BEFORE UPDATE ON PopUpEvents
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

DROP TRIGGER IF EXISTS trg_orders_updatedat ON Orders;
CREATE TRIGGER trg_orders_updatedat
    BEFORE UPDATE ON Orders
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

DROP TRIGGER IF EXISTS trg_commissions_updatedat ON Commissions;
CREATE TRIGGER trg_commissions_updatedat
    BEFORE UPDATE ON Commissions
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

DROP TRIGGER IF EXISTS trg_paymentsettings_updatedat ON PaymentSettings;
CREATE TRIGGER trg_paymentsettings_updatedat
    BEFORE UPDATE ON PaymentSettings
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

-- The ETag the streaming endpoint sends is derived from UpdatedAt, so an
-- edited photo must move it or browsers keep serving the stale bytes.
DROP TRIGGER IF EXISTS trg_assetimages_updatedat ON AssetImages;
CREATE TRIGGER trg_assetimages_updatedat
    BEFORE UPDATE ON AssetImages
    FOR EACH ROW EXECUTE FUNCTION public.trg_star_dom_touch_updated_at();

-- ------------------------------------------------------------
-- Row level security
-- ------------------------------------------------------------
-- The VB.NET app is the only database client and authenticates as the table
-- owner over a direct Npgsql connection, so it is unaffected by RLS (owners
-- bypass it). RLS exists to close the Supabase Data API / PostgREST path:
-- with RLS enabled and only a backend policy present, the anon and
-- authenticated keys see zero rows and may write nothing, so a leaked
-- publishable key cannot read the customer or commission tables.
--
-- This is a deliberate "backend-only" posture, not an oversight: all
-- authorisation (role checks, store-manager gating, CSRF) lives in the
-- application layer, and no Supabase client-side code exists in this project.
DO $do$
DECLARE
    t TEXT;
    backend_roles TEXT;
BEGIN
    -- service_role only exists on Supabase; fall back to the table owner on a
    -- plain local PostgreSQL so this script also runs for development.
    --
    -- The roles are joined into a comma-separated string that is spliced in as
    -- a LIST (%s), never as one identifier (%I): %I would quote the whole
    -- string as a single role named "service_role, postgres", which does not
    -- exist, so CREATE POLICY failed with 'role "service_role, postgres" does
    -- not exist' on every Supabase host. On a plain local PostgreSQL that
    -- branch never ran, which is why it survived.
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'service_role') THEN
        backend_roles := 'service_role, postgres';
    ELSE
        backend_roles := 'postgres';
    END IF;

    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', t || '_backend_access', t);
        EXECUTE format(
            'CREATE POLICY %I ON public.%I FOR ALL TO %s USING (true) WITH CHECK (true)',
            t || '_backend_access', t, backend_roles);
    END LOOP;
END
$do$;
