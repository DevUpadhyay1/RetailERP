USE RetailERPDb;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
PRINT 'Cleaning up old test data...';
DELETE FROM Items WHERE SKU LIKE 'BLK-%';

PRINT 'Starting 100k item seed...';
DECLARE @i INT = 1;

-- Wrap in transaction for speed
BEGIN TRAN;
WHILE @i <= 100000
BEGIN
    INSERT INTO Items (ItemId, CompanyId, SKU, Name, Barcode, UnitPrice, MRP, PurchasePrice, GstPercent, IsActive, ReorderLevel, CreatedAtUtc, UpdatedAtUtc)
    VALUES (NEWID(), '00000000-0000-0000-0000-000000000001', CONCAT('BLK-', @i), CONCAT('Bulk Item ', @i), CONCAT('1000', @i), 150, 200, 100, 18, 1, 5, GETUTCDATE(), GETUTCDATE());
    SET @i = @i + 1;
END;
COMMIT TRAN;

PRINT 'Seed complete.';
GO
