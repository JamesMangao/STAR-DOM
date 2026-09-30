-- ============================================================
-- STAR:DOM — Seed data (demo only)
--
-- PostgreSQL 15+ / Supabase (ported from the MySQL 8.x original).
-- Idempotent: safe to re-run after supabase_schema.sql. Upserts use
-- ON CONFLICT (...) DO UPDATE SET ... = EXCLUDED...., which is the
-- PostgreSQL spelling of MySQL's ON DUPLICATE KEY UPDATE.
--
-- Load with:
--     psql "$SUPABASE_DB_URL" -f supabase_schema.sql
--     psql "$SUPABASE_DB_URL" -f supabase_seed.sql
--
-- Demo logins (all lowercase):
--   CUSTOMER : bella / customer123   | juan / customer123
--   ADMIN    : admin / admin123
--
-- Everything lives in this one file: the 100-item ARTSHOP product catalog,
-- its product images, and the product-linked demo data (event inventory,
-- bundles, order history, commissions, notifications). The separate MySQL
-- seed scripts it used to be split across are archived, unreadable, under
-- legacy-mysql/ -- do not run them.
-- ============================================================


-- ---------- Roles ----------
INSERT INTO Roles (Id, Name, Description) VALUES
  (1, 'CUSTOMER', 'Marketplace shopper & commission requester'),
  (2, 'MERCHANT', 'Artisan creator / studio merchant'),
  (3, 'ADMIN',    'System-level manager (also the artist / merchant)')
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Users & Creator Studios ----------
INSERT INTO Users (Id, Email, Username, FullName, Phone, PasswordHash, RoleId, Status, EmailVerified,
                   CommissionSlotCapacity, CommissionStartingPrice, CommissionTurnaround, CommissionFormats, CommissionTagline) VALUES
(1,  'bella@example.com',     'bella',     'Bella Santos',       '09171234567', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(2,  'mika@stardom.ph',       'mika',      'Mika Visuals',       '09174441122', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 2, 'ACTIVE', TRUE, 4, 1200.00, '2-4 business days', 'Archival Print + Vector', 'Ethereal Flora & Fauna'),
(3,  'renzo@stardom.ph',      'renzo',     'Renzo Cruz Atelier', '09175553344', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 2, 'ACTIVE', TRUE, 3, 2000.00, '5-7 business days', 'Handmade Acrylic & Ink', 'Contemporary Urban Lore'),
(4,  'puffu@stardom.ph',      'puffu',     'Puffu Studio',       '09176665566', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 2, 'ACTIVE', TRUE, 6, 800.00,  '1-3 business days', 'Die-cut Vinyl & Hologram', 'Cute & Chaotic Sticker Guild'),
(5,  'admin@stardom.ph',      'admin',     'STAR:DOM Admin',     '09281234567', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 3, 'ACTIVE', TRUE, 5, 1500.00, '3-5 business days', 'High-Res PNG + A4 Print', 'Anime & Cyberpunk Stylist'),
(6,  'juan@example.com',      'juan',      'Juan Dela Cruz',     '09175678901', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(10, 'cloe@example.com',      'cloe',      'Cloe Valenzuela',    '09179012345', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(11, 'mark@example.com',      'mark',      'Mark Alcantara',     '09170123456', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(12, 'paolo@example.com',     'paolo',     'Paolo Reyes',        '09171239876', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', '')
ON CONFLICT (Id) DO UPDATE SET Email = EXCLUDED.Email, FullName = EXCLUDED.FullName;

-- ---------- Categories ----------
INSERT INTO Categories (Id, Name, Slug, Description, DisplayOrder, IsActive) VALUES
(1,  'Original Art',    'original-art',    'One-of-a-kind originals and A3/A2 gallery pieces', 1, TRUE),
(2,  'Fine Art Prints', 'fine-art-prints', 'Giclee & archival pigment prints on cotton card', 2, TRUE),
(3,  'Stickers',        'stickers',        'Die-cut, holographic & waterproof sticker packs', 3, TRUE),
(4,  'Keychains',       'keychains',       'Acrylic & metal keychains', 4, TRUE),
(5,  'Acrylic Charms',  'acrylic-charms',  'Glitter epoxy charms, stands, and acrylic accessories', 5, TRUE),
(6,  'Merchandise',     'merchandise',     'Enamel pins, totes, apparel & convention merch', 6, TRUE),
(7,  'Bundles',         'bundles',         'Curated multi-item bazaar bundles', 7, TRUE),
(8,  'Custom Artwork',  'custom-artwork',  'Bespoke digital & traditional commissions', 8, TRUE),
(9,  'Commission Slots','commission-slots','Open atelier commission slots', 9, TRUE),
(10, 'Other',           'other',           'Miscellaneous artisan goods', 10, TRUE)
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Promotions ----------
INSERT INTO Promotions (Name, Description, DiscountType, DiscountValue, StartsAt, EndsAt, IsActive) VALUES
('September Payday Sale', '10% off all fine art prints', 'PERCENT', 10, '2026-09-20 00:00:00', '2026-09-30 23:59:59', TRUE),
('Gala Comicon Week', '15% off booth-exclusive merch', 'PERCENT', 15, '2026-10-01 00:00:00', '2026-10-08 23:59:59', TRUE),
('August Clearance', '₱50 off select stickers', 'FIXED', 50, '2026-08-01 00:00:00', '2026-08-31 23:59:59', FALSE)
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Store locations ----------
INSERT INTO StoreLocations (Id, Name, Venue, Address, City, Region, IsActive) VALUES
(1, 'STAR:DOM @ Robinson''s Galleria South', 'Ground Floor Activity Center', 'KM 31 National Highway, Near Main Atrium', 'San Pedro', 'Laguna / South Luzon', TRUE),
(2, 'STAR:DOM @ SM City Santa Rosa',         'Mall Expansion Wing, 2nd Floor', 'Santa Rosa-Tagaytay Road', 'Santa Rosa', 'Laguna / South Luzon', TRUE),
(3, 'STAR:DOM @ Festival Mall Alabang',      'Water Garden Hallway, Upper Ground', 'Filinvest City', 'Muntinlupa', 'Metro Manila South', TRUE),
(4, 'STAR:DOM @ Ayala Malls South Park',     'Level 3 Main Cinema Foyer', 'Alabang-Zapote Road', 'Muntinlupa', 'Metro Manila South', TRUE),
(5, 'STAR:DOM @ Robinsons Place Manila',     'Midtown Atrium Stage', 'Adriatico Street, Ermita', 'Manila', 'Metro Manila Central', TRUE)
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Pop-up events ----------
INSERT INTO PopUpEvents (Id, LocationId, Name, Description, StartDate, EndDate, OpenTime, CloseTime, BoothNumber,
                         VenueDetail, Status, FeaturedGuest, IsCurrent, LineupText) VALUES
(1, 1, 'STAR:DOM @ Robinson''s Galleria South', 'Touch prints, inspect merchandise, watch live sketching, and pay instantly via local Philippine payment rails. Convention sticker sheets and on-site custom sketch slots available.',
 '2026-09-04 10:00:00', '2026-09-07 21:00:00', '10:00 AM', '9:00 PM', 'Stall A-12', 'Ground Atrium Activity Center', 'NOW OPEN', 'Mika S. & Guild', TRUE, '15 Guest Creators'),
(2, 2, 'STAR:DOM @ SM City Santa Rosa', 'South Luzon Artisan Weekend Expo. Over 30 creators joining our combined pavilion with print swaps, sticker rallies, and live tablet painting demo sessions.',
 '2026-09-12 10:00:00', '2026-09-14 21:00:00', '10:00 AM', '9:00 PM', 'Booth D-04', 'Ground Atrium (Booth D-04)', 'UPCOMING', '@Kira_Illustration & Guild', FALSE, '12 Guest Creators'),
(3, 3, 'STAR:DOM @ Festival Mall Alabang', 'Metro Manila south bazaar with watercolor demos and gacha sticker dispensers.',
 '2026-09-19 10:00:00', '2026-09-21 21:00:00', '10:00 AM', '9:00 PM', 'Island F', 'Carousel Court (Island F)', 'UPCOMING', 'Renzo Cruz', FALSE, '18 Guest Creators'),
(4, 4, 'STAR:DOM @ Ayala Malls South Park', 'Indie comic & print exhibition, live ink sketches open at 11:00 AM daily.',
 '2026-10-02 10:00:00', '2026-10-04 21:00:00', '10:00 AM', '9:00 PM', 'Central Pod', 'Level 2 Activity Area', 'UPCOMING', 'Puffu Studio', FALSE, '10 Guest Creators'),
(5, 5, 'STAR:DOM @ Robinsons Place Manila', 'Midtown art fair with enamel pin rallies and convention merch.',
 '2026-10-16 10:00:00', '2026-10-18 21:00:00', '10:00 AM', '9:00 PM', 'Midtown Wing', 'Midtown Atrium Stage', 'UPCOMING', 'Guild Collective', FALSE, '25 Guest Creators'),
(6, 1, 'SM Southmall Artisan Fair', 'Historical tour run — August artisan fair.',
 '2026-08-21 10:00:00', '2026-08-24 21:00:00', '10:00 AM', '9:00 PM', 'Stall K-02', 'Activity Center', 'ENDED', '', FALSE, ''),
(7, 2, 'U.P. Town Center Art Bazaar', 'Historical tour run — university town bazaar.',
 '2026-08-08 10:00:00', '2026-08-10 21:00:00', '10:00 AM', '8:00 PM', 'Block 3', 'Open Plaza', 'ENDED', '', FALSE, '')
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Commissions ----------
INSERT INTO Commissions (Id, CommissionNumber, CustomerId, MerchantId, CategoryId, Title, Description, Quantity,
                         PreferredSize, PreferredDeadline, BudgetMin, BudgetMax, AdditionalNotes, FinalPrice,
                         EstimatedCompletionDate, MerchantNotes, DepositAmount, Status, CreatedAt) VALUES
(1, 'REQ-2026-084', 1, 5, 3, 'Cosmic Cyber-Cat Sticker Set',
 'I would like a set of custom die-cut vinyl stickers of a cosmic cyber-cat mascot wearing a futuristic space visor. Glossy holographic finish, bright neon magenta and cyan highlights. These will be sold at our local gaming booth and handed out as collector badges.',
 150, '3.5 x 3.5 inches die-cut', '2026-10-28 00:00:00', 3500.00, 5000.00, 'Please include a 2mm white bleed border around the cat ears for clean die-cutting.',
 NULL, NULL, '', NULL, 'PENDING REVIEW', '2026-09-01 10:30:00'),
(2, 'REQ-2026-083', 6, 5, 8, 'Sneak Peek Character Bust',
 'A bust-up illustration of my OC in your cyberpunk style. Want a neon city background.',
 1, 'A4 digital', '2026-10-05 00:00:00', 1500.00, 2500.00, '',
 NULL, NULL, '', NULL, 'CLARIFICATION REQUESTED', '2026-08-30 15:00:00'),
(3, 'REQ-2026-082', 1, 5, 2, 'Convention Poster Art (11x17)',
 'Full-bleed print poster for our booth wall featuring the guardian mech.',
 1, '11x17 metallic matte', '2026-10-15 00:00:00', 4000.00, 6000.00, '',
 4800.00, '2026-11-02 00:00:00', 'Includes 2 rounds of revisions and booth-ready export.', 960.00, 'OFFER SENT', '2026-08-28 09:00:00'),
(4, 'REQ-2026-081', 6, 5, 1, 'Watercolor Pet Portrait',
 'Portrait of my shiba inu with gold filigree frame, similar to your sample.',
 1, 'A5 watercolor', '2026-09-30 00:00:00', 2000.00, 2500.00, '',
 2200.00, '2026-09-22 00:00:00', 'Physical mail via LBC included.', 440.00, 'IN PRODUCTION', '2026-08-25 11:00:00'),
(5, 'REQ-2026-080', 1, 5, 8, 'Floral Wedding Invitation Watercolor',
 'Watercolor botanical accents for wedding invitation cards.',
 1, 'A5 suite', '2026-09-20 00:00:00', 2500.00, 3000.00, '',
 2600.00, '2026-09-15 00:00:00', 'Delivered digitally with print-ready files.', 520.00, 'COMPLETED', '2026-08-20 13:00:00'),
(6, 'REQ-2026-079', 6, 5, 3, 'Twitch Emote Pack',
 'Six emotes for my stream channel, chibi style, matching the raised-slot sample.',
 1, '512x512 PNG', '2026-09-05 00:00:00', 700.00, 1000.00, '',
 850.00, '2026-09-02 00:00:00', '2 rounds of tweaks included.', 170.00, 'DECLINED', '2026-08-18 09:00:00');

INSERT INTO CommissionStatusHistory (CommissionId, FromStatus, ToStatus, ChangedBy, Note, CreatedAt) VALUES
(1, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-09-01 10:30:00'),
(2, '', 'SUBMITTED', 'Juan Dela Cruz', 'Customer submitted request', '2026-08-30 15:00:00'),
(2, 'SUBMITTED', 'CLARIFICATION REQUESTED', 'STAR:DOM Admin', 'Need reference for the OC', '2026-08-31 09:00:00'),
(3, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-08-28 09:00:00'),
(3, 'SUBMITTED', 'OFFER SENT', 'STAR:DOM Admin', 'Artist accepted and sent offer', '2026-08-29 11:00:00'),
(4, '', 'SUBMITTED', 'Juan Dela Cruz', 'Customer submitted request', '2026-08-25 11:00:00'),
(4, 'SUBMITTED', 'OFFER SENT', 'STAR:DOM Admin', 'Artist accepted and sent offer', '2026-08-26 10:00:00'),
(4, 'OFFER SENT', 'CUSTOMER CONFIRMED', 'Juan Dela Cruz', 'Customer confirmed the offer', '2026-08-26 16:00:00'),
(4, 'CUSTOMER CONFIRMED', 'PAID', 'Juan Dela Cruz', 'Payment recorded', '2026-08-27 09:30:00'),
(4, 'PAID', 'IN PRODUCTION', 'STAR:DOM Admin', 'Production started', '2026-08-27 14:00:00'),
(5, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-08-20 13:00:00'),
(5, 'SUBMITTED', 'OFFER SENT', 'STAR:DOM Admin', 'Artist accepted and sent offer', '2026-08-21 10:00:00'),
(5, 'OFFER SENT', 'CUSTOMER CONFIRMED', 'Bella Santos', 'Customer confirmed the offer', '2026-08-21 15:00:00'),
(5, 'CUSTOMER CONFIRMED', 'PAID', 'Bella Santos', 'Payment recorded', '2026-08-22 08:00:00'),
(5, 'PAID', 'IN PRODUCTION', 'STAR:DOM Admin', 'Production started', '2026-08-22 11:00:00'),
(5, 'IN PRODUCTION', 'FINALIZED', 'STAR:DOM Admin', 'Work finalized', '2026-09-10 09:00:00'),
(5, 'FINALIZED', 'COMPLETED', 'STAR:DOM Admin', 'Delivered to customer', '2026-09-11 09:00:00'),
(6, '', 'SUBMITTED', 'Juan Dela Cruz', 'Customer submitted request', '2026-08-18 09:00:00'),
(6, 'SUBMITTED', 'DECLINED', 'STAR:DOM Admin', 'Slot already full', '2026-08-19 10:00:00');

INSERT INTO CommissionMessages (CommissionId, SenderId, Message, IsRead, CreatedAt) VALUES
(2, 5, 'Hi Juan! Could you share a reference of your OC, or a color palette? Also, do you want the bust at waist level or shoulders-up?', FALSE, '2026-08-31 09:02:00'),
(3, 1, 'Hi! Looking forward to the poster! Will the file include a print-ready 300dpi version?', FALSE, '2026-08-31 17:30:00');

-- ---------- Notifications ----------
INSERT INTO Notifications (UserId, Title, Message, NotificationType, LinkPath, IsRead, CreatedAt) VALUES
(1, 'Welcome to STAR:DOM!', 'Your CUSTOMER account is ready. Explore the marketplace!', 'SYSTEM', 'marketplace', TRUE, '2026-08-20 09:00:00'),
(1, 'Commission submitted', 'Your request REQ-2026-084 is now PENDING REVIEW.', 'COMMISSION', 'commission-hub', FALSE, '2026-09-01 10:30:00'),
(5, 'New commission', 'Bella Santos submitted a new request (REQ-2026-084).', 'COMMISSION', 'commission-pipeline', FALSE, '2026-09-01 10:30:00'),
(5, 'Event node live', 'Galleria South is NOW OPEN. POS terminal active.', 'EVENT', 'merchant-dashboard', TRUE, '2026-09-04 09:00:00'),
(6, 'Commission update', 'Your request REQ-2026-081 is IN PRODUCTION.', 'COMMISSION', 'commission-hub', FALSE, '2026-08-27 14:00:00');

-- ============================================================
-- STAR:DOM — Seed data from ARTSHOP DATABASE (100 items)
-- Replaces previous demo catalog with official 100 Artshop items
-- Includes image wiring from D:\STARDOM\STAR-DOM-Web\STAR-DOM-Web\Assets
-- ============================================================


-- Disable foreign key checks for clean table reset

TRUNCATE TABLE EventSales CASCADE;
TRUNCATE TABLE EventInventory CASCADE;
TRUNCATE TABLE OrderItems CASCADE;
TRUNCATE TABLE CartItems CASCADE;
TRUNCATE TABLE WishlistItems CASCADE;
TRUNCATE TABLE Reviews CASCADE;
TRUNCATE TABLE BundleItems CASCADE;
TRUNCATE TABLE Bundles CASCADE;
TRUNCATE TABLE ProductImages CASCADE;
TRUNCATE TABLE ProductVariants CASCADE;
TRUNCATE TABLE Products CASCADE;


-- ---------- Insert 100 ARTSHOP Products ----------
INSERT INTO Products (Id, MerchantId, CategoryId, Name, Slug, Description, BasePrice, SalePrice, StockQuantity,
                      LowStockThreshold, Sku, BrandName, IsActive, IsFeatured, IsBoothExclusive, IsEventExclusive, BadgeLabel, MaterialDetails, RatingAvg, RatingCount, SoldCount) VALUES
(1, 4, 3, 'Bleeding heart', 'bleeding-heart-sticker', 'Authentic sticker by Puffu Studio. In Stock (RESTOCK 6)', 30.00, NULL, 21, 3, 'SKU-AS-0001', 'Puffu Studio', TRUE, TRUE, TRUE, TRUE, 'POPULAR', 'Die-cut sticker, Matte finish', 5.00, 1, 5),
(2, 4, 3, 'Tamaraw', 'tamaraw-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 16)', 30.00, NULL, 16, 3, 'SKU-AS-0002', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'POPULAR', 'Die-cut sticker, Matte finish', 0.00, 0, 8),
(3, 4, 3, 'Tarsier', 'tarsier-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0003', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 11),
(4, 4, 3, 'Goby', 'goby-sticker', 'Authentic sticker by Puffu Studio. Low Stock (RESTOCK 12)', 30.00, NULL, 12, 3, 'SKU-AS-0004', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 14),
(5, 4, 3, 'Kalaw', 'kalaw-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0005', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 17),
(6, 4, 3, 'Irrawady', 'irrawady-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 5)', 30.00, NULL, 5, 3, 'SKU-AS-0006', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 20),
(7, 4, 3, 'Deer', 'deer-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0007', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 1, 23),
(8, 4, 3, 'Punch', 'punch-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0008', 'Puffu Studio', TRUE, TRUE, TRUE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 26),
(9, 4, 3, 'Hollanov', 'hollanov-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 26)', 30.00, NULL, 26, 3, 'SKU-AS-0009', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 29),
(10, 4, 3, 'Hollander', 'hollander-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0010', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 32),
(11, 4, 3, 'Rozanov', 'rozanov-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 11, 3, 'SKU-AS-0011', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 35),
(12, 4, 3, 'Good Boy', 'good-boy-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 10)', 30.00, NULL, 38, 3, 'SKU-AS-0012', 'Puffu Studio', TRUE, TRUE, FALSE, TRUE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 38),
(13, 4, 3, 'Good Girl', 'good-girl-sticker', 'Authentic sticker by Puffu Studio. In Stock (9(otherdesign) 14)', 30.00, NULL, 52, 3, 'SKU-AS-0013', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 1, 1),
(14, 4, 3, 'Gay af', 'gay-af-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 20, 3, 'SKU-AS-0014', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 4),
(15, 4, 3, 'Tobio', 'tobio-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 21, 3, 'SKU-AS-0015', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 7),
(16, 4, 3, 'Hinata', 'hinata-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 21, 3, 'SKU-AS-0016', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 0, 10),
(17, 4, 3, 'Zigzagoon', 'zigzagoon-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 1)', 30.00, NULL, 1, 3, 'SKU-AS-0017', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 13),
(18, 4, 3, 'PHM', 'phm-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 6)', 30.00, NULL, 6, 3, 'SKU-AS-0018', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 16),
(19, 4, 3, 'Life is lifing', 'life-is-lifing-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 0)', 30.00, NULL, 20, 3, 'SKU-AS-0019', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 1, 19),
(20, 4, 3, 'Fame whore', 'fame-whore-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 4)', 30.00, NULL, 24, 3, 'SKU-AS-0020', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 22),
(21, 4, 3, 'Jade', 'jade-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 20, 3, 'SKU-AS-0021', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 25),
(22, 4, 3, 'Santan', 'santan-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 7)', 30.00, NULL, 31, 3, 'SKU-AS-0022', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 0, 28),
(23, 4, 3, 'hello', 'hello-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0023', 'Puffu Studio', TRUE, FALSE, FALSE, TRUE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 31),
(24, 4, 3, 'dont kys', 'dont-kys-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 0)', 30.00, NULL, 20, 3, 'SKU-AS-0024', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 34),
(25, 4, 3, 'Dinostarz', 'dinostarz-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Low Stock (Notes)', 120.00, NULL, 4, 3, 'SKU-AS-0025', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 1, 37),
(26, 4, 3, 'fishies', 'fishies-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Restock (Notes)', 80.00, NULL, 1, 3, 'SKU-AS-0026', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 0),
(27, 4, 3, 'smiskis', 'smiskis-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Low Stock (Notes)', 80.00, NULL, 6, 3, 'SKU-AS-0027', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 3),
(28, 4, 3, 'starcraze', 'starcraze-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0028', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 0, 6),
(29, 4, 3, 'skulz', 'skulz-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 120.00, NULL, 5, 3, 'SKU-AS-0029', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 9),
(30, 3, 2, 'Trees(4x6")', 'trees-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 100.00, NULL, 5, 3, 'SKU-AS-0030', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 12),
(31, 2, 2, 'Trees (5x7")', 'trees-5x7-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 2, 3, 'SKU-AS-0031', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 5.00, 1, 15),
(32, 3, 2, 'Heated Rivalry (4x6")', 'heated-rivalry-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 100.00, NULL, 10, 3, 'SKU-AS-0032', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 18),
(33, 2, 2, 'Maya  (5x7")', 'maya-5x7-art-print', 'Authentic art print by Mika Visuals. Low Stock (2)', 120.00, NULL, 3, 3, 'SKU-AS-0033', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Archival art print, Matte finish', 4.50, 1, 21),
(34, 3, 2, 'Bleeding Fame  (5x7")', 'bleeding-fame-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. Low Stock (Notes)', 120.00, NULL, 4, 3, 'SKU-AS-0034', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Archival art print, Matte finish', 5.00, 0, 24),
(35, 2, 2, 'Maral  (5x7")', 'maral-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (2)', 120.00, NULL, 6, 3, 'SKU-AS-0035', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 4.50, 1, 27),
(36, 3, 2, 'Rafflesia  (5x7")', 'rafflesia-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0036', 'Renzo Cruz Atelier', TRUE, FALSE, TRUE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 30),
(37, 2, 2, 'align  (5x7")', 'align-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0037', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 5.00, 1, 33),
(38, 3, 2, 'Space (5x7")', 'space-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. Low Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0038', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Archival art print, Matte finish', 0.00, 0, 36),
(39, 2, 2, 'grace  (5x7")', 'grace-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0039', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 4.50, 1, 39),
(40, 3, 2, 'Olruggio', 'olruggio-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 120.00, NULL, 1, 3, 'SKU-AS-0040', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 5.00, 0, 2),
(41, 2, 2, 'ticket (4x6")', 'ticket-4x6-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0041', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 4.50, 1, 5),
(42, 3, 2, 'bawal umihi d2 (4x6")', 'bawal-umihi-d2-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 1, 3, 'SKU-AS-0042', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 8),
(43, 2, 2, 'A.I.  (4x6")', 'a-i-4x6-art-print', 'Authentic art print by Mika Visuals. Low Stock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0043', 'Mika Visuals', TRUE, FALSE, TRUE, FALSE, 'LOW STOCK', 'Archival art print, Matte finish', 5.00, 1, 11),
(44, 3, 2, 'Beetle', 'beetle-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 4, 3, 'SKU-AS-0044', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 14),
(45, 2, 2, 'Peacock', 'peacock-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 3, 3, 'SKU-AS-0045', 'Mika Visuals', TRUE, FALSE, FALSE, TRUE, 'RESTOCK', 'Archival art print, Matte finish', 4.50, 1, 17),
(46, 2, 6, 'bleh', 'bleh-button-pins', 'Authentic button pins by Guild Collective. Out of Stock (Notes)', 35.00, NULL, 0, 3, 'SKU-AS-0046', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', '1.25 in. pinback button', 5.00, 0, 20),
(47, 2, 6, 'Bleed', 'bleed-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 4, 3, 'SKU-AS-0047', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 4.50, 1, 23),
(48, 2, 6, 'bangus', 'bangus-button-pins', 'Authentic button pins by Guild Collective. Low Stock (Notes)', 35.00, NULL, 5, 3, 'SKU-AS-0048', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', '1.25 in. pinback button', 0.00, 0, 26),
(49, 2, 6, 'i luv stars', 'i-luv-stars-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 3, 3, 'SKU-AS-0049', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 5.00, 1, 29),
(50, 2, 6, 'gay af', 'gay-af-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 2, 3, 'SKU-AS-0050', 'Guild Collective', TRUE, FALSE, TRUE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 32),
(51, 2, 6, 'doggo', 'doggo-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 2, 3, 'SKU-AS-0051', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 4.50, 1, 35),
(52, 2, 6, 'nerdz', 'nerdz-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 1, 3, 'SKU-AS-0052', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 5.00, 0, 38),
(53, 2, 6, 'evil eye', 'evil-eye-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 1, 3, 'SKU-AS-0053', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 4.50, 1, 1),
(54, 2, 6, 'star', 'star-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 4, 3, 'SKU-AS-0054', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 4),
(55, 2, 6, 'phm', 'phm-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 3, 3, 'SKU-AS-0055', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 5.00, 1, 7),
(56, 3, 4, 'Goby', 'goby-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0056', 'RedFox Workshop', TRUE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 10),
(57, 3, 4, 'Pigeon', 'pigeon-keychains', 'Authentic keychains by RedFox Workshop. Restock (Notes)', 150.00, NULL, 1, 3, 'SKU-AS-0057', 'RedFox Workshop', TRUE, FALSE, TRUE, FALSE, 'RESTOCK', 'Durable acrylic keychain', 4.50, 1, 13),
(58, 3, 4, 'Bread tag', 'bread-tag-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0058', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 5.00, 0, 16),
(59, 3, 4, 'Maral', 'maral-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0059', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 4.50, 1, 19),
(60, 3, 4, 'Tamaraw', 'tamaraw-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 5, 3, 'SKU-AS-0060', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 22),
(61, 3, 4, 'Phyton', 'phyton-keychains', 'Authentic keychains by RedFox Workshop. Out of Stock (Notes)', 150.00, NULL, 0, 3, 'SKU-AS-0061', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Durable acrylic keychain', 5.00, 1, 25),
(62, 3, 4, 'santan', 'santan-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 80.00, NULL, 4, 3, 'SKU-AS-0062', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 28),
(63, 3, 4, 'jade', 'jade-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 80.00, NULL, 3, 3, 'SKU-AS-0063', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 4.50, 1, 31),
(64, 3, 4, 'Webbing keychain', 'webbing-keychain-keychains', 'Authentic keychains by RedFox Workshop. In Stock (Notes)', 120.00, NULL, 21, 3, 'SKU-AS-0064', 'RedFox Workshop', TRUE, FALSE, TRUE, FALSE, '', 'Durable acrylic keychain', 5.00, 0, 34),
(65, 2, 6, 'pigeon', 'pigeon-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0065', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 4.50, 1, 37),
(66, 2, 6, 'phyton', 'phyton-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0066', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 0),
(67, 2, 6, 'leopard', 'leopard-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0067', 'Guild Collective', TRUE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Skin-safe temporary tattoo', 5.00, 1, 3),
(68, 2, 6, 'fish', 'fish-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0068', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 6),
(69, 2, 6, 'tamaraw', 'tamaraw-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0069', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 4.50, 1, 9),
(70, 4, 3, 'Disappoint your parents', 'disappoint-your-parents-sticker', 'Authentic sticker by Puffu Studio. Low Stock (5 11)', 30.00, NULL, 11, 3, 'SKU-AS-0070', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 12),
(71, 4, 3, 'One day at a time', 'one-day-at-a-time-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 17, 3, 'SKU-AS-0071', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 15),
(72, 4, 3, 'Know it''s for the better', 'know-it-s-for-the-better-sticker', 'Authentic sticker by Puffu Studio. Low Stock (10 13)', 30.00, NULL, 13, 3, 'SKU-AS-0072', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 18),
(73, 4, 3, 'ICU paint tube', 'icu-paint-tube-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0073', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 1, 21),
(74, 4, 3, 'Drowning risk', 'drowning-risk-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0074', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 24),
(75, 4, 3, 'Button girl', 'button-girl-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 7)', 30.00, NULL, 7, 3, 'SKU-AS-0075', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 27),
(76, 4, 3, 'Nothing matters', 'nothing-matters-sticker', 'Authentic sticker by Puffu Studio. In Stock (4 8)', 30.00, NULL, 36, 3, 'SKU-AS-0076', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 0, 30),
(77, 4, 3, 'Tomorrow will be better', 'tomorrow-will-be-better-sticker', 'Authentic sticker by Puffu Studio. In Stock (6 15)', 30.00, NULL, 15, 3, 'SKU-AS-0077', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 33),
(78, 4, 3, 'Always an angel', 'always-an-angel-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 15)', 30.00, NULL, 15, 3, 'SKU-AS-0078', 'Puffu Studio', TRUE, FALSE, TRUE, TRUE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 36),
(79, 4, 3, 'Girl', 'girl-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 14)', 30.00, NULL, 42, 3, 'SKU-AS-0079', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 1, 39),
(80, 4, 3, 'Phone', 'phone-sticker', 'Authentic sticker by Puffu Studio. Out of Stock (Notes 0)', 30.00, NULL, 0, 3, 'SKU-AS-0080', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 2),
(81, 4, 3, 'World', 'world-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0081', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 5),
(82, 4, 3, 'Lapida', 'lapida-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 9)', 30.00, NULL, 9, 3, 'SKU-AS-0082', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 8),
(83, 4, 3, 'Waiting', 'waiting-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 6)', 30.00, NULL, 32, 3, 'SKU-AS-0083', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 11),
(84, 4, 3, 'Affirmations', 'affirmations-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 17)', 30.00, NULL, 17, 3, 'SKU-AS-0084', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 14),
(85, 4, 3, 'idk lol', 'idk-lol-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 20, 3, 'SKU-AS-0085', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 1, 17),
(86, 4, 3, 'i forgor', 'i-forgor-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 26, 3, 'SKU-AS-0086', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 20),
(87, 4, 3, 'i don car', 'i-don-car-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 13)', 30.00, NULL, 37, 3, 'SKU-AS-0087', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 23),
(88, 4, 3, 'eleveneleven', 'eleveneleven-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 6)', 30.00, NULL, 6, 3, 'SKU-AS-0088', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 26),
(89, 4, 3, 'paper doll', 'paper-doll-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 11)', 30.00, NULL, 11, 3, 'SKU-AS-0089', 'Puffu Studio', TRUE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 29),
(90, 4, 3, 'we ball', 'we-ball-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 11)', 30.00, NULL, 39, 3, 'SKU-AS-0090', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 32),
(91, 4, 3, 'war', 'war-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes)', 30.00, NULL, 0, 3, 'SKU-AS-0091', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 5.00, 1, 35),
(92, 4, 3, 'honeybee', 'honeybee-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes)', 30.00, NULL, 12, 3, 'SKU-AS-0092', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 38),
(93, 4, 3, 'love', 'love-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 70.00, NULL, 8, 3, 'SKU-AS-0093', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 4.50, 1, 1),
(94, 3, 10, 'Desk buddies (Pokemon)', 'desk-buddies-pokemon-clay-deskbuddies', 'Authentic clay deskbuddies by RedFox Workshop. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0094', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay desk buddy', 5.00, 0, 4),
(95, 3, 10, 'Desk buddies (Dinosaurs)', 'desk-buddies-dinosaurs-clay-deskbuddies', 'Authentic clay deskbuddies by RedFox Workshop. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0095', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay desk buddy', 4.50, 1, 7),
(96, 2, 6, 'Pins (big)', 'pins-big-clay-pins', 'Authentic clay pins by Guild Collective. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0096', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay pin', 0.00, 0, 10),
(97, 2, 6, 'Pins (small)', 'pins-small-clay-pins', 'Authentic clay pins by Guild Collective. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0097', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay pin', 5.00, 1, 13),
(98, 3, 2, 'Forwards beckon rebound', 'forwards-beckon-rebound-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 7, 3, 'SKU-AS-0098', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 16),
(99, 2, 2, 'Everything stays', 'everything-stays-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 5, 3, 'SKU-AS-0099', 'Mika Visuals', TRUE, FALSE, TRUE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 4.50, 1, 19),
(100, 3, 2, 'Honeybee', 'honeybee-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0100', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, TRUE, 'RESTOCK', 'Archival art print, Matte finish', 5.00, 0, 22);

-- ---------- Product Images (From Assets) ----------
INSERT INTO ProductImages (ProductId, ImageFile, IsPrimary, SortOrder) VALUES
(1, '/Assets/Stickers/Bleeding heart.webp', TRUE, 1),
(2, '/Assets/Stickers/Tamaraw.webp', TRUE, 1),
(3, '/Assets/Stickers/Tarsier.webp', TRUE, 1),
(4, '/Assets/Stickers/Goby.webp', TRUE, 1),
(5, '/Assets/Stickers/Kalaw.webp', TRUE, 1),
(6, '/Assets/Stickers/Irrawady.webp', TRUE, 1),
(7, '/Assets/Stickers/Philippine Deer.webp', TRUE, 1),
(8, '/Assets/Stickers/Punch.webp', TRUE, 1),
(9, '/Assets/Stickers/Hollanov.webp', TRUE, 1),
(10, '/Assets/Stickers/Hollander.webp', TRUE, 1),
(11, '/Assets/Stickers/Rozanov.webp', TRUE, 1),
(12, '/Assets/Stickers/Good boy.webp', TRUE, 1),
(13, '/Assets/Stickers/Good girl.webp', TRUE, 1),
(14, '/Assets/Stickers/Gay af.webp', TRUE, 1),
(16, '/Assets/Stickers/Hinata.webp', TRUE, 1),
(17, '/Assets/Stickers/Zigzagoon.webp', TRUE, 1),
(18, '/Assets/Stickers/Project Hail Mary.webp', TRUE, 1),
(19, '/Assets/Stickers/Life is lifing.webp', TRUE, 1),
(20, '/Assets/Stickers/Fame whore.webp', TRUE, 1),
(26, '/Assets/Sticker Sheets/Fishies (4x3).webp', TRUE, 1),
(27, '/Assets/Sticker Sheets/Smiskis (4x3).webp', TRUE, 1),
(30, '/Assets/Art Prints/Trees (4x6).webp', TRUE, 1),
(32, '/Assets/Art Prints/Heated Rivalry (4x6).webp', TRUE, 1),
(33, '/Assets/Art Prints/Maya (5x7).webp', TRUE, 1),
(34, '/Assets/Art Prints/Bleeding Heart Pigeon (5x7).webp', TRUE, 1),
(35, '/Assets/Art Prints/Maral (5x7).webp', TRUE, 1),
(37, '/Assets/Art Prints/Stars will align (5x7).webp', TRUE, 1),
(39, '/Assets/Art Prints/Grace (5x7).webp', TRUE, 1),
(40, '/Assets/Art Prints/Olruggio (4x6).webp', TRUE, 1),
(42, '/Assets/Art Prints/Bawal umihi d2 (4x6).webp', TRUE, 1),
(49, '/Assets/Button Pins/Please I am a Star.webp', TRUE, 1),
(50, '/Assets/Button Pins/Gay af.webp', TRUE, 1),
(51, '/Assets/Button Pins/Doggo.webp', TRUE, 1),
(52, '/Assets/Button Pins/I love Nerdz.webp', TRUE, 1),
(53, '/Assets/Button Pins/Evil eye.webp', TRUE, 1),
(54, '/Assets/Button Pins/Please I am a Star.webp', TRUE, 1),
(55, '/Assets/Button Pins/Project Hail Mary.webp', TRUE, 1),
(56, '/Assets/Keychains/Goby keychain.webp', TRUE, 1),
(57, '/Assets/Keychains/Pigeon keychain.webp', TRUE, 1),
(58, '/Assets/Keychains/Bread tag keychain.webp', TRUE, 1),
(70, '/Assets/Stickers/Disappoint your parents.webp', TRUE, 1),
(71, '/Assets/Stickers/One day at a time.webp', TRUE, 1),
(73, '/Assets/Stickers/ICU Paint Tube.webp', TRUE, 1),
(74, '/Assets/Stickers/Drowning risk.webp', TRUE, 1),
(75, '/Assets/Stickers/Button Girl.webp', TRUE, 1),
(76, '/Assets/Stickers/Nothing matters.webp', TRUE, 1),
(77, '/Assets/Stickers/Tomorrow will be better.webp', TRUE, 1),
(78, '/Assets/Stickers/Always an angel.webp', TRUE, 1),
(79, '/Assets/Stickers/Button Girl.webp', TRUE, 1),
(80, '/Assets/Stickers/Phone.webp', TRUE, 1),
(81, '/Assets/Stickers/Spite the world.webp', TRUE, 1),
(82, '/Assets/Stickers/Lapida.webp', TRUE, 1),
(83, '/Assets/Stickers/What are you waiting for.webp', TRUE, 1),
(84, '/Assets/Stickers/Affirmations.webp', TRUE, 1),
(85, '/Assets/Stickers/idk lol.webp', TRUE, 1),
(86, '/Assets/Stickers/i forgor.webp', TRUE, 1),
(89, '/Assets/Stickers/Paper doll.webp', TRUE, 1),
(93, '/Assets/Sticker Sheets/Love sticker sheet (3x4).webp', TRUE, 1);

-- ---------- Event inventory (Galleria South tour) ----------
INSERT INTO EventInventory (EventId, ProductId, StartingStock, SoldQuantity, RemainingStock, IsEventExclusive, IsActive) VALUES
(1, 1, 25, 4, 21, FALSE, TRUE),
(1, 2, 20, 4, 16, FALSE, TRUE),
(1, 3, 15, 5, 10, FALSE, TRUE),
(1, 25, 10, 6, 4, FALSE, TRUE),
(1, 30, 10, 5, 5, FALSE, TRUE),
(1, 46, 10, 10, 0, TRUE, TRUE),
(1, 56, 12, 8, 4, FALSE, TRUE),
(1, 65, 8, 5, 3, FALSE, TRUE);

-- ---------- Event sales (in-person POS for active run) ----------
INSERT INTO EventSales (EventId, OrderId, ProductId, Quantity, UnitPrice, TotalAmount, SaleType, PaymentMethod, SaleDate, Notes) VALUES
(1, NULL, 1, 2, 30.00, 60.00, 'IN_PERSON', 'GCASH', '2026-09-04 11:20:00', 'Walk-in'),
(1, NULL, 25, 1, 120.00, 120.00, 'QR',       'MAYA',  '2026-09-04 13:05:00', 'QR scan'),
(1, NULL, 30, 1, 100.00, 100.00, 'IN_PERSON', 'CASH', '2026-09-04 14:40:00', 'Art Print sale'),
(1, NULL, 56, 1, 150.00, 150.00, 'PREORDER', 'GCASH', '2026-09-04 16:10:00', 'Booth pre-order');

-- ---------- Bundles ----------
-- User specs: Stickers Bundle (4 for 100 PHP), Button pins Bundle (3 for 100 PHP)
-- Standard price 4 stickers @ 30 = 120 PHP -> 100 PHP (16.67% discount)
-- Standard price 3 button pins @ 35 = 105 PHP -> 100 PHP (4.76% discount)
INSERT INTO Bundles (Id, Name, Description, DiscountPercent, IsActive) VALUES
(1, 'Stickers Bundle (4 for 100)', 'Choose your favorite 4 stickers (Bleeding heart, Tamaraw, Tarsier, Goby) for only ₱100!', 16.67, TRUE),
(2, 'Button Pins Bundle (3 for 100)', 'Pick 3 premium 1.25 in. button pins (Bleed, Bangus, I luv stars) for only ₱100!', 4.76, TRUE),
(3, 'Artisan Prints & Sheet Set', 'Dinostarz sticker sheet + Trees art print set with matte finish', 15.00, TRUE);

INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES
(1, 1, 1), (1, 2, 1), (1, 3, 1), (1, 4, 1),
(2, 47, 1), (2, 48, 1), (2, 49, 1),
(3, 25, 1), (3, 30, 1);

-- Sync auto-increment sequences after explicit ID inserts
SELECT setval(pg_get_serial_sequence('roles', 'id'), COALESCE(MAX(Id), 1)) FROM Roles;
SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE(MAX(Id), 1)) FROM Users;
SELECT setval(pg_get_serial_sequence('categories', 'id'), COALESCE(MAX(Id), 1)) FROM Categories;
SELECT setval(pg_get_serial_sequence('promotions', 'id'), COALESCE(MAX(Id), 1)) FROM Promotions;
SELECT setval(pg_get_serial_sequence('storelocations', 'id'), COALESCE(MAX(Id), 1)) FROM StoreLocations;
SELECT setval(pg_get_serial_sequence('popupevents', 'id'), COALESCE(MAX(Id), 1)) FROM PopUpEvents;
SELECT setval(pg_get_serial_sequence('products', 'id'), COALESCE(MAX(Id), 1)) FROM Products;
SELECT setval(pg_get_serial_sequence('commissions', 'id'), COALESCE(MAX(Id), 1)) FROM Commissions;
SELECT setval(pg_get_serial_sequence('bundles', 'id'), COALESCE(MAX(Id), 1)) FROM Bundles;
SELECT setval(pg_get_serial_sequence('orders', 'id'), COALESCE(MAX(Id), 1)) FROM Orders;
