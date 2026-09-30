-- ============================================================
-- STAR:DOM — Seed data (demo only)
-- Idempotent: safe to re-run after schema.sql (INSERT + ON DUP KEY).
-- Demo logins (all lowercase):
--   CUSTOMER : bella / customer123   | juan / customer123
--   ADMIN    : admin / admin123
--
-- NOTE: The product catalog is NOT seeded here anymore. The official
-- 100-item ARTSHOP inventory lives in seed_products_artshop.sql and must
-- be applied AFTER this file on a fresh install:
--   mysql -u root stardom < seed_products_artshop.sql
-- Product-linked demo data (event inventory, bundles, order history) is
-- likewise seeded there against the new Artshop SKUs.
-- ============================================================
USE stardom;

-- ---------- Roles ----------
INSERT INTO Roles (Id, Name, Description) VALUES
  (1, 'CUSTOMER', 'Marketplace shopper & commission requester'),
  (3, 'ADMIN',    'System-level manager (also the artist / merchant)')
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- ---------- Users ----------
-- customer123 / admin123 (PBKDF2-SHA256, salted)
INSERT INTO Users (Id, Email, Username, FullName, Phone, PasswordHash, RoleId, Status, EmailVerified,
                   CommissionSlotCapacity, CommissionStartingPrice, CommissionTurnaround, CommissionFormats, CommissionTagline) VALUES
(1, 'bella@example.com',    'bella',  'Bella Santos',    '09171234567', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', 1, 0, 0, '', '', ''),
(5, 'admin@stardom.ph',     'admin',  'STAR:DOM Admin',  '09281234567', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 3, 'ACTIVE', 1, 5, 1500.00, '3-5 business days', 'High-Res PNG + A4 Print', 'Anime & Cyberpunk Stylist'),
(6, 'juan@example.com',     'juan',   'Juan Dela Cruz',  '09175678901', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', 1, 0, 0, '', '', '')
ON DUPLICATE KEY UPDATE Email = VALUES(Email);

-- ---------- Categories ----------
INSERT INTO Categories (Id, Name, Slug, Description, DisplayOrder, IsActive) VALUES
(1,  'Original Art',    'original-art',    'One-of-a-kind originals and A3/A2 gallery pieces', 1, 1),
(2,  'Fine Art Prints', 'fine-art-prints', 'Giclee & archival pigment prints on cotton card', 2, 1),
(3,  'Stickers',        'stickers',        'Die-cut, holographic & waterproof sticker packs', 3, 1),
(4,  'Keychains',       'keychains',       'Acrylic & metal keychains', 4, 1),
(5,  'Acrylic Charms',  'acrylic-charms',  'Glitter epoxy charms, stands, and acrylic accessories', 5, 1),
(6,  'Merchandise',     'merchandise',     'Enamel pins, totes, apparel & convention merch', 6, 1),
(7,  'Bundles',         'bundles',         'Curated multi-item bazaar bundles', 7, 1),
(8,  'Custom Artwork',  'custom-artwork',  'Bespoke digital & traditional commissions', 8, 1),
(9,  'Commission Slots','commission-slots','Open atelier commission slots', 9, 1),
(10, 'Other',           'other',           'Miscellaneous artisan goods', 10, 1)
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- ---------- Promotions ----------
INSERT INTO Promotions (Name, Description, DiscountType, DiscountValue, StartsAt, EndsAt, IsActive) VALUES
('September Payday Sale', '10% off all fine art prints', 'PERCENT', 10, '2026-09-20 00:00:00', '2026-09-30 23:59:59', 1),
('Gala Comicon Week', '15% off booth-exclusive merch', 'PERCENT', 15, '2026-10-01 00:00:00', '2026-10-08 23:59:59', 1),
('August Clearance', '₱50 off select stickers', 'FIXED', 50, '2026-08-01 00:00:00', '2026-08-31 23:59:59', 0)
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- ---------- Store locations ----------
INSERT INTO StoreLocations (Id, Name, Venue, Address, City, Region, IsActive) VALUES
(1, 'STAR:DOM @ Robinson''s Galleria South', 'Ground Floor Activity Center', 'KM 31 National Highway, Near Main Atrium', 'San Pedro', 'Laguna / South Luzon', 1),
(2, 'STAR:DOM @ SM City Santa Rosa',         'Mall Expansion Wing, 2nd Floor', 'Santa Rosa-Tagaytay Road', 'Santa Rosa', 'Laguna / South Luzon', 1),
(3, 'STAR:DOM @ Festival Mall Alabang',      'Water Garden Hallway, Upper Ground', 'Filinvest City', 'Muntinlupa', 'Metro Manila South', 1),
(4, 'STAR:DOM @ Ayala Malls South Park',     'Level 3 Main Cinema Foyer', 'Alabang-Zapote Road', 'Muntinlupa', 'Metro Manila South', 1),
(5, 'STAR:DOM @ Robinsons Place Manila',     'Midtown Atrium Stage', 'Adriatico Street, Ermita', 'Manila', 'Metro Manila Central', 1)
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- ---------- Pop-up events ----------
INSERT INTO PopUpEvents (Id, LocationId, Name, Description, StartDate, EndDate, OpenTime, CloseTime, BoothNumber,
                         VenueDetail, Status, FeaturedGuest, IsCurrent, LineupText) VALUES
(1, 1, 'STAR:DOM @ Robinson''s Galleria South', 'Touch prints, inspect merchandise, watch live sketching, and pay instantly via local Philippine payment rails. Convention sticker sheets and on-site custom sketch slots available.',
 '2026-09-04 10:00:00', '2026-09-07 21:00:00', '10:00 AM', '9:00 PM', 'Stall A-12', 'Ground Atrium Activity Center', 'NOW OPEN', 'Mika S. & Guild', 1, '15 Guest Creators'),
(2, 2, 'STAR:DOM @ SM City Santa Rosa', 'South Luzon Artisan Weekend Expo. Over 30 creators joining our combined pavilion with print swaps, sticker rallies, and live tablet painting demo sessions.',
 '2026-09-12 10:00:00', '2026-09-14 21:00:00', '10:00 AM', '9:00 PM', 'Booth D-04', 'Ground Atrium (Booth D-04)', 'UPCOMING', '@Kira_Illustration & Guild', 0, '12 Guest Creators'),
(3, 3, 'STAR:DOM @ Festival Mall Alabang', 'Metro Manila south bazaar with watercolor demos and gacha sticker dispensers.',
 '2026-09-19 10:00:00', '2026-09-21 21:00:00', '10:00 AM', '9:00 PM', 'Island F', 'Carousel Court (Island F)', 'UPCOMING', 'Renzo Cruz', 0, '18 Guest Creators'),
(4, 4, 'STAR:DOM @ Ayala Malls South Park', 'Indie comic & print exhibition, live ink sketches open at 11:00 AM daily.',
 '2026-10-02 10:00:00', '2026-10-04 21:00:00', '10:00 AM', '9:00 PM', 'Central Pod', 'Level 2 Activity Area', 'UPCOMING', 'Puffu Studio', 0, '10 Guest Creators'),
(5, 5, 'STAR:DOM @ Robinsons Place Manila', 'Midtown art fair with enamel pin rallies and convention merch.',
 '2026-10-16 10:00:00', '2026-10-18 21:00:00', '10:00 AM', '9:00 PM', 'Midtown Wing', 'Midtown Atrium Stage', 'UPCOMING', 'Guild Collective', 0, '25 Guest Creators'),
(6, 1, 'SM Southmall Artisan Fair', 'Historical tour run — August artisan fair.',
 '2026-08-21 10:00:00', '2026-08-24 21:00:00', '10:00 AM', '9:00 PM', 'Stall K-02', 'Activity Center', 'ENDED', '', 0, ''),
(7, 2, 'U.P. Town Center Art Bazaar', 'Historical tour run — university town bazaar.',
 '2026-08-08 10:00:00', '2026-08-10 21:00:00', '10:00 AM', '8:00 PM', 'Block 3', 'Open Plaza', 'ENDED', '', 0, '')
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

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
 1, 'A4 digital', '2026-10-05 00:00:00', 1500.00, 2500.00, NULL,
 NULL, NULL, '', NULL, 'CLARIFICATION REQUESTED', '2026-08-30 15:00:00'),
(3, 'REQ-2026-082', 1, 5, 2, 'Convention Poster Art (11x17)',
 'Full-bleed print poster for our booth wall featuring the guardian mech.',
 1, '11x17 metallic matte', '2026-10-15 00:00:00', 4000.00, 6000.00, NULL,
 4800.00, '2026-11-02 00:00:00', 'Includes 2 rounds of revisions and booth-ready export.', 960.00, 'OFFER SENT', '2026-08-28 09:00:00'),
(4, 'REQ-2026-081', 6, 5, 1, 'Watercolor Pet Portrait',
 'Portrait of my shiba inu with gold filigree frame, similar to your sample.',
 1, 'A5 watercolor', '2026-09-30 00:00:00', 2000.00, 2500.00, NULL,
 2200.00, '2026-09-22 00:00:00', 'Physical mail via LBC included.', 440.00, 'IN PRODUCTION', '2026-08-25 11:00:00'),
(5, 'REQ-2026-080', 1, 5, 8, 'Floral Wedding Invitation Watercolor',
 'Watercolor botanical accents for wedding invitation cards.',
 1, 'A5 suite', '2026-09-20 00:00:00', 2500.00, 3000.00, NULL,
 2600.00, '2026-09-15 00:00:00', 'Delivered digitally with print-ready files.', 520.00, 'COMPLETED', '2026-08-20 13:00:00'),
(6, 'REQ-2026-079', 6, 5, 3, 'Twitch Emote Pack',
 'Six emotes for my stream channel, chibi style, matching the raised-slot sample.',
 1, '512x512 PNG', '2026-09-05 00:00:00', 700.00, 1000.00, NULL,
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
(2, 5, 'Hi Juan! Could you share a reference of your OC, or a color palette? Also, do you want the bust at waist level or shoulders-up?', 0, '2026-08-31 09:02:00'),
(3, 1, 'Hi! Looking forward to the poster! Will the file include a print-ready 300dpi version?', 0, '2026-08-31 17:30:00');

-- ---------- Notifications ----------
INSERT INTO Notifications (UserId, Title, Message, NotificationType, LinkPath, IsRead, CreatedAt) VALUES
(1, 'Welcome to STAR:DOM!', 'Your CUSTOMER account is ready. Explore the marketplace!', 'SYSTEM', 'marketplace', 1, '2026-08-20 09:00:00'),
(1, 'Commission submitted', 'Your request REQ-2026-084 is now PENDING REVIEW.', 'COMMISSION', 'commission-hub', 0, '2026-09-01 10:30:00'),
(5, 'New commission', 'Bella Santos submitted a new request (REQ-2026-084).', 'COMMISSION', 'commission-pipeline', 0, '2026-09-01 10:30:00'),
(5, 'Event node live', 'Galleria South is NOW OPEN. POS terminal active.', 'EVENT', 'merchant-dashboard', 1, '2026-09-04 09:00:00'),
(6, 'Commission update', 'Your request REQ-2026-081 is IN PRODUCTION.', 'COMMISSION', 'commission-hub', 0, '2026-08-27 14:00:00');