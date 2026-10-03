-- Seed verification. Run this after supabase_seed.sql.
-- Every row should match the "expected" column; any mismatch means the seed did
-- not complete. The bottom block is the fixes themselves, not just counts.

SELECT check_name, actual, expected, CASE WHEN actual = expected THEN 'OK' ELSE 'FIX' END AS status
FROM (
    SELECT 'users'                                   AS check_name, COUNT(*)::text AS actual, '10'  AS expected FROM Users
    UNION ALL SELECT 'customers',                       COUNT(*)::text, '6'  FROM Users WHERE RoleId = 1
    UNION ALL SELECT 'store locations',                   COUNT(*)::text, '7'  FROM StoreLocations
    UNION ALL SELECT 'pop-up events',                     COUNT(*)::text, '7'  FROM PopUpEvents
    -- the itinerary is filled by recycling the finished events onto future
    -- dates, so every seeded run must still be ahead of the clock: one live
    -- (Ayala Malls South Park, 2-4 Oct 2026) and six still to come
    UNION ALL SELECT 'events still ahead of the clock',   COUNT(*)::text, '7'  FROM PopUpEvents WHERE EndDate >= NOW()
    UNION ALL SELECT 'events left in the past',           COUNT(*)::text, '0'  FROM PopUpEvents WHERE EndDate < NOW()
    UNION ALL SELECT 'venues with a region',              COUNT(*)::text, '7'  FROM StoreLocations WHERE Region <> ''
    UNION ALL SELECT 'products',                          COUNT(*)::text, '100' FROM Products
    UNION ALL SELECT 'reviews',                           COUNT(*)::text, '28' FROM Reviews
    -- the reviews are spread over six shoppers, not all signed by Bella
    UNION ALL SELECT 'distinct review authors',          COUNT(DISTINCT UserId)::text, '6' FROM Reviews
    UNION ALL SELECT 'products with a rating',            COUNT(*)::text, '28' FROM Products WHERE RatingCount > 0
    -- 134 / 28 = 4.79, the 4.8 figure the merchant report prints as Average rating
    UNION ALL SELECT 'average rating',                   ROUND(COALESCE(AVG(RatingAvg),0), 1)::text, '4.8' FROM Products WHERE RatingCount > 0
    -- an unrated product must carry 0 / 0, never a leftover 5.00 with no reviews
    UNION ALL SELECT 'unrated products still showing stars', COUNT(*)::text, '0'
        FROM Products WHERE RatingCount = 0 AND RatingAvg <> 0
    UNION ALL SELECT 'commissions',                       COUNT(*)::text, '6'  FROM Commissions

    -- the ACTIVE NODE flag must be clear everywhere
    UNION ALL SELECT 'events still flagged IsCurrent',    COUNT(*)::text, '0'
        FROM PopUpEvents WHERE IsCurrent

    -- each event must point at the venue with the matching id; rows 6 and 7 used
    -- to borrow venue 1 and 2, which quoted the wrong street on pick-up orders
    UNION ALL SELECT 'events on the wrong venue',         COUNT(*)::text, '0'
        FROM PopUpEvents e WHERE e.LocationId <> e.Id

    -- no event may point at a venue row that was never seeded
    UNION ALL SELECT 'events on a missing venue',         COUNT(*)::text, '0'
        FROM PopUpEvents e LEFT JOIN StoreLocations l ON l.Id = e.LocationId
        WHERE l.Id IS NULL

    -- BoothNumber must not carry its own prefix, or the UI prints "Booth Booth D-04"
    UNION ALL SELECT 'booth numbers with a prefix',       COUNT(*)::text, '0'
        FROM PopUpEvents WHERE BoothNumber ~* '^\s*booth\s+'

    -- no row may point at a user that no longer exists
    UNION ALL SELECT 'orphaned user references',          COUNT(*)::text, '0' FROM (
        SELECT c.CustomerId AS uid FROM Commissions c
        UNION ALL SELECT n.UserId FROM Notifications n
        UNION ALL SELECT m.SenderId FROM CommissionMessages m
        UNION ALL SELECT r.UserId FROM Reviews r
    ) x WHERE x.uid NOT IN (SELECT Id FROM Users)
) t;