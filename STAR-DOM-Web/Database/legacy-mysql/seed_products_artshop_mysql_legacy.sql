-- ============================================================
-- STAR:DOM — Seed data from ARTSHOP DATABASE (100 items)
-- Replaces previous demo catalog with official 100 Artshop items
-- Includes image wiring from STAR-DOM-Web\Assets
-- ============================================================
USE stardom;

-- Disable foreign key checks for clean table reset
SET FOREIGN_KEY_CHECKS = 0;
TRUNCATE TABLE EventSales;
TRUNCATE TABLE EventInventory;
TRUNCATE TABLE OrderItems;
TRUNCATE TABLE CartItems;
TRUNCATE TABLE WishlistItems;
TRUNCATE TABLE Reviews;
TRUNCATE TABLE BundleItems;
TRUNCATE TABLE Bundles;
TRUNCATE TABLE ProductImages;
TRUNCATE TABLE ProductVariants;
TRUNCATE TABLE Products;
SET FOREIGN_KEY_CHECKS = 1;

-- ---------- Insert 100 ARTSHOP Products ----------
INSERT INTO Products (Id, MerchantId, CategoryId, Name, Slug, Description, BasePrice, SalePrice, StockQuantity,
                      LowStockThreshold, Sku, BrandName, IsActive, IsFeatured, IsBoothExclusive, IsEventExclusive, BadgeLabel, MaterialDetails, RatingAvg, RatingCount, SoldCount) VALUES
(1, 4, 3, 'Bleeding heart', 'bleeding-heart-sticker', 'Authentic sticker by Puffu Studio. In Stock (RESTOCK 6)', 30.00, NULL, 21, 3, 'SKU-AS-0001', 'Puffu Studio', 1, 1, 1, 1, 'POPULAR', 'Die-cut sticker, Matte finish', 5.00, 1, 5),
(2, 4, 3, 'Tamaraw', 'tamaraw-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 16)', 30.00, NULL, 16, 3, 'SKU-AS-0002', 'Puffu Studio', 1, 1, 0, 0, 'POPULAR', 'Die-cut sticker, Matte finish', 0.00, 0, 8),
(3, 4, 3, 'Tarsier', 'tarsier-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0003', 'Puffu Studio', 1, 1, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 11),
(4, 4, 3, 'Goby', 'goby-sticker', 'Authentic sticker by Puffu Studio. Low Stock (RESTOCK 12)', 30.00, NULL, 12, 3, 'SKU-AS-0004', 'Puffu Studio', 1, 1, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 14),
(5, 4, 3, 'Kalaw', 'kalaw-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0005', 'Puffu Studio', 1, 1, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 17),
(6, 4, 3, 'Irrawady', 'irrawady-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 5)', 30.00, NULL, 5, 3, 'SKU-AS-0006', 'Puffu Studio', 1, 1, 0, 0, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 20),
(7, 4, 3, 'Deer', 'deer-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0007', 'Puffu Studio', 1, 1, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 1, 23),
(8, 4, 3, 'Punch', 'punch-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0008', 'Puffu Studio', 1, 1, 1, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 26),
(9, 4, 3, 'Hollanov', 'hollanov-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 26)', 30.00, NULL, 26, 3, 'SKU-AS-0009', 'Puffu Studio', 1, 1, 0, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 29),
(10, 4, 3, 'Hollander', 'hollander-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0010', 'Puffu Studio', 1, 1, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 32),
(11, 4, 3, 'Rozanov', 'rozanov-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 11, 3, 'SKU-AS-0011', 'Puffu Studio', 1, 1, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 35),
(12, 4, 3, 'Good Boy', 'good-boy-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 10)', 30.00, NULL, 38, 3, 'SKU-AS-0012', 'Puffu Studio', 1, 1, 0, 1, '', 'Die-cut sticker, Matte finish', 0.00, 0, 38),
(13, 4, 3, 'Good Girl', 'good-girl-sticker', 'Authentic sticker by Puffu Studio. In Stock (9(otherdesign) 14)', 30.00, NULL, 52, 3, 'SKU-AS-0013', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 5.00, 1, 1),
(14, 4, 3, 'Gay af', 'gay-af-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 20, 3, 'SKU-AS-0014', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 0.00, 0, 4),
(15, 4, 3, 'Tobio', 'tobio-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 21, 3, 'SKU-AS-0015', 'Puffu Studio', 1, 0, 1, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 7),
(16, 4, 3, 'Hinata', 'hinata-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 21, 3, 'SKU-AS-0016', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 5.00, 0, 10),
(17, 4, 3, 'Zigzagoon', 'zigzagoon-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 1)', 30.00, NULL, 1, 3, 'SKU-AS-0017', 'Puffu Studio', 1, 0, 0, 0, 'RESTOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 13),
(18, 4, 3, 'PHM', 'phm-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 6)', 30.00, NULL, 6, 3, 'SKU-AS-0018', 'Puffu Studio', 1, 0, 0, 0, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 16),
(19, 4, 3, 'Life is lifing', 'life-is-lifing-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 0)', 30.00, NULL, 20, 3, 'SKU-AS-0019', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 5.00, 1, 19),
(20, 4, 3, 'Fame whore', 'fame-whore-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 4)', 30.00, NULL, 24, 3, 'SKU-AS-0020', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 0.00, 0, 22),
(21, 4, 3, 'Jade', 'jade-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 5)', 30.00, NULL, 20, 3, 'SKU-AS-0021', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 25),
(22, 4, 3, 'Santan', 'santan-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 7)', 30.00, NULL, 31, 3, 'SKU-AS-0022', 'Puffu Studio', 1, 0, 1, 0, '', 'Die-cut sticker, Matte finish', 5.00, 0, 28),
(23, 4, 3, 'hello', 'hello-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0023', 'Puffu Studio', 1, 0, 0, 1, '', 'Die-cut sticker, Matte finish', 4.50, 1, 31),
(24, 4, 3, 'dont kys', 'dont-kys-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 0)', 30.00, NULL, 20, 3, 'SKU-AS-0024', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 0.00, 0, 34),
(25, 4, 3, 'Dinostarz', 'dinostarz-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Low Stock (Notes)', 120.00, NULL, 4, 3, 'SKU-AS-0025', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 1, 37),
(26, 4, 3, 'fishies', 'fishies-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Restock (Notes)', 80.00, NULL, 1, 3, 'SKU-AS-0026', 'Puffu Studio', 1, 0, 0, 0, 'RESTOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 0),
(27, 4, 3, 'smiskis', 'smiskis-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. Low Stock (Notes)', 80.00, NULL, 6, 3, 'SKU-AS-0027', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 3),
(28, 4, 3, 'starcraze', 'starcraze-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0028', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 5.00, 0, 6),
(29, 4, 3, 'skulz', 'skulz-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 120.00, NULL, 5, 3, 'SKU-AS-0029', 'Puffu Studio', 1, 0, 1, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 9),
(30, 3, 2, 'Trees(4x6")', 'trees-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 100.00, NULL, 5, 3, 'SKU-AS-0030', 'Renzo Cruz Atelier', 1, 0, 0, 0, '', 'Archival art print, Matte finish', 0.00, 0, 12),
(31, 2, 2, 'Trees (5x7")', 'trees-5x7-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 2, 3, 'SKU-AS-0031', 'Mika Visuals', 1, 0, 0, 0, 'RESTOCK', 'Archival art print, Matte finish', 5.00, 1, 15),
(32, 3, 2, 'Heated Rivalry (4x6")', 'heated-rivalry-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 100.00, NULL, 10, 3, 'SKU-AS-0032', 'Renzo Cruz Atelier', 1, 0, 0, 0, '', 'Archival art print, Matte finish', 0.00, 0, 18),
(33, 2, 2, 'Maya  (5x7")', 'maya-5x7-art-print', 'Authentic art print by Mika Visuals. Low Stock (2)', 120.00, NULL, 3, 3, 'SKU-AS-0033', 'Mika Visuals', 1, 0, 0, 0, 'LOW STOCK', 'Archival art print, Matte finish', 4.50, 1, 21),
(34, 3, 2, 'Bleeding Fame  (5x7")', 'bleeding-fame-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. Low Stock (Notes)', 120.00, NULL, 4, 3, 'SKU-AS-0034', 'Renzo Cruz Atelier', 1, 0, 0, 1, 'LOW STOCK', 'Archival art print, Matte finish', 5.00, 0, 24),
(35, 2, 2, 'Maral  (5x7")', 'maral-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (2)', 120.00, NULL, 6, 3, 'SKU-AS-0035', 'Mika Visuals', 1, 0, 0, 0, '', 'Archival art print, Matte finish', 4.50, 1, 27),
(36, 3, 2, 'Rafflesia  (5x7")', 'rafflesia-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0036', 'Renzo Cruz Atelier', 1, 0, 1, 0, '', 'Archival art print, Matte finish', 0.00, 0, 30),
(37, 2, 2, 'align  (5x7")', 'align-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0037', 'Mika Visuals', 1, 0, 0, 0, '', 'Archival art print, Matte finish', 5.00, 1, 33),
(38, 3, 2, 'Space (5x7")', 'space-5x7-art-print', 'Authentic art print by Renzo Cruz Atelier. Low Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0038', 'Renzo Cruz Atelier', 1, 0, 0, 0, 'LOW STOCK', 'Archival art print, Matte finish', 0.00, 0, 36),
(39, 2, 2, 'grace  (5x7")', 'grace-5x7-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 120.00, NULL, 6, 3, 'SKU-AS-0039', 'Mika Visuals', 1, 0, 0, 0, '', 'Archival art print, Matte finish', 4.50, 1, 39),
(40, 3, 2, 'Olruggio', 'olruggio-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 120.00, NULL, 1, 3, 'SKU-AS-0040', 'Renzo Cruz Atelier', 1, 0, 0, 0, 'RESTOCK', 'Archival art print, Matte finish', 5.00, 0, 2),
(41, 2, 2, 'ticket (4x6")', 'ticket-4x6-art-print', 'Authentic art print by Mika Visuals. In Stock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0041', 'Mika Visuals', 1, 0, 0, 0, '', 'Archival art print, Matte finish', 4.50, 1, 5),
(42, 3, 2, 'bawal umihi d2 (4x6")', 'bawal-umihi-d2-4x6-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 1, 3, 'SKU-AS-0042', 'Renzo Cruz Atelier', 1, 0, 0, 0, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 8),
(43, 2, 2, 'A.I.  (4x6")', 'a-i-4x6-art-print', 'Authentic art print by Mika Visuals. Low Stock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0043', 'Mika Visuals', 1, 0, 1, 0, 'LOW STOCK', 'Archival art print, Matte finish', 5.00, 1, 11),
(44, 3, 2, 'Beetle', 'beetle-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 4, 3, 'SKU-AS-0044', 'Renzo Cruz Atelier', 1, 0, 0, 0, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 14),
(45, 2, 2, 'Peacock', 'peacock-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 3, 3, 'SKU-AS-0045', 'Mika Visuals', 1, 0, 0, 1, 'RESTOCK', 'Archival art print, Matte finish', 4.50, 1, 17),
(46, 2, 6, 'bleh', 'bleh-button-pins', 'Authentic button pins by Guild Collective. Out of Stock (Notes)', 35.00, NULL, 0, 3, 'SKU-AS-0046', 'Guild Collective', 1, 0, 0, 0, 'OUT OF STOCK', '1.25 in. pinback button', 5.00, 0, 20),
(47, 2, 6, 'Bleed', 'bleed-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 4, 3, 'SKU-AS-0047', 'Guild Collective', 1, 0, 0, 0, 'RESTOCK', '1.25 in. pinback button', 4.50, 1, 23),
(48, 2, 6, 'bangus', 'bangus-button-pins', 'Authentic button pins by Guild Collective. Low Stock (Notes)', 35.00, NULL, 5, 3, 'SKU-AS-0048', 'Guild Collective', 1, 0, 0, 0, 'LOW STOCK', '1.25 in. pinback button', 0.00, 0, 26),
(49, 2, 6, 'i luv stars', 'i-luv-stars-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 3, 3, 'SKU-AS-0049', 'Guild Collective', 1, 0, 0, 0, 'RESTOCK', '1.25 in. pinback button', 5.00, 1, 29),
(50, 2, 6, 'gay af', 'gay-af-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 2, 3, 'SKU-AS-0050', 'Guild Collective', 1, 0, 1, 0, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 32),
(51, 2, 6, 'doggo', 'doggo-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 2, 3, 'SKU-AS-0051', 'Guild Collective', 1, 0, 0, 0, 'RESTOCK', '1.25 in. pinback button', 4.50, 1, 35),
(52, 2, 6, 'nerdz', 'nerdz-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 1, 3, 'SKU-AS-0052', 'Guild Collective', 1, 0, 0, 0, 'RESTOCK', '1.25 in. pinback button', 5.00, 0, 38),
(53, 2, 6, 'evil eye', 'evil-eye-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 1, 3, 'SKU-AS-0053', 'Guild Collective', 1, 0, 0, 0, 'RESTOCK', '1.25 in. pinback button', 4.50, 1, 1),
(54, 2, 6, 'star', 'star-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 4, 3, 'SKU-AS-0054', 'Guild Collective', 1, 0, 0, 0, 'RESTOCK', '1.25 in. pinback button', 0.00, 0, 4),
(55, 2, 6, 'phm', 'phm-button-pins', 'Authentic button pins by Guild Collective. Restock (Notes)', 35.00, NULL, 3, 3, 'SKU-AS-0055', 'Guild Collective', 1, 0, 0, 0, 'RESTOCK', '1.25 in. pinback button', 5.00, 1, 7),
(56, 3, 4, 'Goby', 'goby-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0056', 'RedFox Workshop', 1, 0, 0, 1, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 10),
(57, 3, 4, 'Pigeon', 'pigeon-keychains', 'Authentic keychains by RedFox Workshop. Restock (Notes)', 150.00, NULL, 1, 3, 'SKU-AS-0057', 'RedFox Workshop', 1, 0, 1, 0, 'RESTOCK', 'Durable acrylic keychain', 4.50, 1, 13),
(58, 3, 4, 'Bread tag', 'bread-tag-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0058', 'RedFox Workshop', 1, 0, 0, 0, 'LOW STOCK', 'Durable acrylic keychain', 5.00, 0, 16),
(59, 3, 4, 'Maral', 'maral-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 4, 3, 'SKU-AS-0059', 'RedFox Workshop', 1, 0, 0, 0, 'LOW STOCK', 'Durable acrylic keychain', 4.50, 1, 19),
(60, 3, 4, 'Tamaraw', 'tamaraw-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 150.00, NULL, 5, 3, 'SKU-AS-0060', 'RedFox Workshop', 1, 0, 0, 0, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 22),
(61, 3, 4, 'Phyton', 'phyton-keychains', 'Authentic keychains by RedFox Workshop. Out of Stock (Notes)', 150.00, NULL, 0, 3, 'SKU-AS-0061', 'RedFox Workshop', 1, 0, 0, 0, 'OUT OF STOCK', 'Durable acrylic keychain', 5.00, 1, 25),
(62, 3, 4, 'santan', 'santan-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 80.00, NULL, 4, 3, 'SKU-AS-0062', 'RedFox Workshop', 1, 0, 0, 0, 'LOW STOCK', 'Durable acrylic keychain', 0.00, 0, 28),
(63, 3, 4, 'jade', 'jade-keychains', 'Authentic keychains by RedFox Workshop. Low Stock (Notes)', 80.00, NULL, 3, 3, 'SKU-AS-0063', 'RedFox Workshop', 1, 0, 0, 0, 'LOW STOCK', 'Durable acrylic keychain', 4.50, 1, 31),
(64, 3, 4, 'Webbing keychain', 'webbing-keychain-keychains', 'Authentic keychains by RedFox Workshop. In Stock (Notes)', 120.00, NULL, 21, 3, 'SKU-AS-0064', 'RedFox Workshop', 1, 0, 1, 0, '', 'Durable acrylic keychain', 5.00, 0, 34),
(65, 2, 6, 'pigeon', 'pigeon-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0065', 'Guild Collective', 1, 0, 0, 0, 'LOW STOCK', 'Skin-safe temporary tattoo', 4.50, 1, 37),
(66, 2, 6, 'phyton', 'phyton-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0066', 'Guild Collective', 1, 0, 0, 0, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 0),
(67, 2, 6, 'leopard', 'leopard-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0067', 'Guild Collective', 1, 0, 0, 1, 'LOW STOCK', 'Skin-safe temporary tattoo', 5.00, 1, 3),
(68, 2, 6, 'fish', 'fish-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0068', 'Guild Collective', 1, 0, 0, 0, 'LOW STOCK', 'Skin-safe temporary tattoo', 0.00, 0, 6),
(69, 2, 6, 'tamaraw', 'tamaraw-temp-tattoos', 'Authentic temp tattoos by Guild Collective. Low Stock (Notes)', 70.00, NULL, 3, 3, 'SKU-AS-0069', 'Guild Collective', 1, 0, 0, 0, 'LOW STOCK', 'Skin-safe temporary tattoo', 4.50, 1, 9),
(70, 4, 3, 'Disappoint your parents', 'disappoint-your-parents-sticker', 'Authentic sticker by Puffu Studio. Low Stock (5 11)', 30.00, NULL, 11, 3, 'SKU-AS-0070', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 12),
(71, 4, 3, 'One day at a time', 'one-day-at-a-time-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 17, 3, 'SKU-AS-0071', 'Puffu Studio', 1, 0, 1, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 15),
(72, 4, 3, 'Know it''s for the better', 'know-it-s-for-the-better-sticker', 'Authentic sticker by Puffu Studio. Low Stock (10 13)', 30.00, NULL, 13, 3, 'SKU-AS-0072', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 18),
(73, 4, 3, 'ICU paint tube', 'icu-paint-tube-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 10)', 30.00, NULL, 10, 3, 'SKU-AS-0073', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 1, 21),
(74, 4, 3, 'Drowning risk', 'drowning-risk-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0074', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 24),
(75, 4, 3, 'Button girl', 'button-girl-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 7)', 30.00, NULL, 7, 3, 'SKU-AS-0075', 'Puffu Studio', 1, 0, 0, 0, 'RESTOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 27),
(76, 4, 3, 'Nothing matters', 'nothing-matters-sticker', 'Authentic sticker by Puffu Studio. In Stock (4 8)', 30.00, NULL, 36, 3, 'SKU-AS-0076', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 5.00, 0, 30),
(77, 4, 3, 'Tomorrow will be better', 'tomorrow-will-be-better-sticker', 'Authentic sticker by Puffu Studio. In Stock (6 15)', 30.00, NULL, 15, 3, 'SKU-AS-0077', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 33),
(78, 4, 3, 'Always an angel', 'always-an-angel-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 15)', 30.00, NULL, 15, 3, 'SKU-AS-0078', 'Puffu Studio', 1, 0, 1, 1, '', 'Die-cut sticker, Matte finish', 0.00, 0, 36),
(79, 4, 3, 'Girl', 'girl-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 14)', 30.00, NULL, 42, 3, 'SKU-AS-0079', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 5.00, 1, 39),
(80, 4, 3, 'Phone', 'phone-sticker', 'Authentic sticker by Puffu Studio. Out of Stock (Notes 0)', 30.00, NULL, 0, 3, 'SKU-AS-0080', 'Puffu Studio', 1, 0, 0, 0, 'OUT OF STOCK', 'Die-cut sticker, Matte finish', 0.00, 0, 2),
(81, 4, 3, 'World', 'world-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 12)', 30.00, NULL, 12, 3, 'SKU-AS-0081', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 5),
(82, 4, 3, 'Lapida', 'lapida-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 9)', 30.00, NULL, 9, 3, 'SKU-AS-0082', 'Puffu Studio', 1, 0, 0, 0, 'LOW STOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 8),
(83, 4, 3, 'Waiting', 'waiting-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 6)', 30.00, NULL, 32, 3, 'SKU-AS-0083', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 11),
(84, 4, 3, 'Affirmations', 'affirmations-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 17)', 30.00, NULL, 17, 3, 'SKU-AS-0084', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 0.00, 0, 14),
(85, 4, 3, 'idk lol', 'idk-lol-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 20, 3, 'SKU-AS-0085', 'Puffu Studio', 1, 0, 1, 0, '', 'Die-cut sticker, Matte finish', 5.00, 1, 17),
(86, 4, 3, 'i forgor', 'i-forgor-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 2)', 30.00, NULL, 26, 3, 'SKU-AS-0086', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 0.00, 0, 20),
(87, 4, 3, 'i don car', 'i-don-car-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 13)', 30.00, NULL, 37, 3, 'SKU-AS-0087', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 23),
(88, 4, 3, 'eleveneleven', 'eleveneleven-sticker', 'Authentic sticker by Puffu Studio. Restock (Notes 6)', 30.00, NULL, 6, 3, 'SKU-AS-0088', 'Puffu Studio', 1, 0, 0, 0, 'RESTOCK', 'Die-cut sticker, Matte finish', 5.00, 0, 26),
(89, 4, 3, 'paper doll', 'paper-doll-sticker', 'Authentic sticker by Puffu Studio. Low Stock (Notes 11)', 30.00, NULL, 11, 3, 'SKU-AS-0089', 'Puffu Studio', 1, 0, 0, 1, 'LOW STOCK', 'Die-cut sticker, Matte finish', 4.50, 1, 29),
(90, 4, 3, 'we ball', 'we-ball-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes 11)', 30.00, NULL, 39, 3, 'SKU-AS-0090', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 0.00, 0, 32),
(91, 4, 3, 'war', 'war-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes)', 30.00, NULL, 0, 3, 'SKU-AS-0091', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 5.00, 1, 35),
(92, 4, 3, 'honeybee', 'honeybee-sticker', 'Authentic sticker by Puffu Studio. In Stock (Notes)', 30.00, NULL, 12, 3, 'SKU-AS-0092', 'Puffu Studio', 1, 0, 1, 0, '', 'Die-cut sticker, Matte finish', 0.00, 0, 38),
(93, 4, 3, 'love', 'love-sticker-sheets', 'Authentic sticker sheets by Puffu Studio. In Stock (Notes)', 70.00, NULL, 8, 3, 'SKU-AS-0093', 'Puffu Studio', 1, 0, 0, 0, '', 'Die-cut sticker, Matte finish', 4.50, 1, 1),
(94, 3, 10, 'Desk buddies (Pokemon)', 'desk-buddies-pokemon-clay-deskbuddies', 'Authentic clay deskbuddies by RedFox Workshop. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0094', 'RedFox Workshop', 1, 0, 0, 0, 'OUT OF STOCK', 'Handcrafted polymer clay desk buddy', 5.00, 0, 4),
(95, 3, 10, 'Desk buddies (Dinosaurs)', 'desk-buddies-dinosaurs-clay-deskbuddies', 'Authentic clay deskbuddies by RedFox Workshop. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0095', 'RedFox Workshop', 1, 0, 0, 0, 'OUT OF STOCK', 'Handcrafted polymer clay desk buddy', 4.50, 1, 7),
(96, 2, 6, 'Pins (big)', 'pins-big-clay-pins', 'Authentic clay pins by Guild Collective. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0096', 'Guild Collective', 1, 0, 0, 0, 'OUT OF STOCK', 'Handcrafted polymer clay pin', 0.00, 0, 10),
(97, 2, 6, 'Pins (small)', 'pins-small-clay-pins', 'Authentic clay pins by Guild Collective. Out of Stock (Notes)', 0.00, NULL, 0, 3, 'SKU-AS-0097', 'Guild Collective', 1, 0, 0, 0, 'OUT OF STOCK', 'Handcrafted polymer clay pin', 5.00, 1, 13),
(98, 3, 2, 'Forwards beckon rebound', 'forwards-beckon-rebound-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 7, 3, 'SKU-AS-0098', 'Renzo Cruz Atelier', 1, 0, 0, 0, 'RESTOCK', 'Archival art print, Matte finish', 0.00, 0, 16),
(99, 2, 2, 'Everything stays', 'everything-stays-art-print', 'Authentic art print by Mika Visuals. Restock (Notes)', 100.00, NULL, 5, 3, 'SKU-AS-0099', 'Mika Visuals', 1, 0, 1, 0, 'RESTOCK', 'Archival art print, Matte finish', 4.50, 1, 19),
(100, 3, 2, 'Honeybee', 'honeybee-art-print', 'Authentic art print by Renzo Cruz Atelier. Restock (Notes)', 100.00, NULL, 6, 3, 'SKU-AS-0100', 'Renzo Cruz Atelier', 1, 0, 0, 1, 'RESTOCK', 'Archival art print, Matte finish', 5.00, 0, 22);

-- ---------- Product Images (From Assets) ----------
INSERT INTO ProductImages (ProductId, ImageFile, IsPrimary, SortOrder) VALUES
(1, '/Assets/Stickers/Bleeding heart.webp', 1, 1),
(2, '/Assets/Stickers/Tamaraw.webp', 1, 1),
(3, '/Assets/Stickers/Tarsier.webp', 1, 1),
(4, '/Assets/Stickers/Goby.webp', 1, 1),
(5, '/Assets/Stickers/Kalaw.webp', 1, 1),
(6, '/Assets/Stickers/Irrawady.webp', 1, 1),
(7, '/Assets/Stickers/Philippine Deer.webp', 1, 1),
(8, '/Assets/Stickers/Punch.webp', 1, 1),
(9, '/Assets/Stickers/Hollanov.webp', 1, 1),
(10, '/Assets/Stickers/Hollander.webp', 1, 1),
(11, '/Assets/Stickers/Rozanov.webp', 1, 1),
(12, '/Assets/Stickers/Good boy.webp', 1, 1),
(13, '/Assets/Stickers/Good girl.webp', 1, 1),
(14, '/Assets/Stickers/Gay af.webp', 1, 1),
(16, '/Assets/Stickers/Hinata.webp', 1, 1),
(17, '/Assets/Stickers/Zigzagoon.webp', 1, 1),
(18, '/Assets/Stickers/Project Hail Mary.webp', 1, 1),
(19, '/Assets/Stickers/Life is lifing.webp', 1, 1),
(20, '/Assets/Stickers/Fame whore.webp', 1, 1),
(26, '/Assets/Sticker Sheets/Fishies (4x3).webp', 1, 1),
(27, '/Assets/Sticker Sheets/Smiskis (4x3).webp', 1, 1),
(30, '/Assets/Art Prints/Trees (4x6).webp', 1, 1),
(32, '/Assets/Art Prints/Heated Rivalry (4x6).webp', 1, 1),
(33, '/Assets/Art Prints/Maya (5x7).webp', 1, 1),
(34, '/Assets/Art Prints/Bleeding Heart Pigeon (5x7).webp', 1, 1),
(35, '/Assets/Art Prints/Maral (5x7).webp', 1, 1),
(37, '/Assets/Art Prints/Stars will align (5x7).webp', 1, 1),
(39, '/Assets/Art Prints/Grace (5x7).webp', 1, 1),
(40, '/Assets/Art Prints/Olruggio (4x6).webp', 1, 1),
(42, '/Assets/Art Prints/Bawal umihi d2 (4x6).webp', 1, 1),
(49, '/Assets/Button Pins/Please I am a Star.webp', 1, 1),
(50, '/Assets/Button Pins/Gay af.webp', 1, 1),
(51, '/Assets/Button Pins/Doggo.webp', 1, 1),
(52, '/Assets/Button Pins/I love Nerdz.webp', 1, 1),
(53, '/Assets/Button Pins/Evil eye.webp', 1, 1),
(54, '/Assets/Button Pins/Please I am a Star.webp', 1, 1),
(55, '/Assets/Button Pins/Project Hail Mary.webp', 1, 1),
(56, '/Assets/Keychains/Goby keychain.webp', 1, 1),
(57, '/Assets/Keychains/Pigeon keychain.webp', 1, 1),
(58, '/Assets/Keychains/Bread tag keychain.webp', 1, 1),
(70, '/Assets/Stickers/Disappoint your parents.webp', 1, 1),
(71, '/Assets/Stickers/One day at a time.webp', 1, 1),
(73, '/Assets/Stickers/ICU Paint Tube.webp', 1, 1),
(74, '/Assets/Stickers/Drowning risk.webp', 1, 1),
(75, '/Assets/Stickers/Button Girl.webp', 1, 1),
(76, '/Assets/Stickers/Nothing matters.webp', 1, 1),
(77, '/Assets/Stickers/Tomorrow will be better.webp', 1, 1),
(78, '/Assets/Stickers/Always an angel.webp', 1, 1),
(79, '/Assets/Stickers/Button Girl.webp', 1, 1),
(80, '/Assets/Stickers/Phone.webp', 1, 1),
(81, '/Assets/Stickers/Spite the world.webp', 1, 1),
(82, '/Assets/Stickers/Lapida.webp', 1, 1),
(83, '/Assets/Stickers/What are you waiting for.webp', 1, 1),
(84, '/Assets/Stickers/Affirmations.webp', 1, 1),
(85, '/Assets/Stickers/idk lol.webp', 1, 1),
(86, '/Assets/Stickers/i forgor.webp', 1, 1),
(89, '/Assets/Stickers/Paper doll.webp', 1, 1),
(93, '/Assets/Sticker Sheets/Love sticker sheet (3x4).webp', 1, 1);

-- ---------- Event inventory (Galleria South tour) ----------
INSERT INTO EventInventory (EventId, ProductId, StartingStock, SoldQuantity, RemainingStock, IsEventExclusive, IsActive) VALUES
(1, 1, 25, 4, 21, 0, 1),
(1, 2, 20, 4, 16, 0, 1),
(1, 3, 15, 5, 10, 0, 1),
(1, 25, 10, 6, 4, 0, 1),
(1, 30, 10, 5, 5, 0, 1),
(1, 46, 10, 10, 0, 1, 1),
(1, 56, 12, 8, 4, 0, 1),
(1, 65, 8, 5, 3, 0, 1);

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
(1, 'Stickers Bundle (4 for 100)', 'Choose your favorite 4 stickers (Bleeding heart, Tamaraw, Tarsier, Goby) for only ₱100!', 16.67, 1),
(2, 'Button Pins Bundle (3 for 100)', 'Pick 3 premium 1.25 in. button pins (Bleed, Bangus, I luv stars) for only ₱100!', 4.76, 1),
(3, 'Artisan Prints & Sheet Set', 'Dinostarz sticker sheet + Trees art print set with matte finish', 15.00, 1);

INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES
(1, 1, 1), (1, 2, 1), (1, 3, 1), (1, 4, 1),
(2, 47, 1), (2, 48, 1), (2, 49, 1),
(3, 25, 1), (3, 30, 1);