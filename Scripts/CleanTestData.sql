-- Script to clean up test data from integration tests
-- Run this to remove test records from your development database

-- WARNING: This will delete data! Make sure you're running against the correct database

PRINT 'Starting cleanup of test data...';

-- Delete test reservations (if any)
DELETE FROM Reservations 
WHERE Id IN (
    SELECT r.Id FROM Reservations r
    INNER JOIN Hotels h ON r.HotelId = h.Id
    WHERE h.Name LIKE 'Test Hotel%' OR h.Name LIKE '%Test%'
);
PRINT 'Deleted test reservations';

-- Delete test rooms
DELETE FROM Rooms 
WHERE HotelId IN (
    SELECT Id FROM Hotels 
    WHERE Name LIKE 'Test Hotel%' OR Name LIKE '%Test%'
);
PRINT 'Deleted test rooms';

-- Delete test hotels
DELETE FROM Hotels 
WHERE Name LIKE 'Test Hotel%' OR Name LIKE '%Test%';
PRINT 'Deleted test hotels';

-- Delete test users (be careful with this!)
DELETE FROM AspNetUsers 
WHERE Email LIKE 'test%@test.com' OR Email LIKE '%testroom%@test.com';
PRINT 'Deleted test users';

-- Clean up orphaned user roles
DELETE FROM AspNetUserRoles 
WHERE UserId NOT IN (SELECT Id FROM AspNetUsers);
PRINT 'Cleaned up orphaned user roles';

PRINT 'Cleanup complete!';

-- Show remaining counts
SELECT 'Hotels' AS TableName, COUNT(*) AS RecordCount FROM Hotels
UNION ALL
SELECT 'Rooms', COUNT(*) FROM Rooms
UNION ALL
SELECT 'Users', COUNT(*) FROM AspNetUsers
UNION ALL
SELECT 'Reservations', COUNT(*) FROM Reservations;
