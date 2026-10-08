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
--   CUSTOMER        : bella / customer123  (+ miguel, andrea, kim, paolo,
--                     grace — same password, they sign the seeded reviews)
--   ADMIN (owner)   : admin / admin123
--   MERCHANT studios: mika, renzo, puffu -- all share the admin hash
--                     (admin123), because the owner runs the booths.
--
-- The three merchants are not optional: every Products row carries
-- MerchantId 2, 3 or 4, so removing one orphans its part of the catalogue.
-- The extra customers exist only so the reviews below are not all signed by
-- the same person.
--
-- Everything lives in this one file: the 100-item ARTSHOP product catalog,
-- its product images, and the product-linked demo data (store locations, pop-up
-- events, event inventory, bundles, commissions, notifications, reviews). The
-- separate MySQL seed scripts it used to be split across are archived,
-- unreadable, under legacy-mysql/ -- do not run them.
-- ============================================================



-- Clean reset. This has to cover EVERY seeded table, not just the catalog.
-- The previous list stopped at Products, so a second run of the seed hit a
-- duplicate key on Commissions and aborted -- and because the Reviews and
-- store-location rows sit further down the file, they never landed either.
-- One complete TRUNCATE makes the seed re-runnable end to end.
--
-- PaymentSettings is deliberately NOT truncated: it holds the admin-managed
-- GCash / GOtyme QR details, which are configuration rather than demo data,
-- and it upserts on Channel anyway. AppErrors is a log, so it is left alone.
TRUNCATE TABLE
    Reviews, EventSales, EventInventory, OrderItems, CartItems, WishlistItems,
    Payments, Shipping, Receipts, Commissions, BundleItems, Bundles,
    ProductImages, ProductVariants, Orders, Notifications, Cart, Products,
    PopUpEvents, StoreLocations, Promotions, Users, Categories
RESTART IDENTITY CASCADE;


-- ---------- Roles ----------
-- STAR:DOM is a single-owner brand: two roles only. ADMIN is the store owner
-- (and the commission artist); CUSTOMER is the marketplace shopper.
INSERT INTO Roles (Id, Name, Description) VALUES
  (1, 'CUSTOMER', 'Marketplace shopper & commission requester'),
  (3, 'ADMIN',    'Store owner: merchant, studio artist & system admin')
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Users & Creator Studios ----------
-- STAR:DOM is a single-owner brand: only the admin account holds the ADMIN role.
-- Every other account is a CUSTOMER. The demo catalog still shows the studio
-- accounts (Mika, Renzo, Puffu) as product merchants, but their role stays
-- CUSTOMER so the Admin Console reads as a single-admin brand.
INSERT INTO Users (Id, Email, Username, FullName, Phone, PasswordHash, RoleId, Status, EmailVerified,
                   CommissionSlotCapacity, CommissionStartingPrice, CommissionTurnaround, CommissionFormats, CommissionTagline) VALUES
(1,  'bella@example.com',     'bella',     'Bella Santos',       '09171234567', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(2,  'mika@stardom.ph',       'mika',      'Mika Visuals',       '09174441122', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 1, 'ACTIVE', TRUE, 0, 1200.00, '2-4 business days', 'Archival Print + Vector', 'Ethereal Flora & Fauna'),
(3,  'renzo@stardom.ph',      'renzo',     'Renzo Cruz Atelier', '09175553344', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 1, 'ACTIVE', TRUE, 0, 2000.00, '5-7 business days', 'Handmade Acrylic & Ink', 'Contemporary Urban Lore'),
(4,  'puffu@stardom.ph',      'puffu',     'Puffu Studio',       '09176665566', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 1, 'ACTIVE', TRUE, 0, 800.00,  '1-3 business days', 'Die-cut Vinyl & Hologram', 'Cute & Chaotic Sticker Guild'),
(5,  'admin@stardom.ph',      'admin',     'STAR:DOM Admin',     '09281234567', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 3, 'ACTIVE', TRUE, 20, 1500.00, '3-5 business days', 'High-Res PNG + A4 Print', 'Anime & Cyberpunk Stylist'),
-- Five more shoppers. Reviews used to be signed by Bella on all 100 rows,
-- which made the product pages read like one person wrote every review in the
-- shop. These five only exist to sign reviews (plus whatever the demo needs a
-- second shopper for); they all share Bella's customer123 hash.
(6,  'miguel@example.com',    'miguel',    'Miguel Ramos',      '09181234567', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(7,  'andrea@example.com',    'andrea',    'Andrea Dela Cruz',   '09182345678', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(8,  'kim@example.com',       'kim',       'Kim Santos',         '09183456789', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(9,  'paolo@example.com',     'paolo',     'Paolo Aquino',       '09184567890', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(10, 'grace@example.com',     'grace',     'Grace Lim',          '09185678901', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', '')
ON CONFLICT (Id) DO UPDATE SET Email = EXCLUDED.Email, FullName = EXCLUDED.FullName;

-- Ten users. 2/3/4 own the catalog (every Products row has
-- MerchantId 2, 3 or 4, so dropping any of them orphans the catalogue) and 5 is
-- the store owner and the commission artist. Customers are 1 (Bella, the login
-- the demo uses) plus 6-10, who exist so the 28 seeded reviews are signed by
-- six different people instead of one. Bella still authors every seeded
-- commission and notification, so the commission pipeline stays a single
-- shopper's story.

-- ---------- Categories ----------
INSERT INTO Categories (Id, Name, Slug, Description, DisplayOrder, IsActive) VALUES
(2,  'Art Print',       'art-print',       'Giclee & archival pigment prints on cotton card', 2, TRUE),
(3,  'Stickers',        'stickers',        'Die-cut, holographic & waterproof stickers', 3, TRUE),
(11, 'Sticker Sheets',  'sticker-sheets',  'Larger multi-design die-cut sticker sheets', 4, TRUE),
(4,  'Keychains',       'keychains',       'Acrylic & metal keychains', 5, TRUE),
(6,  'Button Pins',     'button-pins',     'Enamel pin designs, 1.25 in.', 6, TRUE),
(10, 'Other',           'other',           'Miscellaneous artisan goods', 10, TRUE),
-- Seeded inactive because the demo product set has nothing in them, and the
-- storefront hides an empty category anyway. Flip to TRUE once products land
-- there; CatalogService only shows categories that hold at least one live SKU.
(1,  'Original Art',    'original-art',    'One-of-a-kind originals and A3/A2 gallery pieces', 1, FALSE),
(5,  'Acrylic Charms',  'acrylic-charms',  'Glitter epoxy charms, stands, and acrylic accessories', 5, FALSE),
(7,  'Bundles',         'bundles',         'Curated multi-item bazaar bundles', 7, FALSE),
(8,  'Custom Artwork',  'custom-artwork',  'Bespoke digital & traditional commissions', 8, FALSE),
(9,  'Commission Slots','commission-slots','Open atelier commission slots', 9, FALSE)
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name, Slug = EXCLUDED.Slug,
  Description = EXCLUDED.Description, DisplayOrder = EXCLUDED.DisplayOrder, IsActive = EXCLUDED.IsActive;

-- ---------- Promotions ----------
INSERT INTO Promotions (Name, Description, DiscountType, DiscountValue, StartsAt, EndsAt, IsActive) VALUES
('September Payday Sale', '10% off all fine art prints', 'PERCENT', 10, '2026-09-20 00:00:00', '2026-09-30 23:59:59', TRUE),
('Gala Comicon Week', '15% off booth-exclusive merch', 'PERCENT', 15, '2026-10-01 00:00:00', '2026-10-08 23:59:59', TRUE),
('August Clearance', '₱50 off select stickers', 'FIXED', 50, '2026-08-01 00:00:00', '2026-08-31 23:59:59', FALSE)
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Store locations ----------
-- Names are stored WITHOUT the "STAR:DOM @ " prefix: every renderer that shows an
-- event/location title prepends the brand itself ("STAR:DOM @ " & Name), so keeping
-- the prefix here made the UI print "STAR:DOM @ STAR:DOM @ ...".
INSERT INTO StoreLocations (Id, Name, Venue, Address, City, Region, IsActive) VALUES
(1, 'Robinson''s Galleria South', 'Ground Floor Activity Center', 'KM 31 National Highway, Near Main Atrium', 'San Pedro', 'Laguna / South Luzon', TRUE),
(2, 'SM City Santa Rosa',         'Mall Expansion Wing, 2nd Floor', 'Santa Rosa-Tagaytay Road', 'Santa Rosa', 'Laguna / South Luzon', TRUE),
(3, 'Festival Mall Alabang',      'Water Garden Hallway, Upper Ground', 'Filinvest City', 'Muntinlupa', 'Metro Manila South', TRUE),
(4, 'Ayala Malls South Park',     'Level 3 Main Cinema Foyer', 'Alabang-Zapote Road', 'Muntinlupa', 'Metro Manila South', TRUE),
(5, 'Robinsons Place Manila',     'Midtown Atrium Stage', 'Adriatico Street, Ermita', 'Manila', 'Metro Manila Central', TRUE),
-- 6 and 7 were missing entirely, so the two historical events below had no venue
-- of their own and borrowed another mall's row (LocationId 1 and 2). Their pick-up
-- shipping address therefore quoted the wrong street.
(6, 'SM Southmall',               'Activity Center', 'Avenida de Legazpi, Brgy. Marcelo', 'San Pedro', 'Laguna / South Luzon', TRUE),
(7, 'U.P. Town Center',           'Open Plaza', 'National Highway, Brgy. Bayanan', 'Santa Rosa', 'Laguna / South Luzon', TRUE)
-- No extra venues. An earlier pass added six more malls (SM North EDSA, Trinoma,
-- SM Megamall, Marquee, SM City Lipa, SM City Pampanga) just to fill the
-- itinerary grid, but there is no artwork for them, so they all rendered as the
-- same gradient placeholder. Seven real venues is plenty: the finished events
-- below are recycled onto future dates instead of inventing malls the artwork
-- library cannot illustrate. Every one of the seven now has its own photo in
-- Assets\Malls, so none of them falls back to the gradient.
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name, Venue = EXCLUDED.Venue,
    Address = EXCLUDED.Address, City = EXCLUDED.City, Region = EXCLUDED.Region,
    IsActive = EXCLUDED.IsActive;

-- ---------- Pop-up events ----------
-- STAR:DOM is a single-owner brand (the admin IS the merchant): no guest-creator
-- lineups. FeaturedGuest/LineupText stay as schema columns for compatibility but
-- are seeded empty and no longer rendered. Every event carries its venue's photo
-- from Assets\Malls; an empty ImageFile falls back to the brand gradient.
--
-- The paths here are only keys. The bytes are loaded into AssetImages by
-- Database\mall_images.sql, and App\AssetImg.aspx serves the database copy with
-- the file as a fallback -- so the itinerary still has pictures on a machine
-- where Assets\Malls was never cloned, or after the folder is deleted by
-- accident. Filenames are slugs (no spaces, no apostrophe) so they survive the
-- query string of that endpoint and a CSS url('...').
INSERT INTO PopUpEvents (Id, LocationId, Name, Description, StartDate, EndDate, OpenTime, CloseTime, BoothNumber,
                         VenueDetail, Status, FeaturedGuest, IsCurrent, LineupText, ImageFile) VALUES
(1, 1, 'Robinson''s Galleria South', 'Touch prints, inspect merchandise, watch live sketching, and pay instantly via local Philippine payment rails. Convention sticker sheets and on-site custom sketch slots available.',
 '2026-10-09 10:00:00', '2026-10-11 21:00:00', '10:00 AM', '9:00 PM', 'Stall A-12', 'Ground Atrium Activity Center', 'UPCOMING', '', FALSE, '', '/Assets/Malls/robinsons-galleria-south.webp'),
(2, 2, 'SM City Santa Rosa', 'South Luzon Artisan Weekend Expo. Print swaps, sticker rallies, and live tablet painting demo sessions at the pavilion.',
 '2026-10-16 10:00:00', '2026-10-18 21:00:00', '10:00 AM', '9:00 PM', 'D-04', 'Ground Atrium (Booth D-04)', 'UPCOMING', '', FALSE, '', '/Assets/Malls/sm-city-santa-rosa.webp'),
(3, 3, 'Festival Mall Alabang', 'Metro Manila south bazaar with watercolor demos and gacha sticker dispensers.',
 '2026-10-23 10:00:00', '2026-10-25 21:00:00', '10:00 AM', '9:00 PM', 'Island F', 'Carousel Court (Island F)', 'UPCOMING', '', FALSE, '', '/Assets/Malls/festival-mall-alabang.webp'),
(4, 4, 'Ayala Malls South Park', 'Indie comic & print exhibition, live ink sketches open at 11:00 AM daily.',
 '2026-10-02 10:00:00', '2026-10-04 21:00:00', '10:00 AM', '9:00 PM', 'Central Pod', 'Level 2 Activity Area', 'UPCOMING', '', FALSE, '', '/Assets/Malls/ayala-south-park.webp'),
(5, 5, 'Robinsons Place Manila', 'Midtown art fair with enamel pin rallies and convention merch. Grand finale of the 2026 tour, with a retrospective wall of the full catalogue.',
 '2026-11-13 10:00:00', '2026-11-15 21:00:00', '10:00 AM', '9:00 PM', 'Midtown Wing', 'Midtown Atrium Stage', 'UPCOMING', '', FALSE, '', '/Assets/Malls/robinsons-place-manila.webp'),
(6, 6, 'SM Southmall Artisan Fair', 'Santa Rosa artisan run — the full sticker catalogue on one wall, plus live sketching slots all day.',
 '2026-10-30 10:00:00', '2026-11-01 21:00:00', '10:00 AM', '9:00 PM', 'Stall K-02', 'Activity Center', 'UPCOMING', '', FALSE, '', '/Assets/Malls/sm-southmall-artisan-fair.webp'),
(7, 7, 'U.P. Town Center Art Bazaar', 'University town bazaar — student-run booths, keychain bar, and portfolio review bookings with the artist.',
 '2026-11-06 10:00:00', '2026-11-08 21:00:00', '10:00 AM', '8:00 PM', 'Block 3', 'Open Plaza', 'UPCOMING', '', FALSE, '', '/Assets/Malls/up-town-center.webp')
-- The tour is RECYCLED, not extended: the same seven venues come back around,
-- one per weekend, rather than six invented malls that only had the gradient
-- placeholder to show for them. Every row above is a re-dating of an event that
-- already existed; only Robinsons Place Manila moved later, to close the tour.
-- Stored Status does not reach the UI -- EventRepository.DeriveStatus recomputes
-- it from the Asia/Manila clock on every read -- so all rows store UPCOMING and
-- the clock decides which one is NOW OPEN.
--
--   Fri 2 - Sun 4 Oct        Ayala Malls South Park   <- live inside the window
--   Fri 9 - Sun 11 Oct       Robinson's Galleria South
--   Fri 16 - Sun 18 Oct      SM City Santa Rosa
--   Fri 23 - Sun 25 Oct      Festival Mall Alabang
--   Fri 30 Oct - Sun 1 Nov   SM Southmall Artisan Fair
--   Fri 6 - Sun 8 Nov        U.P. Town Center Art Bazaar
--   Fri 13 - Sun 15 Nov      Robinsons Place Manila   <- grand finale
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name, Description = EXCLUDED.Description, VenueDetail = EXCLUDED.VenueDetail,
    LocationId = EXCLUDED.LocationId, BoothNumber = EXCLUDED.BoothNumber,
    StartDate = EXCLUDED.StartDate, EndDate = EXCLUDED.EndDate,
    OpenTime = EXCLUDED.OpenTime, CloseTime = EXCLUDED.CloseTime,
    Status = EXCLUDED.Status, FeaturedGuest = EXCLUDED.FeaturedGuest, LineupText = EXCLUDED.LineupText, ImageFile = EXCLUDED.ImageFile;

-- IsCurrent is no longer read by anything: the live booth comes from the
-- Asia/Manila clock via EventRepository.GetCurrentEvent. Rows left TRUE by the
-- old "active node" feature are cleared here so the column cannot disagree with
-- the derived status again.
UPDATE PopUpEvents SET IsCurrent = FALSE WHERE IsCurrent;

-- ---------- Commissions ----------
INSERT INTO Commissions (Id, CommissionNumber, CustomerId, MerchantId, CategoryId, Title, Description, Quantity,
                         PreferredSize, PreferredDeadline, BudgetMin, BudgetMax, AdditionalNotes, FinalPrice,
                         EstimatedCompletionDate, MerchantNotes, DepositAmount, Status, CreatedAt) VALUES
(1, 'REQ-2026-084', 1, 5, 3, 'Cosmic Cyber-Cat Sticker Set',
 'I would like a set of custom die-cut vinyl stickers of a cosmic cyber-cat mascot wearing a futuristic space visor. Glossy holographic finish, bright neon magenta and cyan highlights. These will be sold at our local gaming booth and handed out as collector badges.',
 150, '3.5 x 3.5 inches die-cut', '2026-10-28 00:00:00', 3500.00, 5000.00, 'Please include a 2mm white bleed border around the cat ears for clean die-cutting.',
 NULL, NULL, '', NULL, 'PENDING REVIEW', '2026-09-01 10:30:00'),
(2, 'REQ-2026-083', 1, 5, 8, 'Sneak Peek Character Bust',
 'A bust-up illustration of my OC in your cyberpunk style. Want a neon city background.',
 1, 'A4 digital', '2026-10-05 00:00:00', 1500.00, 2500.00, '',
 NULL, NULL, '', NULL, 'PENDING REVIEW', '2026-08-30 15:00:00'),
(3, 'REQ-2026-082', 1, 5, 2, 'Convention Poster Art (11x17)',
 'Full-bleed print poster for our booth wall featuring the guardian mech.',
 1, '11x17 metallic matte', '2026-10-15 00:00:00', 4000.00, 6000.00, '',
 4800.00, '2026-11-02 00:00:00', 'Includes 2 rounds of revisions and booth-ready export.', 960.00, 'OFFER SENT', '2026-08-28 09:00:00'),
(4, 'REQ-2026-081', 1, 5, 1, 'Watercolor Pet Portrait',
 'Portrait of my shiba inu with gold filigree frame, similar to your sample.',
 1, 'A5 watercolor', '2026-09-30 00:00:00', 2000.00, 2500.00, '',
 2200.00, '2026-09-22 00:00:00', 'Physical mail via LBC included.', 440.00, 'IN PRODUCTION', '2026-08-25 11:00:00'),
(5, 'REQ-2026-080', 1, 5, 8, 'Floral Wedding Invitation Watercolor',
 'Watercolor botanical accents for wedding invitation cards.',
 1, 'A5 suite', '2026-09-20 00:00:00', 2500.00, 3000.00, '',
 2600.00, '2026-09-15 00:00:00', 'Delivered digitally with print-ready files.', 520.00, 'COMPLETED', '2026-08-20 13:00:00'),
(6, 'REQ-2026-079', 1, 5, 3, 'Twitch Emote Pack',
 'Six emotes for my stream channel, chibi style, matching the raised-slot sample.',
 1, '512x512 PNG', '2026-09-05 00:00:00', 700.00, 1000.00, '',
 850.00, '2026-09-02 00:00:00', '2 rounds of tweaks included.', 170.00, 'DECLINED', '2026-08-18 09:00:00');

INSERT INTO CommissionStatusHistory (CommissionId, FromStatus, ToStatus, ChangedBy, Note, CreatedAt) VALUES
(1, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-09-01 10:30:00'),
(2, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-08-30 15:00:00'),
(2, '', 'PENDING REVIEW', 'STAR:DOM Admin', 'Request received for review', '2026-08-31 09:00:00'),
(3, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-08-28 09:00:00'),
(3, 'SUBMITTED', 'OFFER SENT', 'STAR:DOM Admin', 'Artist accepted and sent offer', '2026-08-29 11:00:00'),
(4, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-08-25 11:00:00'),
(4, 'SUBMITTED', 'OFFER SENT', 'STAR:DOM Admin', 'Artist accepted and sent offer', '2026-08-26 10:00:00'),
(4, 'OFFER SENT', 'CUSTOMER CONFIRMED', 'Bella Santos', 'Customer confirmed the offer', '2026-08-26 16:00:00'),
(4, 'CUSTOMER CONFIRMED', 'PAID', 'Bella Santos', 'Payment recorded', '2026-08-27 09:30:00'),
(4, 'PAID', 'IN PRODUCTION', 'STAR:DOM Admin', 'Production started', '2026-08-27 14:00:00'),
(5, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-08-20 13:00:00'),
(5, 'SUBMITTED', 'OFFER SENT', 'STAR:DOM Admin', 'Artist accepted and sent offer', '2026-08-21 10:00:00'),
(5, 'OFFER SENT', 'CUSTOMER CONFIRMED', 'Bella Santos', 'Customer confirmed the offer', '2026-08-21 15:00:00'),
(5, 'CUSTOMER CONFIRMED', 'PAID', 'Bella Santos', 'Payment recorded', '2026-08-22 08:00:00'),
(5, 'PAID', 'IN PRODUCTION', 'STAR:DOM Admin', 'Production started', '2026-08-22 11:00:00'),
(5, 'IN PRODUCTION', 'FINALIZED', 'STAR:DOM Admin', 'Work finalized', '2026-09-10 09:00:00'),
(5, 'FINALIZED', 'COMPLETED', 'STAR:DOM Admin', 'Delivered to customer', '2026-09-11 09:00:00'),
(6, '', 'SUBMITTED', 'Bella Santos', 'Customer submitted request', '2026-08-18 09:00:00'),
(6, 'SUBMITTED', 'DECLINED', 'STAR:DOM Admin', 'Slot already full', '2026-08-19 10:00:00');

-- CommissionMessages is intentionally not seeded: the table is dropped with the
-- commission clarification round-trip and message thread.

-- ---------- Notifications ----------
INSERT INTO Notifications (UserId, Title, Message, NotificationType, LinkPath, IsRead, CreatedAt) VALUES
(1, 'Welcome to STAR:DOM!', 'Your CUSTOMER account is ready. Explore the marketplace!', 'SYSTEM', 'marketplace', TRUE, '2026-08-20 09:00:00'),
(1, 'Commission submitted', 'Your request REQ-2026-084 is now PENDING REVIEW.', 'COMMISSION', 'commission-hub', FALSE, '2026-09-01 10:30:00'),
(5, 'New commission', 'Bella Santos submitted a new request (REQ-2026-084).', 'COMMISSION', 'commission-pipeline', FALSE, '2026-09-01 10:30:00'),
(5, 'Event node live', 'Ayala Malls South Park is NOW OPEN. Booth is trading.', 'EVENT', 'merchant-dashboard', TRUE, '2026-10-03 09:00:00'),
(1, 'Commission update', 'Your request REQ-2026-081 is IN PRODUCTION.', 'COMMISSION', 'commission-hub', FALSE, '2026-08-27 14:00:00');

-- ============================================================
-- STAR:DOM — Seed data from ARTSHOP DATABASE (100 items)
-- Replaces previous demo catalog with official 100 Artshop items
-- Includes image wiring from STAR-DOM-Web\Assets
-- ============================================================




-- ---------- Insert 100 ARTSHOP Products ----------
INSERT INTO Products (Id, MerchantId, CategoryId, Name, Slug, Description, BasePrice, SalePrice, StockQuantity,
                      LowStockThreshold, Sku, BrandName, IsActive, IsFeatured, IsBoothExclusive, IsEventExclusive, BadgeLabel, MaterialDetails, RatingAvg, RatingCount, SoldCount) VALUES
(1, 4, 3, 'Bleeding heart', 'bleeding-heart-sticker', 'Authentic sticker by Puffu Studio. In Stock (RESTOCK 6)', 30.00, NULL, 21, 3, 'SKU-AS-0001', 'Puffu Studio', TRUE, TRUE, TRUE, TRUE, 'POPULAR', 'Die-cut sticker, Matte finish', 0.00, 0, 5),
(2, 4, 3, 'Tamaraw', 'tamaraw-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 16)', 30.00, NULL, 16, 3, 'SKU-AS-0002', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'POPULAR', 'Die-cut sticker, Matte finish', 0.00, 0, 8),
(3, 4, 3, 'Tarsier', 'tarsier-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0003', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 11),
(4, 4, 3, 'Goby', 'goby-sticker', 'Authentic sticker by Puffu Studio. Low Stock (RESTOCK 12)', 30.00, NULL, 12, 3, 'SKU-AS-0004', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 14),
(5, 4, 3, 'Kalaw', 'kalaw-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0005', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 17),
(6, 4, 3, 'Irrawady', 'irrawady-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 5)', 30.00, NULL, 5, 3, 'SKU-AS-0006', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 20),
(7, 4, 3, 'Deer', 'deer-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0007', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 23),
(8, 4, 3, 'Punch', 'punch-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0008', 'Puffu Studio', TRUE, TRUE, TRUE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 26),
(9, 4, 3, 'Hollanov', 'hollanov-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 26)', 30.00, NULL, 26, 3, 'SKU-AS-0009', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 29),
(10, 4, 3, 'Hollander', 'hollander-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0010', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 32),
(11, 4, 3, 'Rozanov', 'rozanov-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 11, 3, 'SKU-AS-0011', 'Puffu Studio', TRUE, TRUE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 35),
(12, 4, 3, 'Good Boy', 'good-boy-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 10)', 30.00, NULL, 38, 3, 'SKU-AS-0012', 'Puffu Studio', TRUE, TRUE, FALSE, TRUE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 38),
(13, 4, 3, 'Good Girl', 'good-girl-sticker', 'Authentic sticker by Puffu Studio. In Stock (9(otherdesign) 14)', 30.00, NULL, 52, 3, 'SKU-AS-0013', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 1),
(14, 4, 3, 'Gay af', 'gay-af-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 20, 3, 'SKU-AS-0014', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 4),
(15, 4, 3, 'Tobio', 'tobio-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 21, 3, 'SKU-AS-0015', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 7),
(16, 4, 3, 'Hinata', 'hinata-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 21, 3, 'SKU-AS-0016', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 10),
(17, 4, 3, 'Zigzagoon', 'zigzagoon-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 1)', 30.00, NULL, 1, 3, 'SKU-AS-0017', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 13),
(18, 4, 3, 'PHM', 'phm-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 6)', 30.00, NULL, 6, 3, 'SKU-AS-0018', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 16),
(19, 4, 3, 'Life is lifing', 'life-is-lifing-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 0)', 30.00, NULL, 20, 3, 'SKU-AS-0019', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 19),
(20, 4, 3, 'Fame whore', 'fame-whore-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 4)', 30.00, NULL, 24, 3, 'SKU-AS-0020', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 22),
(21, 4, 3, 'Jade', 'jade-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 20, 3, 'SKU-AS-0021', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 25),
(22, 4, 3, 'Santan', 'santan-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 7)', 30.00, NULL, 31, 3, 'SKU-AS-0022', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 28),
(23, 4, 3, 'hello', 'hello-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0023', 'Puffu Studio', TRUE, FALSE, FALSE, TRUE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 31),
(24, 4, 3, 'dont kys', 'dont-kys-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 0)', 30.00, NULL, 20, 3, 'SKU-AS-0024', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 34),
(25, 4, 11, 'Dinostarz', 'dinostarz-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Low Stock (Notes)', 120.00, NULL, 4, 3, 'SKU-AS-0025', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 37),
(26, 4, 11, 'fishies', 'fishies-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Restock (Notes)', 80.00, NULL, 1, 3, 'SKU-AS-0026', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 0),
(27, 4, 11, 'smiskis', 'smiskis-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Low Stock (Notes)', 80.00, NULL, 6, 3, 'SKU-AS-0027', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 3),
(28, 4, 11, 'starcraze', 'starcraze-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0028', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 6),
(29, 4, 11, 'skulz', 'skulz-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 120.00, NULL, 5, 3, 'SKU-AS-0029', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 9),
(30, 3, 2, 'Trees(4x6")', 'trees-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 100.00, NULL, 5, 3, 'SKU-AS-0030', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 12),
(31, 2, 2, 'Trees (5x7")', 'trees-5x7-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 2, 3, 'SKU-AS-0031', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 15),
(32, 3, 2, 'Heated Rivalry (4x6")', 'heated-rivalry-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 100.00, NULL, 10, 3, 'SKU-AS-0032', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 18),
(33, 2, 2, 'Maya  (5x7")', 'maya-5x7-art-print', 'Authentic art print by Mika Visuals. Low Stock (2)', 120.00, NULL, 3, 3, 'SKU-AS-0033', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Archival art print, Matte finish', 0.00, 0, 21),
(34, 3, 2, 'Bleeding Fame  (5x7")', 'bleeding-fame-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. Low Stock (Notes)', 120.00, NULL, 4, 3, 'SKU-AS-0034', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Archival art print, Matte finish', 0.00, 0, 24),
(35, 2, 2, 'Maral  (5x7")', 'maral-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (2)', 120.00, NULL, 6, 3, 'SKU-AS-0035', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 27),
(36, 3, 2, 'Rafflesia  (5x7")', 'rafflesia-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0036', 'Renzo Cruz Atelier', TRUE, FALSE, TRUE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 30),
(37, 2, 2, 'align  (5x7")', 'align-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0037', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 33),
(38, 3, 2, 'Space (5x7")', 'space-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. Low Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0038', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Archival art print, Matte finish', 0.00, 0, 36),
(39, 2, 2, 'grace  (5x7")', 'grace-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0039', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 39),
(40, 3, 2, 'Olruggio', 'olruggio-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 120.00, NULL, 1, 3, 'SKU-AS-0040', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 2),
(41, 2, 2, 'ticket (4x6")', 'ticket-4x6-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0041', 'Mika Visuals', TRUE, FALSE, FALSE, FALSE, '', 'Archival art print, Matte finish', 0.00, 0, 5),
(42, 3, 2, 'bawal umihi d2 (4x6")', 'bawal-umihi-d2-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 1, 3, 'SKU-AS-0042', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 8),
(43, 2, 2, 'A.I.  (4x6")', 'a-i-4x6-art-print', 'Authentic art print by Mika Visuals. Low Stock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0043', 'Mika Visuals', TRUE, FALSE, TRUE, FALSE, 'LOW STOCK', 'Archival art print, Matte finish', 0.00, 0, 11),
(44, 3, 2, 'Beetle', 'beetle-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 4, 3, 'SKU-AS-0044', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 14),
(45, 2, 2, 'Peacock', 'peacock-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 3, 3, 'SKU-AS-0045', 'Mika Visuals', TRUE, FALSE, FALSE, TRUE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 17),
(46, 2, 6, 'bleh', 'bleh-button-pins', 'Authentic button pins by Guild Collective. Out of Stock (Notes)', 35.00, NULL, 0, 3, 'SKU-AS-0046', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', '1.25 in. pinback button', 0.00, 0, 20),
(47, 2, 6, 'Bleed', 'bleed-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 4, 3, 'SKU-AS-0047', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 23),
(48, 2, 6, 'bangus', 'bangus-button-pins', 'Authentic button pins by Guild Collective. Low Stock (Notes)', 35.00, NULL, 5, 3, 'SKU-AS-0048', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', '1.25 in. pinback button', 0.00, 0, 26),
(49, 2, 6, 'i luv stars', 'i-luv-stars-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 3, 3, 'SKU-AS-0049', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 29),
(50, 2, 6, 'gay af', 'gay-af-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 2, 3, 'SKU-AS-0050', 'Guild Collective', TRUE, FALSE, TRUE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 32),
(51, 2, 6, 'doggo', 'doggo-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 2, 3, 'SKU-AS-0051', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 35),
(52, 2, 6, 'nerdz', 'nerdz-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 1, 3, 'SKU-AS-0052', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 38),
(53, 2, 6, 'evil eye', 'evil-eye-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 1, 3, 'SKU-AS-0053', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 1),
(54, 2, 6, 'star', 'star-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 4, 3, 'SKU-AS-0054', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 4),
(55, 2, 6, 'phm', 'phm-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 3, 3, 'SKU-AS-0055', 'Guild Collective', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 7),
(56, 3, 4, 'Goby', 'goby-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0056', 'RedFox Workshop', TRUE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 10),
(57, 3, 4, 'Pigeon', 'pigeon-keychains', 'Authentic keychains by RedFox Workshop. Restock (Notes)', 150.00, NULL, 1, 3, 'SKU-AS-0057', 'RedFox Workshop', TRUE, FALSE, TRUE, FALSE, 'RESTOCK', 'Durable acrylic keychain', 0.00, 0, 13),
(58, 3, 4, 'Bread tag', 'bread-tag-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0058', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 16),
(59, 3, 4, 'Maral', 'maral-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0059', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 19),
(60, 3, 4, 'Tamaraw', 'tamaraw-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 5, 3, 'SKU-AS-0060', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 22),
(61, 3, 4, 'Phyton', 'phyton-keychains', 'Authentic keychains by RedFox Workshop. Out of Stock (Notes)', 150.00, NULL, 0, 3, 'SKU-AS-0061', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Durable acrylic keychain', 0.00, 0, 25),
(62, 3, 4, 'santan', 'santan-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 80.00, NULL, 4, 3, 'SKU-AS-0062', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 28),
(63, 3, 4, 'jade', 'jade-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 80.00, NULL, 3, 3, 'SKU-AS-0063', 'RedFox Workshop', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 31),
(64, 3, 4, 'Webbing keychain', 'webbing-keychain-keychains', 'Authentic keychains by RedFox Workshop. In Stock (Notes)', 120.00, NULL, 21, 3, 'SKU-AS-0064', 'RedFox Workshop', TRUE, FALSE, TRUE, FALSE, '', 'Durable acrylic keychain', 0.00, 0, 34),
(65, 2, 6, 'pigeon', 'pigeon-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0065', 'Guild Collective', FALSE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 37),
(66, 2, 6, 'phyton', 'phyton-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0066', 'Guild Collective', FALSE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 0),
(67, 2, 6, 'leopard', 'leopard-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0067', 'Guild Collective', FALSE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 3),
(68, 2, 6, 'fish', 'fish-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0068', 'Guild Collective', FALSE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 6),
(69, 2, 6, 'tamaraw', 'tamaraw-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0069', 'Guild Collective', FALSE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 9),
(70, 4, 3, 'Disappoint your parents', 'disappoint-your-parents-sticker', 'Authentic sticker by Puffu Studio. Low Stock (5 11)', 30.00, NULL, 11, 3, 'SKU-AS-0070', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 12),
(71, 4, 3, 'One day at a time', 'one-day-at-a-time-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 17, 3, 'SKU-AS-0071', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 15),
(72, 4, 3, 'Know it''s for the better', 'know-it-s-for-the-better-sticker', 'Authentic sticker by Puffu Studio. Low Stock (10 13)', 30.00, NULL, 13, 3, 'SKU-AS-0072', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 18),
(73, 4, 3, 'ICU paint tube', 'icu-paint-tube-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0073', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 21),
(74, 4, 3, 'Drowning risk', 'drowning-risk-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0074', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 24),
(75, 4, 3, 'Button girl', 'button-girl-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 7)', 30.00, NULL, 7, 3, 'SKU-AS-0075', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 27),
(76, 4, 3, 'Nothing matters', 'nothing-matters-sticker', 'Authentic sticker by Puffu Studio. In Stock (4 8)', 30.00, NULL, 36, 3, 'SKU-AS-0076', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 30),
(77, 4, 3, 'Tomorrow will be better', 'tomorrow-will-be-better-sticker', 'Authentic sticker by Puffu Studio. In Stock (6 15)', 30.00, NULL, 15, 3, 'SKU-AS-0077', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 33),
(78, 4, 3, 'Always an angel', 'always-an-angel-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 15)', 30.00, NULL, 15, 3, 'SKU-AS-0078', 'Puffu Studio', TRUE, FALSE, TRUE, TRUE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 36),
(79, 4, 3, 'Girl', 'girl-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 14)', 30.00, NULL, 42, 3, 'SKU-AS-0079', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 39),
(80, 4, 3, 'Phone', 'phone-sticker', 'Authentic sticker by Puffu Studio. Out of Stock (Notes 0)', 30.00, NULL, 0, 3, 'SKU-AS-0080', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 2),
(81, 4, 3, 'World', 'world-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0081', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 5),
(82, 4, 3, 'Lapida', 'lapida-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 9)', 30.00, NULL, 9, 3, 'SKU-AS-0082', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 8),
(83, 4, 3, 'Waiting', 'waiting-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 6)', 30.00, NULL, 32, 3, 'SKU-AS-0083', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 11),
(84, 4, 3, 'Affirmations', 'affirmations-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 17)', 30.00, NULL, 17, 3, 'SKU-AS-0084', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 14),
(85, 4, 3, 'idk lol', 'idk-lol-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 20, 3, 'SKU-AS-0085', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 17),
(86, 4, 3, 'i forgor', 'i-forgor-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 26, 3, 'SKU-AS-0086', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 20),
(87, 4, 3, 'i don car', 'i-don-car-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 13)', 30.00, NULL, 37, 3, 'SKU-AS-0087', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 23),
(88, 4, 3, 'eleveneleven', 'eleveneleven-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 6)', 30.00, NULL, 6, 3, 'SKU-AS-0088', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 26),
(89, 4, 3, 'paper doll', 'paper-doll-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 11)', 30.00, NULL, 11, 3, 'SKU-AS-0089', 'Puffu Studio', TRUE, FALSE, FALSE, TRUE, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 29),
(90, 4, 3, 'we ball', 'we-ball-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 11)', 30.00, NULL, 39, 3, 'SKU-AS-0090', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 32),
(91, 4, 3, 'war', 'war-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes)', 30.00, NULL, 0, 3, 'SKU-AS-0091', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 35),
(92, 4, 3, 'honeybee', 'honeybee-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes)', 30.00, NULL, 12, 3, 'SKU-AS-0092', 'Puffu Studio', TRUE, FALSE, TRUE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 38),
(93, 4, 11, 'love', 'love-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 70.00, NULL, 8, 3, 'SKU-AS-0093', 'Puffu Studio', TRUE, FALSE, FALSE, FALSE, '', 'Die-cut sticker, Matte finish', 0.00, 0, 1),
(94, 3, 10, 'Desk buddies (Pokemon)', 'desk-buddies-pokemon-clay-deskbuddies', 'Authentic clay deskbuddies by RedFox Workshop. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0094', 'RedFox Workshop', FALSE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay desk buddy', 0.00, 0, 4),
(95, 3, 10, 'Desk buddies (Dinosaurs)', 'desk-buddies-dinosaurs-clay-deskbuddies', 'Authentic clay deskbuddies by RedFox Workshop. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0095', 'RedFox Workshop', FALSE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay desk buddy', 0.00, 0, 7),
(96, 2, 6, 'Pins (big)', 'pins-big-clay-pins', 'Authentic clay pins by Guild Collective. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0096', 'Guild Collective', FALSE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay pin', 0.00, 0, 10),
(97, 2, 6, 'Pins (small)', 'pins-small-clay-pins', 'Authentic clay pins by Guild Collective. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0097', 'Guild Collective', FALSE, FALSE, FALSE, FALSE, 'OUT OF STOCK', 'Handcrafted polymer clay pin', 0.00, 0, 13),
(98, 3, 2, 'Forwards beckon rebound', 'forwards-beckon-rebound-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 7, 3, 'SKU-AS-0098', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 16),
(99, 2, 2, 'Everything stays', 'everything-stays-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 5, 3, 'SKU-AS-0099', 'Mika Visuals', TRUE, FALSE, TRUE, FALSE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 19),
(100, 3, 2, 'Honeybee', 'honeybee-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0100', 'Renzo Cruz Atelier', TRUE, FALSE, FALSE, TRUE, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 22);

-- Nine products are seeded IsActive = FALSE because the current pricelist no
-- longer carries them. They are deactivated rather than deleted so historical
-- order lines and cart rows still resolve to a real product:
--   65-69  pigeon / phyton / leopard / fish / tamaraw  (temp tattoos, not sold)
--   94-95  Desk buddies Pokemon / Dinosaurs            (zero price)
--   96-97  Pins (big) / Pins (small)                    (zero price)
-- Category 10 "Other" therefore holds no live product and the storefront
-- auto-hides the tab; the merchant studio still lists it.

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


-- ---------- Reviews ----------
-- Reviews used to be seeded empty while Products.RatingAvg / RatingCount were
-- hand-written into each product row: two disagreeing sources of truth. These
-- rows are the only rating data now, and the UPDATE at the bottom rebuilds
-- Products.RatingAvg / RatingCount from them using the same expression
-- OrderRepository.RecalcRating runs after a live review, so the storefront, the
-- product page and the merchant report cannot drift apart after a re-seed.
--
-- 28 reviews, not 100. A hundred reviews stretched one per product made the shop
-- look like a stocked catalogue nobody had ever bought from, and it forced 72
-- products to carry an invented 5-star average with zero reviews behind it. 28
-- reviews over 28 different products reads like a real small shop, and the other
-- 72 products are left honestly at 0 / 0 — the trailing UPDATE zeroes any
-- product with no review row, exactly as RecalcRating would after the last
-- review of a product is deleted.
--
-- Six different shoppers sign them (users 1, 6-10). All 28 under one name made
-- the product pages look like a single person wrote the whole shop.
-- Ratings total 134 / 28 = 4.79, so the merchant report still prints the
-- "Average rating 4.8" the report card showed with 100 reviews.
INSERT INTO Reviews (ProductId, OrderId, UserId, Rating, Comment, IsApproved, CreatedAt) VALUES
(1,  NULL, 1,  5, 'Stuck this on my laptop two weeks ago and it still has not peeled off or lost its shine.', TRUE, NOW() - INTERVAL '6 days'),
(4,  NULL, 6,  5, 'The die-cut came out clean and the holographic layer catches the light nicely.', TRUE, NOW() - INTERVAL '9 days'),
(9,  NULL, 7,  5, 'Great sticker overall, the adhesive is just a bit stronger than I expected.', TRUE, NOW() - INTERVAL '12 days'),
(16, NULL, 8,  5, 'Print quality is crisp. Only wish it came in a slightly bigger size.', TRUE, NOW() - INTERVAL '15 days'),
(25, NULL, 9,  5, 'Bought the sheet for my shop and every single sticker is well drawn.', TRUE, NOW() - INTERVAL '19 days'),
(27, NULL, 10, 5, 'Packaging arrived flat and undamaged, which I appreciated.', TRUE, NOW() - INTERVAL '22 days'),
(30, NULL, 1,  4, 'The art is lovely but one corner was slightly bent on arrival.', TRUE, NOW() - INTERVAL '26 days'),
(33, NULL, 6,  5, 'Matted it and framed it the same night. Colors are exactly as pictured.', TRUE, NOW() - INTERVAL '29 days'),
(36, NULL, 7,  5, 'The watercolor gradients on this one are genuinely lovely.', TRUE, NOW() - INTERVAL '33 days'),
(41, NULL, 8,  3, 'Came in smaller than the listed size, so it does not fill the frame.', TRUE, NOW() - INTERVAL '37 days'),
(44, NULL, 9,  5, 'Sharp details on the beetle. Delivery took a while but it was worth it.', TRUE, NOW() - INTERVAL '41 days'),
(48, NULL, 10, 5, 'The pin clasp is strong and it has not come off my bag since.', TRUE, NOW() - INTERVAL '45 days'),
(52, NULL, 1,  4, 'Cute design, though the back is a little scratchy against fabric.', TRUE, NOW() - INTERVAL '49 days'),
(55, NULL, 6,  5, 'Second set I have ordered. Holds up well after washing.', TRUE, NOW() - INTERVAL '53 days'),
(57, NULL, 7,  5, 'The webbing strap feels sturdy and the charm hangs straight.', TRUE, NOW() - INTERVAL '57 days'),
(61, NULL, 8,  5, 'Bought one for each of my cousins. They all asked where I got it.', TRUE, NOW() - INTERVAL '61 days'),
(64, NULL, 9,  3, 'Nice webbing, but the clasp is stiff and takes two hands to open.', TRUE, NOW() - INTERVAL '64 days'),
(70, NULL, 10, 5, 'Everyone I showed it to asked where I bought it.', TRUE, NOW() - INTERVAL '68 days'),
(74, NULL, 1,  5, 'Bold artwork and it still reads well from across the room.', TRUE, NOW() - INTERVAL '71 days'),
(78, NULL, 6,  5, 'The finish on this one is noticeably nicer than the rest of the set.', TRUE, NOW() - INTERVAL '74 days'),
(82, NULL, 1,  5, 'Bought it as a gift and it landed in a proper little envelope.', TRUE, NOW() - INTERVAL '77 days'),
(86, NULL, 7,  5, 'Funny and well printed. The colors are slightly brighter in person.', TRUE, NOW() - INTERVAL '80 days'),
(90, NULL, 1,  5, 'Cheap, cute, and it already survived a week in my bag.', TRUE, NOW() - INTERVAL '83 days'),
(94, NULL, 8,  5, 'The stand actually holds it upright, which I did not expect.', TRUE, NOW() - INTERVAL '86 days'),
(95, NULL, 1,  5, 'Ordered two and they sit perfectly on my monitor.', TRUE, NOW() - INTERVAL '88 days'),
(96, NULL, 9,  5, 'Bigger than I expected in a good way. The finish is slightly glossy.', TRUE, NOW() - INTERVAL '90 days'),
(99, NULL, 1,  5, 'Lovely print, and the paper is thick enough to frame as it is.', TRUE, NOW() - INTERVAL '92 days'),
(100, NULL, 10, 5, 'The gold foil detail catches the light beautifully.', TRUE, NOW() - INTERVAL '95 days');

-- Recompute the denormalized rating columns from the rows just inserted. This is
-- the same expression OrderRepository runs after a customer submits a review, so
-- the storefront, the product page and the Event Sales report all read the same
-- numbers the Reviews page lists.
UPDATE Products p SET
    RatingAvg = COALESCE(r.avg_rating, 0),
    RatingCount = COALESCE(r.cnt, 0)
FROM (
    SELECT productid AS pid,
           AVG(rating)::numeric(3,2) AS avg_rating,
           COUNT(*) AS cnt
    FROM Reviews
    GROUP BY productid
) r
WHERE p.id = r.pid;
-- Products with no review rows are not in the subquery above. The Products
-- insert now writes 0 / 0 for every row, so nothing is stale — but this makes
-- the reset unconditional, so a product whose reviews are all gone ends up at
-- 0 / 0 exactly as RecalcRating would leave it after the last one is deleted.
-- Without it, an old re-seed could leave 72 products wearing an invented
-- 5.00 average with no review behind it.
UPDATE Products SET RatingAvg = 0, RatingCount = 0
WHERE NOT EXISTS (SELECT 1 FROM Reviews r WHERE r.ProductId = Products.Id);

-- ---------- Event inventory (Galleria South run) ----------
INSERT INTO EventInventory (EventId, ProductId, StartingStock, SoldQuantity, RemainingStock, IsEventExclusive, IsActive) VALUES
(1, 1, 25, 4, 21, FALSE, TRUE),
(1, 2, 20, 4, 16, FALSE, TRUE),
(1, 3, 15, 5, 10, FALSE, TRUE),
(1, 25, 10, 6, 4, FALSE, TRUE),
(1, 30, 10, 5, 5, FALSE, TRUE),
(1, 46, 10, 10, 0, TRUE, TRUE),
(1, 56, 12, 8, 4, FALSE, TRUE),
(1, 65, 8, 5, 3, FALSE, TRUE);

-- ---------- Event sales (in-person booth takings for the active run) ----------
INSERT INTO EventSales (EventId, OrderId, ProductId, Quantity, UnitPrice, TotalAmount, SaleType, PaymentMethod, SaleDate, Notes) VALUES
(1, NULL, 1, 2, 30.00, 60.00, 'IN_PERSON', 'GCASH', '2026-10-09 11:20:00', 'Walk-in'),
(1, NULL, 25, 1, 120.00, 120.00, 'QR',       'GOTYME', '2026-10-09 13:05:00', 'QR scan'),
(1, NULL, 30, 1, 100.00, 100.00, 'IN_PERSON', 'CASH', '2026-10-09 14:40:00', 'Art Print sale'),
(1, NULL, 56, 1, 150.00, 150.00, 'PREORDER', 'GCASH', '2026-10-09 16:10:00', 'Booth pre-order');

-- ---------- Bundles ----------
-- User specs: Stickers Bundle (4 for 100 PHP), Button pins Bundle (3 for 100 PHP)
-- Standard price 4 stickers @ 30 = 120 PHP -> 100 PHP (16.67% discount)
-- Standard price 3 button pins @ 35 = 105 PHP -> 100 PHP (4.76% discount)
--
-- Membership is the whole category, not a hand-picked shortlist: the shopper
-- reads "any 4 stickers" as any sticker in the shop, and a bundle that only
-- covered four designs just made the deal look broken. Bundle 1 therefore lists
-- every ₱30 die-cut sticker in category 3; the premium sheet designs (Dinostarz,
-- fishies, smiskis, starcraze, skulz, love) live in category 11 "Sticker
-- Sheets" precisely because a ₱120 sheet inside a "4 for ₱100" bundle would sell
-- at ₱25. Bundle 2 covers the whole Button Pins category.
--
-- The deal lives in GroupSize + BundlePrice, never in Name. An older build
-- parsed "(4 for 100)" out of the name string, so renaming a bundle silently
-- turned its pricing off. The name is now only a label.
-- GroupSize = items per group; BundlePrice = what the whole group pays.
INSERT INTO Bundles (Id, Name, Description, DiscountPercent, GroupSize, BundlePrice, IsActive) VALUES
(1, 'Stickers Bundle', 'Any 4 standard die-cut stickers for only ₱100!', 16.67, 4, 100, TRUE),
(2, 'Button Pins Bundle', 'Any 3 button pins for only ₱100!', 4.76, 3, 100, TRUE),
(3, 'Artisan Prints & Sheet Set', 'Dinostarz sticker sheet + Trees art print set with matte finish', 15.00, 2, 200, TRUE);

-- Bundle 1: every ₱30 die-cut sticker (all of category 3 "Stickers").
DELETE FROM BundleItems WHERE BundleId = 1;
INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES
(1, 1, 1), (1, 2, 1), (1, 3, 1), (1, 4, 1), (1, 5, 1), (1, 6, 1), (1, 7, 1), (1, 8, 1),
(1, 9, 1), (1, 10, 1), (1, 11, 1), (1, 12, 1), (1, 13, 1), (1, 14, 1), (1, 15, 1),
(1, 16, 1), (1, 17, 1), (1, 18, 1), (1, 19, 1), (1, 20, 1), (1, 21, 1), (1, 22, 1),
(1, 23, 1), (1, 24, 1), (1, 70, 1), (1, 71, 1), (1, 72, 1), (1, 73, 1), (1, 74, 1),
(1, 75, 1), (1, 76, 1), (1, 77, 1), (1, 78, 1), (1, 79, 1), (1, 80, 1), (1, 81, 1),
(1, 82, 1), (1, 83, 1), (1, 84, 1), (1, 85, 1), (1, 86, 1), (1, 87, 1), (1, 88, 1),
(1, 89, 1), (1, 90, 1), (1, 91, 1), (1, 92, 1);

-- Bundle 2: the ten ₱35 button pins. The ₱70 temp-tattoo designs (65-69) and the
-- two zero-price clay pin placeholders (96-97) are NOT members — a ₱70 design
-- inside a "3 for ₱100" bundle would sell at a third of its price.
DELETE FROM BundleItems WHERE BundleId = 2;
INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES
(2, 46, 1), (2, 47, 1), (2, 48, 1), (2, 49, 1), (2, 50, 1), (2, 51, 1), (2, 52, 1),
(2, 53, 1), (2, 54, 1), (2, 55, 1);

-- Bundle 3 is a fixed pairing, so its membership stays exactly as listed.
DELETE FROM BundleItems WHERE BundleId = 3;
INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES
(3, 25, 1), (3, 30, 1);

-- ---------- Payment settings (admin-managed e-wallet QR details) ----------
-- Defaults match the details that used to be hardcoded in OrderDetail's QR
-- popup. The admin can change all of this from App/Admin/PaymentSettings.aspx
-- without touching code or re-seeding.
INSERT INTO PaymentSettings (Channel, AccountName, AccountNumber, QrDisplayMode, IsEnabled) VALUES
('GCASH', 'STAR:DOM ATELIER / JAMES M.', '0917 839 2041', 'BOTH', TRUE),
('GOTYME', 'STAR:DOM ATELIER / JAMES M.', '0998 552 1928', 'BOTH', TRUE)
ON CONFLICT (Channel) DO NOTHING;

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
