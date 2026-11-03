/* Run before 07_SchemaFixes.sql. Each query lists rows that would block the fix. Empty result = safe. */
SET NOCOUNT ON;

SELECT 'Shops with Status outside 0..4' AS Check_, Id, Name, Status
FROM dbo.Shops WHERE Status NOT BETWEEN 0 AND 4;

SELECT 'Duplicate global category slugs' AS Check_, Slug, COUNT(*) AS Copies
FROM dbo.Categories WHERE IsGlobal = 1 GROUP BY Slug HAVING COUNT(*) > 1;

SELECT 'Duplicate shop category slugs' AS Check_, ShopId, Slug, COUNT(*) AS Copies
FROM dbo.Categories WHERE IsGlobal = 0 GROUP BY ShopId, Slug HAVING COUNT(*) > 1;

SELECT 'Shops with NULL VerificationStatus (fixed automatically by 07)' AS Check_, COUNT(*) AS Shops
FROM dbo.Shops WHERE VerificationStatus IS NULL;
