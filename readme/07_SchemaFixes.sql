/* =====================================================================
   Kwikby: schema fixes (SQL Server 2014 compatible, safe to re-run)

   1. Shops.RejectedUtc and VerifiedUtc: datetime -> datetime2(7)
   2. Shops.VerificationStatus: NOT NULL, default 0 (matches the C# property)
   3. Shops.Status: CHECK constraint, values 0..4 (ShopStatus enum)
   4. Categories: unique slug among global categories, and unique slug
      per shop for shop categories (filtered unique indexes)

   Run 08_PreChecks first, or read the pre-check section below. The script
   stops if data would break a rule, so nothing is changed half-way.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

/* ---------- Pre-checks: stop if the data would violate a new rule ---------- */
IF EXISTS (SELECT 1 FROM dbo.Shops WHERE Status NOT BETWEEN 0 AND 4)
BEGIN
    RAISERROR(N'Some shops have a Status outside 0..4. Fix them first (see 08_PreChecks.sql).', 16, 1);
    RETURN;
END

IF EXISTS (SELECT Slug FROM dbo.Categories WHERE IsGlobal = 1 GROUP BY Slug HAVING COUNT(*) > 1)
BEGIN
    RAISERROR(N'Duplicate global category slugs exist. Remove the duplicates first (see 08_PreChecks.sql).', 16, 1);
    RETURN;
END

IF EXISTS (SELECT ShopId, Slug FROM dbo.Categories WHERE IsGlobal = 0 GROUP BY ShopId, Slug HAVING COUNT(*) > 1)
BEGIN
    RAISERROR(N'Duplicate shop category slugs exist. Remove the duplicates first (see 08_PreChecks.sql).', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

/* ---------- 1. Date columns to datetime2 ---------- */
ALTER TABLE dbo.Shops ALTER COLUMN RejectedUtc datetime2(7) NULL;
ALTER TABLE dbo.Shops ALTER COLUMN VerifiedUtc datetime2(7) NULL;

/* ---------- 2. VerificationStatus: NOT NULL with default 0 ---------- */
UPDATE dbo.Shops SET VerificationStatus = 0 WHERE VerificationStatus IS NULL;
ALTER TABLE dbo.Shops ALTER COLUMN VerificationStatus int NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE parent_object_id = OBJECT_ID(N'dbo.Shops')
                 AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Shops'), N'VerificationStatus', 'ColumnId'))
    ALTER TABLE dbo.Shops ADD CONSTRAINT DF_Shops_VerificationStatus DEFAULT 0 FOR VerificationStatus;

/* ---------- 3. Shop status range ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Shops_Status')
    ALTER TABLE dbo.Shops WITH CHECK
        ADD CONSTRAINT CK_Shops_Status CHECK (Status BETWEEN 0 AND 4);

/* ---------- 4. Category slug uniqueness ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Categories_GlobalSlug' AND object_id = OBJECT_ID(N'dbo.Categories'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_Categories_GlobalSlug
        ON dbo.Categories (Slug)
        WHERE IsGlobal = 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Categories_ShopSlug' AND object_id = OBJECT_ID(N'dbo.Categories'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_Categories_ShopSlug
        ON dbo.Categories (ShopId, Slug)
        WHERE IsGlobal = 0;

COMMIT TRANSACTION;

SELECT 'Schema fixes applied.' AS Result;
