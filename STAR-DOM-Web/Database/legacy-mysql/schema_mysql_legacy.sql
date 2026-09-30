-- ============================================================
-- STAR:DOM — Artisan Marketplace & Pop-up Tour System
-- MySQL 8.x schema
-- Run this file against the "stardom" database (see README).
-- Idempotent: safe to re-run (CREATE TABLE IF NOT EXISTS).
-- ============================================================

CREATE DATABASE IF NOT EXISTS stardom CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE stardom;

-- ------------------------------------------------------------
-- Identity & roles
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Roles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Email VARCHAR(190) NOT NULL UNIQUE,
    Username VARCHAR(60) NOT NULL UNIQUE,
    FullName VARCHAR(120) NOT NULL,
    Phone VARCHAR(30) NOT NULL DEFAULT '',
    PasswordHash VARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    AvatarFile VARCHAR(255) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    EmailVerified TINYINT(1) NOT NULL DEFAULT 0,
    -- Merchant commission-atelier profile (drives the Commission Hub cards)
    CommissionSlotCapacity INT NOT NULL DEFAULT 5,
    CommissionStartingPrice DECIMAL(12,2) NOT NULL DEFAULT 0,
    CommissionTurnaround VARCHAR(120) NOT NULL DEFAULT '3-5 business days',
    CommissionFormats VARCHAR(160) NOT NULL DEFAULT 'High-Res PNG + Print',
    CommissionSampleImage VARCHAR(255) NOT NULL DEFAULT '',
    CommissionTagline VARCHAR(160) NOT NULL DEFAULT '',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    LastLoginAt DATETIME NULL,
    CONSTRAINT FK_Users_Role FOREIGN KEY (RoleId) REFERENCES Roles(Id),
    INDEX IDX_Users_Role (RoleId),
    INDEX IDX_Users_Status (Status)
) ENGINE=InnoDB;

-- ------------------------------------------------------------
-- Catalog
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Categories (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(80) NOT NULL,
    Slug VARCHAR(100) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    ParentId INT NULL,
    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId) REFERENCES Categories(Id),
    INDEX IDX_Categories_Parent (ParentId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Products (
    Id INT AUTO_INCREMENT PRIMARY KEY,
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
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    IsFeatured TINYINT(1) NOT NULL DEFAULT 0,
    IsBoothExclusive TINYINT(1) NOT NULL DEFAULT 0,
    IsEventExclusive TINYINT(1) NOT NULL DEFAULT 0,
    BadgeLabel VARCHAR(60) NOT NULL DEFAULT '',
    MaterialDetails VARCHAR(255) NOT NULL DEFAULT '',
    RatingAvg DECIMAL(3,2) NOT NULL DEFAULT 0,
    RatingCount INT NOT NULL DEFAULT 0,
    SoldCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT FK_Products_Merchant FOREIGN KEY (MerchantId) REFERENCES Users(Id),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
    INDEX IDX_Products_Category (CategoryId),
    INDEX IDX_Products_Merchant (MerchantId),
    INDEX IDX_Products_Active (IsActive, IsFeatured)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS ProductImages (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ProductId INT NOT NULL,
    ImageFile VARCHAR(255) NOT NULL,
    IsPrimary TINYINT(1) NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductImages_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    INDEX IDX_ProductImages_Product (ProductId, IsPrimary)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS ProductVariants (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ProductId INT NOT NULL,
    Name VARCHAR(120) NOT NULL,
    Sku VARCHAR(60) NOT NULL,
    PriceAdjustment DECIMAL(12,2) NOT NULL DEFAULT 0,
    StockQuantity INT NOT NULL DEFAULT 0,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CONSTRAINT FK_ProductVariants_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    INDEX IDX_ProductVariants_Product (ProductId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Bundles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(160) NOT NULL,
    Description VARCHAR(255) NULL,
    DiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS BundleItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    BundleId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    CONSTRAINT FK_BundleItems_Bundle FOREIGN KEY (BundleId) REFERENCES Bundles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BundleItems_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    INDEX IDX_BundleItems_Bundle (BundleId),
    INDEX IDX_BundleItems_Product (ProductId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Promotions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(140) NOT NULL,
    Description VARCHAR(255) NULL,
    DiscountType VARCHAR(20) NOT NULL DEFAULT 'PERCENT', -- PERCENT | FIXED
    DiscountValue DECIMAL(12,2) NOT NULL DEFAULT 0,
    StartsAt DATETIME NOT NULL,
    EndsAt DATETIME NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    INDEX IDX_Promotions_Live (IsActive, StartsAt, EndsAt)
) ENGINE=InnoDB;

-- ------------------------------------------------------------
-- Cart & wishlist
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Cart (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL UNIQUE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT FK_Cart_User FOREIGN KEY (UserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CartItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CartId INT NOT NULL,
    ProductId INT NOT NULL,
    VariantId INT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    AddedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_CartItems_Cart FOREIGN KEY (CartId) REFERENCES Cart(Id) ON DELETE CASCADE,
    CONSTRAINT FK_CartItems_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    INDEX IDX_CartItems_Cart (CartId),
    INDEX IDX_CartItems_Product (ProductId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS WishlistItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    ProductId INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Wishlist_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Wishlist_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    UNIQUE KEY UK_Wishlist (UserId, ProductId)
) ENGINE=InnoDB;

-- ------------------------------------------------------------
-- Pop-up tour & store locations
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS StoreLocations (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(160) NOT NULL,
    Venue VARCHAR(255) NOT NULL DEFAULT '',
    Address VARCHAR(255) NOT NULL DEFAULT '',
    City VARCHAR(80) NOT NULL DEFAULT '',
    Region VARCHAR(80) NOT NULL DEFAULT '',
    Latitude DECIMAL(10,6) NULL,
    Longitude DECIMAL(10,6) NULL,
    Contact VARCHAR(80) NOT NULL DEFAULT '',
    IsActive TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS PopUpEvents (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    LocationId INT NOT NULL,
    Name VARCHAR(190) NOT NULL,
    Description TEXT NULL,
    StartDate DATETIME NOT NULL,
    EndDate DATETIME NOT NULL,
    OpenTime VARCHAR(20) NOT NULL DEFAULT '10:00 AM',
    CloseTime VARCHAR(20) NOT NULL DEFAULT '9:00 PM',
    BoothNumber VARCHAR(40) NOT NULL DEFAULT '',
    VenueDetail VARCHAR(255) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'UPCOMING', -- UPCOMING/NOW OPEN/ENDED/CANCELLED
    FeaturedGuest VARCHAR(160) NOT NULL DEFAULT '',
    IsCurrent TINYINT(1) NOT NULL DEFAULT 0,
    ImageFile VARCHAR(255) NOT NULL DEFAULT '',
    LineupText VARCHAR(160) NOT NULL DEFAULT '',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT FK_PopUpEvents_Location FOREIGN KEY (LocationId) REFERENCES StoreLocations(Id),
    INDEX IDX_Events_Status (Status, StartDate),
    INDEX IDX_Events_Current (IsCurrent)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS EventInventory (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    EventId INT NOT NULL,
    ProductId INT NOT NULL,
    StartingStock INT NOT NULL DEFAULT 0,
    SoldQuantity INT NOT NULL DEFAULT 0,
    RemainingStock INT NOT NULL DEFAULT 0,
    IsEventExclusive TINYINT(1) NOT NULL DEFAULT 0,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CONSTRAINT FK_EventInventory_Event FOREIGN KEY (EventId) REFERENCES PopUpEvents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_EventInventory_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    UNIQUE KEY UK_EventInventory (EventId, ProductId)
) ENGINE=InnoDB;

-- ------------------------------------------------------------
-- Commerce (orders reference pop-up events)
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Orders (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    OrderNumber VARCHAR(40) NOT NULL UNIQUE,
    UserId INT NOT NULL,
    EventId INT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'PENDING', -- PENDING/CONFIRMED/PROCESSING/SHIPPED/DELIVERED/CANCELLED
    Subtotal DECIMAL(12,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    ShippingFee DECIMAL(12,2) NOT NULL DEFAULT 0,
    TotalAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    PaymentMethod VARCHAR(20) NOT NULL DEFAULT 'COD', -- GCASH/MAYA/CARD/COD
    PaymentStatus VARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING/PAID/REFUNDED/FAILED
    ShippingAddress VARCHAR(255) NOT NULL DEFAULT '',
    ContactPhone VARCHAR(30) NOT NULL DEFAULT '',
    Notes VARCHAR(500) NOT NULL DEFAULT '',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT FK_Orders_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_Orders_Event FOREIGN KEY (EventId) REFERENCES PopUpEvents(Id),
    INDEX IDX_Orders_User (UserId),
    INDEX IDX_Orders_Status (Status),
    INDEX IDX_Orders_Created (CreatedAt)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS EventSales (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    EventId INT NOT NULL,
    OrderId INT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(12,2) NOT NULL,
    TotalAmount DECIMAL(12,2) NOT NULL,
    SaleType VARCHAR(20) NOT NULL DEFAULT 'IN_PERSON', -- IN_PERSON/QR/PREORDER/ONLINE
    PaymentMethod VARCHAR(20) NOT NULL DEFAULT 'GCASH',
    SaleDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Notes VARCHAR(255) NOT NULL DEFAULT '',
    CONSTRAINT FK_EventSales_Event FOREIGN KEY (EventId) REFERENCES PopUpEvents(Id) ON DELETE CASCADE,
    CONSTRAINT FK_EventSales_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id),
    CONSTRAINT FK_EventSales_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    INDEX IDX_EventSales_Event (EventId),
    INDEX IDX_EventSales_Product (ProductId),
    INDEX IDX_EventSales_Order (OrderId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS OrderItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    VariantId INT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(12,2) NOT NULL,
    LineTotal DECIMAL(12,2) NOT NULL,
    CONSTRAINT FK_OrderItems_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    INDEX IDX_OrderItems_Order (OrderId),
    INDEX IDX_OrderItems_Product (ProductId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Payments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    OrderId INT NOT NULL,
    PaymentMethod VARCHAR(20) NOT NULL,
    Amount DECIMAL(12,2) NOT NULL,
    ReferenceNumber VARCHAR(80) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING/PAID/FAILED/REFUNDED
    PaidAt DATETIME NULL,
    GatewayResponse VARCHAR(255) NOT NULL DEFAULT '',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Payments_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
    INDEX IDX_Payments_Order (OrderId),
    INDEX IDX_Payments_Method (PaymentMethod)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Receipts (
    Id INT AUTO_INCREMENT PRIMARY KEY,
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
    IssuedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Receipts_Payment FOREIGN KEY (PaymentId) REFERENCES Payments(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Receipts_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
    INDEX IDX_Receipts_Order (OrderId),
    UNIQUE KEY UK_Receipts_Payment (PaymentId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Shipping (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    OrderId INT NOT NULL UNIQUE,
    Courier VARCHAR(60) NOT NULL DEFAULT '',
    TrackingNumber VARCHAR(80) NOT NULL DEFAULT '',
    Status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    Address VARCHAR(255) NOT NULL DEFAULT '',
    ShippedAt DATETIME NULL,
    DeliveredAt DATETIME NULL,
    CONSTRAINT FK_Shipping_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Reviews (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ProductId INT NOT NULL,
    OrderId INT NULL,
    UserId INT NOT NULL,
    Rating INT NOT NULL,
    Comment VARCHAR(1000) NOT NULL DEFAULT '',
    IsApproved TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Reviews_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Reviews_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    INDEX IDX_Reviews_Product (ProductId),
    INDEX IDX_Reviews_User (UserId)
) ENGINE=InnoDB;

-- ------------------------------------------------------------
-- Commission atelier
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Commissions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CommissionNumber VARCHAR(40) NOT NULL UNIQUE,
    CustomerId INT NOT NULL,
    MerchantId INT NOT NULL,
    CategoryId INT NOT NULL,
    Title VARCHAR(160) NOT NULL,
    Description TEXT NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    PreferredSize VARCHAR(120) NOT NULL DEFAULT '',
    PreferredDeadline DATETIME NULL,
    BudgetMin DECIMAL(12,2) NULL,
    BudgetMax DECIMAL(12,2) NULL,
    AdditionalNotes VARCHAR(1000) NOT NULL DEFAULT '',
    FinalPrice DECIMAL(12,2) NULL,
    EstimatedCompletionDate DATETIME NULL,
    MerchantNotes VARCHAR(1000) NOT NULL DEFAULT '',
    DepositAmount DECIMAL(12,2) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'SUBMITTED',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT FK_Commissions_Customer FOREIGN KEY (CustomerId) REFERENCES Users(Id),
    CONSTRAINT FK_Commissions_Merchant FOREIGN KEY (MerchantId) REFERENCES Users(Id),
    CONSTRAINT FK_Commissions_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
    INDEX IDX_Commissions_Merchant (MerchantId, Status),
    INDEX IDX_Commissions_Customer (CustomerId, Status)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CommissionReferenceImages (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CommissionId INT NOT NULL,
    ImageFile VARCHAR(255) NOT NULL DEFAULT '',
    FileName VARCHAR(160) NOT NULL DEFAULT '',
    FileSizeKb INT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_CommRefs_Commission FOREIGN KEY (CommissionId) REFERENCES Commissions(Id) ON DELETE CASCADE,
    INDEX IDX_CommRefs_Commission (CommissionId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CommissionMessages (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CommissionId INT NOT NULL,
    SenderId INT NOT NULL,
    Message TEXT NOT NULL,
    IsRead TINYINT(1) NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_CommMessages_Commission FOREIGN KEY (CommissionId) REFERENCES Commissions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_CommMessages_Sender FOREIGN KEY (SenderId) REFERENCES Users(Id),
    INDEX IDX_CommMessages_Commission (CommissionId),
    INDEX IDX_CommMessages_Sender (SenderId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CommissionStatusHistory (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CommissionId INT NOT NULL,
    FromStatus VARCHAR(30) NOT NULL DEFAULT '',
    ToStatus VARCHAR(30) NOT NULL,
    ChangedBy VARCHAR(120) NOT NULL DEFAULT '',
    Note VARCHAR(500) NOT NULL DEFAULT '',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_CommHistory_Commission FOREIGN KEY (CommissionId) REFERENCES Commissions(Id) ON DELETE CASCADE,
    INDEX IDX_CommHistory_Commission (CommissionId)
) ENGINE=InnoDB;

-- ------------------------------------------------------------
-- Notifications & diagnostics
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Notifications (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    Title VARCHAR(160) NOT NULL,
    Message TEXT NULL,
    NotificationType VARCHAR(20) NOT NULL DEFAULT 'SYSTEM', -- ORDER/COMMISSION/EVENT/SYSTEM
    LinkPath VARCHAR(80) NOT NULL DEFAULT '',
    IsRead TINYINT(1) NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX IDX_Notifications_User (UserId, IsRead)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS AppErrors (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Context VARCHAR(100) NOT NULL DEFAULT '',
    Message TEXT NULL,
    StackTrace TEXT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX IDX_AppErrors_Created (CreatedAt)
) ENGINE=InnoDB;