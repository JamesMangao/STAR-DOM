-- STAR:DOM 2026 Luzon pop-up tour seed
-- Real venues across Metro Manila, Central Luzon, Southern Tagalog and the North.
-- Idempotent: INSERT ... ON DUPLICATE KEY UPDATE, explicit IDs, safe to re-run.
-- Event rows carry TimeZone = 'Asia/Manila'; statuses below match the clock-derived
-- rules at seed time (a live SM Mall of Asia run covers Sep 7-9, i.e. "today").

-- ---------- New store locations (Luzon-wide) ----------
INSERT INTO StoreLocations (Id, Name, Venue, Address, City, Region, IsActive) VALUES
(6, 'STAR:DOM @ SM Mall of Asia',          'Bay Boulevard Activity Center, Seaside', 'Mall of Asia Complex', 'Pasay City', 'Metro Manila / Bay Area', 1),
(7, 'STAR:DOM @ SM North EDSA',            'The Block Atrium, Ground Floor', 'EDSA cor. North Avenue', 'Quezon City', 'Metro Manila North', 1),
(8, 'STAR:DOM @ Greenhills Shopping Center', 'Uniwide Bazaar, West Wing', 'Greenhills Commercial Center', 'San Juan City', 'Metro Manila Central', 1),
(9, 'STAR:DOM @ Marquee Mall',             'Annex Event Center, Lower Ground', 'Angeles-Clark Road', 'Angeles City', 'Central Luzon', 1),
(10, 'STAR:DOM @ SM City Clark',           'Event Atrium, Main Building', 'Dolores, Clark Freeport Zone', 'Mabalacat', 'Central Luzon', 1),
(11, 'STAR:DOM @ Market! Market!',         'Garden Foyer, Level 1', 'Upper McKinley Hill, Fort Bonifacio', 'Taguig City', 'Metro Manila South', 1),
(12, 'STAR:DOM @ SM City Baguio',          'Cafe/Lobby Catwalk, Upper Ground', 'Upper Session Road, Luneta Hill', 'Baguio City', 'Cordillera / North Luzon', 1),
(13, 'STAR:DOM @ SM City Batangas',        'Main Atrium, Ground Floor', 'Ayala Highway, Pallocan West', 'Batangas City', 'Southern Tagalog', 1),
(14, 'STAR:DOM @ SM City Cabanatuan',      'Event Hall, 2nd Floor', 'Gen. Tinio Capitol Road', 'Cabanatuan City', 'Central Luzon (Nueva Ecija)', 1),
(15, 'STAR:DOM @ SM City San Pablo',       'Center Atrium, Ground Floor', 'National Highway, Brgy. San Rafael', 'San Pablo City', 'Laguna / South Luzon', 1)
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- ---------- New tour events ----------
INSERT INTO PopUpEvents (Id, LocationId, Name, Description, StartDate, EndDate, OpenTime, CloseTime, BoothNumber,
                         VenueDetail, Status, FeaturedGuest, IsCurrent, ImageFile, LineupText, TimeZone) VALUES
(8, 6, 'STAR:DOM @ SM Mall of Asia',
     'Grand Metro Manila kickoff by the bay. Oversized sticker gacha walls, live watercolor demos at sunset, and a print signing alley with our featured creators.',
     '2026-09-07 10:00:00', '2026-09-09 21:00:00', '10:00 AM', '9:00 PM', 'Stall B-07',
     'Bay Boulevard Activity Center (Stall B-07)', 'NOW OPEN', 'Mika S., Renzo Cruz & Guild', 1, '', '25 Guest Creators', 'Asia/Manila'),
(9, 7, 'STAR:DOM @ SM North EDSA',
     'North Metro convention weekend with enamel pin rallies, zine fair, and a commission corridor where 12 artists take live sketch requests on the spot.',
     '2026-09-25 10:00:00', '2026-09-27 21:00:00', '10:00 AM', '9:00 PM', 'The Block Pod 2',
     'The Block Atrium (The Block Pod 2)', 'UPCOMING', '@Kira_Illustration', 0, '', '18 Guest Creators', 'Asia/Manila'),
(10, 8, 'STAR:DOM @ Greenhills Shopping Center',
     'Neighborhood bazaar inside the Greenhills shopping complex — sticker rallies, mini print swaps, and a booth booth for booth-stock exclusives.',
     '2026-10-02 10:00:00', '2026-10-04 21:00:00', '10:00 AM', '9:00 PM', 'Uniwide Stall 12',
     'Uniwide Bazaar (Uniwide Stall 12)', 'UPCOMING', 'Puffu Studio', 0, '', '10 Guest Creators', 'Asia/Manila'),
(11, 9, 'STAR:DOM @ Marquee Mall',
     'Central Luzon''s biggest artisan weekend yet — 40 creators, a trading-card zone, live screen-printing, and gacha sticker machines for the whole family.',
     '2026-10-09 10:00:00', '2026-10-11 21:00:00', '10:00 AM', '9:00 PM', 'Annex Island F',
     'Annex Event Center (Annex Island F)', 'UPCOMING', 'Guild Collective', 0, '', '30 Guest Creators', 'Asia/Manila'),
(12, 11, 'STAR:DOM @ Market! Market!',
     'BGC-area artistic lifestyle fair with exclusive tote drops, enamel pin launches, and a late-evening open-air sketch jam on the garden deck.',
     '2026-10-23 10:00:00', '2026-10-25 21:00:00', '10:00 AM', '9:00 PM', 'Garden Pod A',
     'Garden Foyer (Garden Pod A)', 'UPCOMING', 'Renzo Cruz', 0, '', '20 Guest Creators', 'Asia/Manila'),
(13, 10, 'STAR:DOM @ SM City Clark',
     'North Central Luzon leg — convention-exclusive prints, sticker gacha wall, and a live artist alley beside the Clark event atrium.',
     '2026-11-06 10:00:00', '2026-11-08 21:00:00', '10:00 AM', '9:00 PM', 'Atrium Booth K-05',
     'Event Atrium (Atrium Booth K-05)', 'UPCOMING', 'Mika S. & Guild', 0, '', '22 Guest Creators', 'Asia/Manila'),
(14, 12, 'STAR:DOM @ SM City Baguio',
     'Cool-climate Cordillera leg: wool-and-print blend, handcrafted jams and coffee collabs, warm-weather sticker sheets, and an evening lantern sketch session.',
     '2026-11-13 10:00:00', '2026-11-15 21:00:00', '10:00 AM', '9:00 PM', 'Catwalk Stall C-3',
     'Cafe/Lobby Catwalk (Catwalk Stall C-3)', 'UPCOMING', 'Puffu Studio', 0, '', '16 Guest Creators', 'Asia/Manila'),
(15, 13, 'STAR:DOM @ SM City Batangas',
     'Southern Tagalog beachside leg — ocean-toned print edition, ferry-riders'' pickup point, and province-first sticker gacha installations.',
     '2026-11-20 10:00:00', '2026-11-22 21:00:00', '10:00 AM', '9:00 PM', 'Atrium Booth J-1',
     'Main Atrium (Atrium Booth J-1)', 'UPCOMING', '@Kira_Illustration', 0, '', '14 Guest Creators', 'Asia/Manila'),
(16, 14, 'STAR:DOM @ SM City Cabanatuan',
     'Rice-bowl region celebration — trading card rally, provincial print fair, and a school-break workshop track for young creators.',
     '2026-12-04 10:00:00', '2026-12-06 21:00:00', '10:00 AM', '9:00 PM', 'Event Hall Row 2',
     'Event Hall (Event Hall Row 2)', 'UPCOMING', 'Guild Collective', 0, '', '15 Guest Creators', 'Asia/Manila'),
(17, 15, 'STAR:DOM @ SM City San Pablo',
     'Laguna lakeside grand finale for the 2026 tour — stagione stickers, artist awards, and the last chance to finish your Stamp Card before the holidays.',
     '2026-12-11 10:00:00', '2026-12-13 21:00:00', '10:00 AM', '9:00 PM', 'Center Stall G-4',
     'Center Atrium (Center Stall G-4)', 'UPCOMING', 'Mika S. & Renzo Cruz', 0, '', '28 Guest Creators', 'Asia/Manila')
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- Keep a single canonical IsCurrent flag on the live-run event (clock remains authoritative at runtime).
UPDATE PopUpEvents SET IsCurrent = IF(Id = 8, 1, 0);