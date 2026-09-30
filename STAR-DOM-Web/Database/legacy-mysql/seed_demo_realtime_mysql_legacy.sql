-- ============================================================
-- STAR:DOM — Real-time Demonstration & Kiosk Data Seed
-- Generates realistic active creator studios, orders, payments,
-- reviews, event sales, and pop-up locations for 2026.
-- ============================================================
USE stardom;

-- ---------- Additional Customers ----------
INSERT INTO Users (Id, Email, Username, FullName, Phone, PasswordHash, RoleId, Status, EmailVerified,
                   CommissionSlotCapacity, CommissionStartingPrice, CommissionTurnaround, CommissionFormats, CommissionTagline) VALUES
(10,'cloe@example.com',     'cloe',     'Cloe Valenzuela',   '09179012345', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', 1, 0, 0, '', '', ''),
(11,'mark@example.com',     'mark',     'Mark Alcantara',    '09170123456', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', 1, 0, 0, '', '', ''),
(12,'paolo@example.com',    'paolo',    'Paolo Reyes',       '09171239876', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', 1, 0, 0, '', '', '')
ON DUPLICATE KEY UPDATE FullName = VALUES(FullName), RoleId = VALUES(RoleId);

-- ---------- Additional Pop-up Tour Locations ----------
INSERT INTO StoreLocations (Id, Name, Venue, Address, City, Region, IsActive) VALUES
(2, 'STAR:DOM @ SM City Santa Rosa',         'Mall Expansion Wing, 2nd Floor', 'Santa Rosa-Tagaytay Road', 'Santa Rosa', 'Laguna / South Luzon', 1),
(3, 'STAR:DOM @ Festival Mall Alabang',      'Water Garden Hallway, Upper Ground', 'Filinvest City', 'Muntinlupa', 'Metro Manila South', 1),
(4, 'STAR:DOM @ Ayala Malls South Park',     'Level 3 Main Cinema Foyer', 'Alabang-Zapote Road', 'Muntinlupa', 'Metro Manila South', 1),
(5, 'STAR:DOM @ SM Mall of Asia',            'Bay Boulevard Activity Center', 'Mall of Asia Complex, Seaside', 'Pasay City', 'Metro Manila Bay Area', 1),
(6, 'STAR:DOM @ Robinsons Place Manila',     'Midtown Atrium Stage', 'Adriatico Street, Ermita', 'Manila', 'Metro Manila Central', 1)
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- ---------- Pop-Up Tour Events (Active in 2026) ----------
INSERT INTO PopUpEvents (Id, LocationId, Name, Description, StartDate, EndDate, OpenTime, CloseTime, BoothNumber,
                         VenueDetail, Status, FeaturedGuest, IsCurrent, LineupText) VALUES
(1, 1, 'STAR:DOM @ Robinson''s Galleria South', 'Touch prints, inspect merchandise, watch live sketching, and pay instantly via local Philippine payment rails. Convention sticker sheets and on-site custom sketch slots available.',
 '2026-09-01 10:00:00', '2026-09-30 21:00:00', '10:00 AM', '9:00 PM', 'Stall A-12', 'Ground Atrium Activity Center', 'NOW OPEN', 'Mika S. & Guild', 1, '15 Guest Creators'),
(2, 2, 'STAR:DOM @ SM City Santa Rosa', 'South Luzon Artisan Weekend Expo. Over 30 creators joining our combined pavilion with print swaps, sticker rallies, and live tablet painting demo sessions.',
 '2026-10-02 10:00:00', '2026-10-05 21:00:00', '10:00 AM', '9:00 PM', 'Booth D-04', 'Ground Atrium (Booth D-04)', 'UPCOMING', '@Kira_Illustration & Guild', 0, '12 Guest Creators'),
(3, 3, 'STAR:DOM @ Festival Mall Alabang', 'Metro Manila south bazaar with watercolor demos and gacha sticker dispensers.',
 '2026-10-16 10:00:00', '2026-10-19 21:00:00', '10:00 AM', '9:00 PM', 'Island F', 'Carousel Court (Island F)', 'UPCOMING', 'Renzo Cruz', 0, '18 Guest Creators'),
(4, 4, 'STAR:DOM @ Ayala Malls South Park', 'Indie comic & print exhibition, live ink sketches open at 11:00 AM daily.',
 '2026-10-30 10:00:00', '2026-11-02 21:00:00', '10:00 AM', '9:00 PM', 'Central Pod', 'Level 2 Activity Area', 'UPCOMING', 'Puffu Studio', 0, '10 Guest Creators'),
(5, 5, 'STAR:DOM @ SM Mall of Asia', 'Grand bay area creator showcase featuring live screen-printing and gacha dispensers.',
 '2026-11-13 10:00:00', '2026-11-16 21:00:00', '10:00 AM', '9:00 PM', 'Bay Pavilion 8', 'Seaside Boulevard', 'UPCOMING', 'Guild Collective', 0, '25 Guest Creators')
ON DUPLICATE KEY UPDATE Status = VALUES(Status), BoothNumber = VALUES(BoothNumber), StartDate = VALUES(StartDate), EndDate = VALUES(EndDate);

-- ---------- Real-Time Sample Orders & Payments ----------
INSERT INTO Orders (Id, OrderNumber, UserId, EventId, Status, Subtotal, DiscountAmount, ShippingFee, TotalAmount, PaymentMethod, PaymentStatus, ShippingAddress, ContactPhone, Notes, CreatedAt) VALUES
(1, 'ORD-2026-1001', 1, 1, 'DELIVERED', 450.00, 50.00, 80.00, 480.00, 'GCASH', 'PAID', 'Unit 402 Solenad Residences, Santa Rosa, Laguna', '09171234567', 'Please deliver after 2 PM', '2026-09-18 14:20:00'),
(2, 'ORD-2026-1002', 6, 1, 'SHIPPED',   360.00, 0.00, 80.00, 440.00, 'MAYA',  'PAID', '12 Rosal Street, San Pedro, Laguna', '09175678901', 'Leave with building security', '2026-09-20 11:15:00'),
(3, 'ORD-2026-1003', 10, NULL, 'PROCESSING', 620.00, 60.00, 100.00, 660.00, 'GCASH', 'PAID', '88 Katipunan Ave, Quezon City', '09179012345', 'Fragile art prints inside', '2026-09-22 16:45:00'),
(4, 'ORD-2026-1004', 11, NULL, 'CONFIRMED', 280.00, 0.00, 80.00, 360.00, 'COD', 'PENDING', 'Poblacion, Biñan City, Laguna', '09170123456', '', '2026-09-23 09:30:00'),
(5, 'ORD-2026-1005', 12, 1, 'DELIVERED', 850.00, 100.00, 80.00, 830.00, 'GCASH', 'PAID', 'Alabang Hills Village, Muntinlupa', '09171239876', 'Express delivery requested', '2026-09-24 10:10:00')
ON DUPLICATE KEY UPDATE PaymentStatus = VALUES(PaymentStatus);

-- ---------- Order Items ----------
INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice, LineTotal) VALUES
(1, 1, 3, 30.00, 90.00),
(1, 30, 2, 100.00, 200.00),
(1, 56, 1, 150.00, 150.00),
(2, 2, 2, 30.00, 60.00),
(2, 25, 2, 120.00, 240.00),
(2, 47, 2, 35.00, 70.00),
(3, 33, 2, 120.00, 240.00),
(3, 34, 2, 120.00, 240.00),
(3, 58, 1, 150.00, 150.00),
(4, 3, 2, 30.00, 60.00),
(4, 48, 2, 35.00, 70.00),
(4, 64, 1, 120.00, 120.00),
(5, 31, 3, 100.00, 300.00),
(5, 59, 2, 150.00, 300.00),
(5, 65, 3, 70.00, 210.00);

-- ---------- Payments ----------
INSERT INTO Payments (OrderId, PaymentMethod, Amount, ReferenceNumber, Status, PaidAt, GatewayResponse) VALUES
(1, 'GCASH', 480.00, 'GC-20260918-8831', 'PAID', '2026-09-18 14:22:00', 'SUCCESS: Approved by GCash Merchant Gateway'),
(2, 'MAYA',  440.00, 'MY-20260920-1944', 'PAID', '2026-09-20 11:17:00', 'SUCCESS: Approved by Maya QR Pay'),
(3, 'GCASH', 660.00, 'GC-20260922-5520', 'PAID', '2026-09-22 16:48:00', 'SUCCESS: Approved by GCash Merchant Gateway'),
(4, 'COD',   360.00, 'COD-PENDING',      'PENDING', NULL, 'PENDING: Cash upon delivery dispatch'),
(5, 'GCASH', 830.00, 'GC-20260924-9102', 'PAID', '2026-09-24 10:12:00', 'SUCCESS: Approved by GCash Merchant Gateway')
ON DUPLICATE KEY UPDATE Status = VALUES(Status);

-- ---------- Additional Event Sales (Live POS) ----------
INSERT INTO EventSales (EventId, OrderId, ProductId, Quantity, UnitPrice, TotalAmount, SaleType, PaymentMethod, SaleDate, Notes) VALUES
(1, 1, 1, 3, 30.00, 90.00, 'IN_PERSON', 'GCASH', '2026-09-18 14:20:00', 'Stall A-12 walk-in customer'),
(1, 1, 30, 2, 100.00, 200.00, 'IN_PERSON', 'GCASH', '2026-09-18 14:20:00', 'Framed art print pair'),
(1, 2, 2, 2, 30.00, 60.00, 'QR', 'MAYA', '2026-09-20 11:15:00', 'Stall A-12 QR scan counter'),
(1, 5, 31, 3, 100.00, 300.00, 'IN_PERSON', 'GCASH', '2026-09-24 10:10:00', 'Stall A-12 visitor bundle'),
(1, NULL, 47, 4, 35.00, 140.00, 'IN_PERSON', 'CASH', '2026-09-24 13:30:00', 'Button pins cash sale'),
(1, NULL, 64, 2, 120.00, 240.00, 'QR', 'GCASH', '2026-09-24 15:45:00', 'Webbing keychain QR scan');

-- ---------- Product Reviews ----------
INSERT INTO Reviews (ProductId, UserId, Rating, Comment, IsApproved, CreatedAt) VALUES
(1, 1, 5, 'Super high quality matte finish on the Bleeding heart sticker! Sticks solidly to my MacBook.', 1, '2026-09-19 15:00:00'),
(2, 6, 5, 'Love the Tamaraw sticker! Authentic Philippine biodiversity art.', 1, '2026-09-21 12:00:00'),
(30, 1, 5, 'The Trees 4x6 art print looks breathtaking on archival card.', 1, '2026-09-22 09:30:00'),
(25, 6, 4, 'Dinostarz sheet is super cute, colors are bright and die-cuts are crisp.', 1, '2026-09-23 14:00:00'),
(47, 10, 5, 'The Bleed button pin is solid 1.25 in. with premium pinback clasp.', 1, '2026-09-24 11:00:00');

-- Update product rating aggregations
UPDATE Products p SET 
    RatingAvg = (SELECT COALESCE(AVG(r.Rating), 5.0) FROM Reviews r WHERE r.ProductId = p.Id AND r.IsApproved = 1),
    RatingCount = (SELECT COUNT(*) FROM Reviews r WHERE r.ProductId = p.Id AND r.IsApproved = 1),
    SoldCount = SoldCount + 5
WHERE Id IN (1, 2, 25, 30, 47);
