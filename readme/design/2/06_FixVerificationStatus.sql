/* Run once on a database where the shop script already inserted NULL.
   The C# Shop.VerificationStatus property is not nullable.
   Confirm the value first: in Domain/Shop.cs, enum VerificationStatus must
   have NotStarted = 0. If it does not, change the 0 below. */
SET NOCOUNT ON;

UPDATE dbo.Shops
SET VerificationStatus = 0          -- VerificationStatus.NotStarted
WHERE VerificationStatus IS NULL;

SELECT COUNT(*) AS ShopsFixed FROM dbo.Shops WHERE VerificationStatus = 0;
