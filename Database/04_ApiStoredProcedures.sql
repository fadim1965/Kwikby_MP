/* =====================================================================
   Kwikby API: stored procedures added for the REST API
   File:       Database/04_ApiStoredProcedures.sql
   Depends on: 00_Schema.sql (42 EF tables) and 02_StoredProcedures.sql
   Server:     SQL Server 2014 compatible (no CREATE OR ALTER)

   Scope for Phase 1 (read API + JWT sign-in):
     usp_Auth_GetUserForLogin
     usp_Auth_GetUserClaims
     usp_Auth_RecordLogin
     usp_Auth_RecordFailedLogin
     usp_Category_List
     usp_Shop_List
     usp_Shop_Get

   Phase 2+ appends the write procedures to this same file (or a later file);
   they are deliberately not present yet so the deployed surface matches the
   endpoints that actually exist.

   Error numbers (mapped by Infrastructure/SqlErrorTranslator.cs):
     52001 validation / zero quantity           -> 400
     52002 unknown enum value                   -> 400
     52003 entity not found                     -> 404
     52004 insufficient stock                   -> 409
     52005 duplicate slug / SKU / email         -> 409
     52006 invalid state transition             -> 409
     52007 forbidden shop / role scope          -> 403
     52008 concurrency conflict                 -> 409
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------
   Sign-in: one round trip returns the account, roles and shop memberships.
   The account row is returned even when inactive or locked; the API decides
   the response so the same 401 is returned for every failure.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Auth_GetUserForLogin', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Auth_GetUserForLogin AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Auth_GetUserForLogin
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Normalized NVARCHAR(256) = UPPER(LTRIM(RTRIM(@Email)));

    -- Result set 1: the account
    SELECT
        u.Id,
        u.Email,
        u.DisplayName,
        u.PasswordHash,
        u.IsActive,
        u.AccessFailedCount,
        u.LockoutEndUtc,
        u.LastLoginUtc
    FROM dbo.Users AS u
    WHERE u.NormalizedEmail = @Normalized;

    -- Result set 2: platform roles (Admin / Customer)
    SELECT r.RoleName
    FROM dbo.UserRoles AS r
    INNER JOIN dbo.Users AS u ON u.Id = r.UserId
    WHERE u.NormalizedEmail = @Normalized
    ORDER BY r.RoleName;

    -- Result set 3: shop memberships
    SELECT su.ShopId, su.Role
    FROM dbo.ShopUsers AS su
    INNER JOIN dbo.Users AS u ON u.Id = su.UserId
    WHERE u.NormalizedEmail = @Normalized
    ORDER BY su.ShopId;
END;
GO

/* ---------------------------------------------------------------------
   GET /auth/me: current account facts, refreshed from the database.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Auth_GetUserClaims', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Auth_GetUserClaims AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Auth_GetUserClaims
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: the account
    SELECT u.Id, u.Email, u.DisplayName, u.IsActive
    FROM dbo.Users AS u
    WHERE u.Id = @UserId;

    -- Result set 2: platform roles
    SELECT r.RoleName
    FROM dbo.UserRoles AS r
    WHERE r.UserId = @UserId
    ORDER BY r.RoleName;

    -- Result set 3: shop memberships
    SELECT su.ShopId, su.Role
    FROM dbo.ShopUsers AS su
    WHERE su.UserId = @UserId
    ORDER BY su.ShopId;
END;
GO

/* ---------------------------------------------------------------------
   Successful sign-in: reset the failure counter and record the timestamp.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Auth_RecordLogin', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Auth_RecordLogin AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Auth_RecordLogin
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET LastLoginUtc       = SYSUTCDATETIME(),
        AccessFailedCount  = 0,
        LockoutEndUtc      = NULL
    WHERE Id = @UserId;
END;
GO

/* ---------------------------------------------------------------------
   Failed sign-in: mirrors AccountService.RecordFailedLoginAsync exactly.
   Five failures lock the account for fifteen minutes and reset the counter.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Auth_RecordFailedLogin', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Auth_RecordFailedLogin AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Auth_RecordFailedLogin
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Normalized NVARCHAR(256) = UPPER(LTRIM(RTRIM(@Email)));
    DECLARE @UserId NVARCHAR(450);

    SELECT @UserId = u.Id
    FROM dbo.Users AS u
    WHERE u.NormalizedEmail = @Normalized;

    IF @UserId IS NULL
        RETURN;

    UPDATE dbo.Users
    SET AccessFailedCount = AccessFailedCount + 1
    WHERE Id = @UserId;

    UPDATE dbo.Users
    SET LockoutEndUtc      = DATEADD(MINUTE, 15, SYSUTCDATETIME()),
        AccessFailedCount  = 0
    WHERE Id = @UserId
      AND AccessFailedCount >= 5;
END;
GO

/* ---------------------------------------------------------------------
   Categories: public list (active only) or admin list (all).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Category_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Category_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Category_List
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.Id,
        c.ShopId,
        c.IsGlobal,
        c.Name,
        c.Slug,
        c.Description,
        c.ParentId,
        c.DisplayOrder,
        c.IsActive,
        c.CommissionRate
    FROM dbo.Categories AS c
    WHERE (@IncludeInactive = 1 OR c.IsActive = 1)
    ORDER BY c.DisplayOrder, c.Name;
END;
GO

/* ---------------------------------------------------------------------
   Shops: public list of approved shops. IncludeAll is reserved for the
   admin endpoints added in Phase 2.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Shop_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_List
    @Status     INT = NULL,
    @IncludeAll BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.Id,
        s.Name,
        s.Slug,
        s.Description,
        s.Tagline,
        s.Status,
        s.City,
        s.Country,
        s.LogoUrl,
        s.BannerUrl,
        s.IsFeatured,
        s.ThemeKey,
        s.Currency,
        s.CommissionRate,
        s.CreatedUtc,
        s.ApprovedUtc
    FROM dbo.Shops AS s
    WHERE (@IncludeAll = 1 OR s.Status = 1)              -- 1 = Approved
      AND (@Status IS NULL OR s.Status = @Status)
    ORDER BY s.IsFeatured DESC, s.Name;
END;
GO

/* ---------------------------------------------------------------------
   One approved shop with the public profile fields.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Shop_Get', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_Get AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_Get
    @ShopId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.Id,
        s.Name,
        s.Slug,
        s.Description,
        s.Tagline,
        s.Status,
        s.ContactName,
        s.Phone,
        s.Email,
        s.Website,
        s.AddressLine1,
        s.City,
        s.PostalCode,
        s.Country,
        s.PickupLocation,
        s.CommissionRate,
        s.FlatShippingRate,
        s.FreeShippingThreshold,
        s.Currency,
        s.ThemeKey,
        s.LogoUrl,
        s.BannerUrl,
        s.IsFeatured,
        s.CreatedUtc,
        s.ApprovedUtc,
        s.SuspendedUtc,
        s.RejectedUtc
    FROM dbo.Shops AS s
    WHERE s.Id = @ShopId
      AND s.Status = 1;                                  -- 1 = Approved
END;
GO
/* ---------------------------------------------------------------------
   Categories: single read, create, update, soft-delete (admin).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Category_Get', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Category_Get AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Category_Get
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.Id,
        c.ShopId,
        c.IsGlobal,
        c.Name,
        c.Slug,
        c.Description,
        c.ParentId,
        c.DisplayOrder,
        c.IsActive,
        c.CommissionRate
    FROM dbo.Categories AS c
    WHERE c.Id = @CategoryId;

    IF @@ROWCOUNT = 0
        THROW 52003, N'Category not found.', 1;
END;
GO

IF OBJECT_ID(N'dbo.usp_Category_Create', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Category_Create AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Category_Create
    @Name            NVARCHAR(80),
    @Slug            NVARCHAR(80),
    @Description     NVARCHAR(500) = NULL,
    @ParentId        INT           = NULL,
    @DisplayOrder    INT           = 0,
    @IsActive        BIT           = 1,
    @CommissionRate  DECIMAL(18,4)= NULL,
    @IsGlobal        BIT           = 1,
    @ShopId          INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Categories WHERE Slug = @Slug
               AND ((ShopId IS NULL AND @ShopId IS NULL) OR ShopId = @ShopId))
        THROW 52005, N'A category with this slug already exists.', 1;

    INSERT INTO dbo.Categories
        (ShopId, IsGlobal, Name, Slug, Description, ParentId, DisplayOrder, IsActive, CommissionRate)
    VALUES
        (@ShopId, @IsGlobal, @Name, @Slug, @Description, @ParentId, @DisplayOrder, @IsActive, @CommissionRate);

    DECLARE @NewId INT = SCOPE_IDENTITY();

    SELECT
        c.Id, c.ShopId, c.IsGlobal, c.Name, c.Slug, c.Description, c.ParentId,
        c.DisplayOrder, c.IsActive, c.CommissionRate
    FROM dbo.Categories AS c
    WHERE c.Id = @NewId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Category_Update', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Category_Update AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Category_Update
    @CategoryId      INT,
    @Name            NVARCHAR(80),
    @Slug            NVARCHAR(80),
    @Description     NVARCHAR(500) = NULL,
    @ParentId        INT           = NULL,
    @DisplayOrder    INT           = 0,
    @IsActive        BIT           = 1,
    @CommissionRate  DECIMAL(18,4)= NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Id = @CategoryId)
        THROW 52003, N'Category not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Categories WHERE Slug = @Slug AND Id <> @CategoryId
               AND ((ShopId IS NULL AND (SELECT ShopId FROM dbo.Categories WHERE Id = @CategoryId) IS NULL)
                    OR ShopId = (SELECT ShopId FROM dbo.Categories WHERE Id = @CategoryId)))
        THROW 52005, N'A category with this slug already exists.', 1;

    UPDATE dbo.Categories
       SET Name = @Name,
           Slug = @Slug,
           Description = @Description,
           ParentId = @ParentId,
           DisplayOrder = @DisplayOrder,
           IsActive = @IsActive,
           CommissionRate = @CommissionRate
     WHERE Id = @CategoryId;

    SELECT
        c.Id, c.ShopId, c.IsGlobal, c.Name, c.Slug, c.Description, c.ParentId,
        c.DisplayOrder, c.IsActive, c.CommissionRate
    FROM dbo.Categories AS c
    WHERE c.Id = @CategoryId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Category_Delete', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Category_Delete AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Category_Delete
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Id = @CategoryId)
        THROW 52003, N'Category not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Products WHERE CategoryId = @CategoryId AND IsDeleted = 0)
        THROW 52006, N'Category is in use by products.', 1;

    DELETE FROM dbo.Categories WHERE Id = @CategoryId;
END;
GO
/* ---------------------------------------------------------------------
   Products: admin/vendor list and detail.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Product_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_List
    @ShopId          INT           = NULL,
    @CategoryId      INT           = NULL,
    @Query           NVARCHAR(200) = NULL,
    @IsPublished     BIT           = NULL,
    @ModerationStatus INT          = NULL,
    @Page            INT           = 1,
    @PageSize        INT           = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;

    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        p.Id          AS ProductId,
        p.Name,
        p.Slug,
        p.Brand,
        p.IsFeatured,
        p.CategoryId,
        p.ShopId,
        s.Name        AS ShopName,
        s.Slug        AS ShopSlug,
        pr.MinPrice,
        st.TotalStock,
        rv.AvgRating,
        rv.ReviewCount,
        p.IsPublished,
        p.ModerationStatus,
        p.IsDeleted,
        p.CreatedUtc,
        p.UpdatedUtc
    FROM dbo.Products AS p
    INNER JOIN dbo.Shops AS s ON s.Id = p.ShopId
    CROSS APPLY (SELECT MIN(v.Price) AS MinPrice FROM dbo.ProductVariants v WHERE v.ProductId = p.Id AND v.IsActive = 1) AS pr
    CROSS APPLY (SELECT ISNULL(SUM(v.StockQuantity), 0) AS TotalStock FROM dbo.ProductVariants v WHERE v.ProductId = p.Id AND v.IsActive = 1) AS st
    CROSS APPLY (SELECT CAST(AVG(CAST(r.Rating AS DECIMAL(5,2))) AS DECIMAL(5,2)) AS AvgRating, COUNT(*) AS ReviewCount FROM dbo.Reviews r WHERE r.ProductId = p.Id AND r.Status = 1) AS rv
    WHERE p.IsDeleted = 0
      AND (@ShopId IS NULL OR p.ShopId = @ShopId)
      AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
      AND (@IsPublished IS NULL OR p.IsPublished = @IsPublished)
      AND (@ModerationStatus IS NULL OR p.ModerationStatus = @ModerationStatus)
      AND (@Query IS NULL OR p.Name LIKE N'%' + @Query + N'%' OR p.Brand LIKE N'%' + @Query + N'%')
    ORDER BY p.UpdatedUtc DESC, p.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.Products AS p
    WHERE p.IsDeleted = 0
      AND (@ShopId IS NULL OR p.ShopId = @ShopId)
      AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
      AND (@IsPublished IS NULL OR p.IsPublished = @IsPublished)
      AND (@ModerationStatus IS NULL OR p.ModerationStatus = @ModerationStatus)
      AND (@Query IS NULL OR p.Name LIKE N'%' + @Query + N'%' OR p.Brand LIKE N'%' + @Query + N'%');
END;
GO

IF OBJECT_ID(N'dbo.usp_Product_Get', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_Get AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_Get
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id AS ProductId,
        p.Name, p.Slug, p.Brand, p.ShortDescription, p.Description,
        p.CategoryId, p.ShopId, s.Name AS ShopName, s.Slug AS ShopSlug,
        p.TaxClassId, p.IsPublished, p.IsDeleted, p.IsFeatured,
        p.ModerationStatus, p.ModerationReason, p.ModeratedUtc,
        p.CreatedUtc, p.UpdatedUtc
    FROM dbo.Products AS p
    INNER JOIN dbo.Shops AS s ON s.Id = p.ShopId
    WHERE p.Id = @ProductId;

    IF @@ROWCOUNT = 0 THROW 52003, N'Product not found.', 1;

    SELECT pv.Id AS ProductVariantId, pv.Sku, pv.Name, pv.Price, pv.CompareAtPrice,
           pv.StockQuantity, pv.IsActive, pv.LowStockThreshold
    FROM dbo.ProductVariants AS pv
    WHERE pv.ProductId = @ProductId
    ORDER BY pv.Id;

    SELECT pi.Id AS ProductImageId, pi.Url, pi.AltText, pi.DisplayOrder, pi.IsPrimary
    FROM dbo.ProductImages AS pi
    WHERE pi.ProductId = @ProductId
    ORDER BY pi.DisplayOrder, pi.Id;
END;
GO

IF OBJECT_ID(N'dbo.usp_Product_Create', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_Create AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_Create
    @ShopId           INT,
    @Name             NVARCHAR(160),
    @Slug             NVARCHAR(160),
    @ShortDescription NVARCHAR(200) = NULL,
    @Description      NVARCHAR(MAX) = NULL,
    @Brand            NVARCHAR(120) = NULL,
    @CategoryId       INT,
    @TaxClassId       INT           = NULL,
    @IsPublished      BIT           = 1,
    @IsFeatured       BIT           = 0,
    @ModerationStatus INT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Shops WHERE Id = @ShopId)
        THROW 52003, N'Shop not found.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Id = @CategoryId)
        THROW 52003, N'Category not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Products WHERE ShopId = @ShopId AND Slug = @Slug AND IsDeleted = 0)
        THROW 52005, N'A product with this slug already exists for the shop.', 1;

    INSERT INTO dbo.Products
        (ShopId, Name, Slug, ShortDescription, Description, Brand, CategoryId, TaxClassId,
         IsPublished, IsDeleted, IsFeatured, ModerationStatus, CreatedUtc, UpdatedUtc)
    VALUES
        (@ShopId, @Name, @Slug, @ShortDescription, @Description, @Brand, @CategoryId, @TaxClassId,
         @IsPublished, 0, @IsFeatured, @ModerationStatus, SYSUTCDATETIME(), SYSUTCDATETIME());

    DECLARE @NewId INT = SCOPE_IDENTITY();

    SELECT
        p.Id AS ProductId, p.Name, p.Slug, p.Brand, p.ShortDescription, p.Description,
        p.CategoryId, p.ShopId, s.Name AS ShopName, s.Slug AS ShopSlug,
        p.TaxClassId, p.IsPublished, p.IsDeleted, p.IsFeatured, p.ModerationStatus,
        p.ModerationReason, p.ModeratedUtc, p.CreatedUtc, p.UpdatedUtc
    FROM dbo.Products AS p
    INNER JOIN dbo.Shops AS s ON s.Id = p.ShopId
    WHERE p.Id = @NewId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Product_Update', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_Update AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_Update
    @ProductId        INT,
    @Name             NVARCHAR(160),
    @Slug             NVARCHAR(160),
    @ShortDescription NVARCHAR(200) = NULL,
    @Description      NVARCHAR(MAX) = NULL,
    @Brand            NVARCHAR(120) = NULL,
    @CategoryId       INT,
    @TaxClassId       INT           = NULL,
    @IsPublished      BIT           = 1,
    @IsFeatured       BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0)
        THROW 52003, N'Product not found.', 1;

    DECLARE @ShopId INT = (SELECT ShopId FROM dbo.Products WHERE Id = @ProductId);

    IF EXISTS (SELECT 1 FROM dbo.Products WHERE ShopId = @ShopId AND Slug = @Slug AND Id <> @ProductId AND IsDeleted = 0)
        THROW 52005, N'A product with this slug already exists for the shop.', 1;

    UPDATE dbo.Products
       SET Name = @Name,
           Slug = @Slug,
           ShortDescription = @ShortDescription,
           Description = @Description,
           Brand = @Brand,
           CategoryId = @CategoryId,
           TaxClassId = @TaxClassId,
           IsPublished = @IsPublished,
           IsFeatured = @IsFeatured,
           UpdatedUtc = SYSUTCDATETIME()
     WHERE Id = @ProductId;

    SELECT
        p.Id AS ProductId, p.Name, p.Slug, p.Brand, p.ShortDescription, p.Description,
        p.CategoryId, p.ShopId, s.Name AS ShopName, s.Slug AS ShopSlug,
        p.TaxClassId, p.IsPublished, p.IsDeleted, p.IsFeatured, p.ModerationStatus,
        p.ModerationReason, p.ModeratedUtc, p.CreatedUtc, p.UpdatedUtc
    FROM dbo.Products AS p
    INNER JOIN dbo.Shops AS s ON s.Id = p.ShopId
    WHERE p.Id = @ProductId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Product_SoftDelete', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_SoftDelete AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_SoftDelete
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0)
        THROW 52003, N'Product not found.', 1;

    UPDATE dbo.Products
       SET IsDeleted = 1,
           IsPublished = 0,
           UpdatedUtc = SYSUTCDATETIME()
     WHERE Id = @ProductId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Product_SetPublished', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_SetPublished AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_SetPublished
    @ProductId INT,
    @IsPublished BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0)
        THROW 52003, N'Product not found.', 1;

    UPDATE dbo.Products
       SET IsPublished = @IsPublished,
           UpdatedUtc = SYSUTCDATETIME()
     WHERE Id = @ProductId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Product_SetModeration', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_SetModeration AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_SetModeration
    @ProductId INT,
    @ModerationStatus INT,
    @Reason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0)
        THROW 52003, N'Product not found.', 1;

    UPDATE dbo.Products
       SET ModerationStatus = @ModerationStatus,
           ModerationReason = @Reason,
           ModeratedUtc = SYSUTCDATETIME(),
           UpdatedUtc = SYSUTCDATETIME()
     WHERE Id = @ProductId;
END;
GO
/* ---------------------------------------------------------------------
   Variants and images.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Variant_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Variant_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Variant_List
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pv.Id AS ProductVariantId, pv.Sku, pv.Name, pv.Price, pv.CompareAtPrice,
           pv.StockQuantity, pv.IsActive, pv.LowStockThreshold
    FROM dbo.ProductVariants AS pv
    WHERE pv.ProductId = @ProductId
    ORDER BY pv.Id;
END;
GO

IF OBJECT_ID(N'dbo.usp_Variant_Create', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Variant_Create AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Variant_Create
    @ProductId        INT,
    @Sku              NVARCHAR(64),
    @Name             NVARCHAR(80) = NULL,
    @Price            DECIMAL(18,2),
    @CompareAtPrice   DECIMAL(18,2) = NULL,
    @StockQuantity    INT = 0,
    @IsActive         BIT = 1,
    @LowStockThreshold INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0)
        THROW 52003, N'Product not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.ProductVariants WHERE Sku = @Sku)
        THROW 52005, N'A variant with this SKU already exists.', 1;

    INSERT INTO dbo.ProductVariants
        (ProductId, Sku, Name, Price, CompareAtPrice, StockQuantity, IsActive, LowStockThreshold, RowVersion)
    VALUES
        (@ProductId, @Sku, ISNULL(@Name, N''), @Price, @CompareAtPrice, @StockQuantity, @IsActive, @LowStockThreshold, 0x01);

    DECLARE @NewId INT = SCOPE_IDENTITY();

    SELECT pv.Id AS ProductVariantId, pv.Sku, pv.Name, pv.Price, pv.CompareAtPrice,
           pv.StockQuantity, pv.IsActive, pv.LowStockThreshold
    FROM dbo.ProductVariants AS pv
    WHERE pv.Id = @NewId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Variant_Update', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Variant_Update AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Variant_Update
    @VariantId        INT,
    @Sku              NVARCHAR(64),
    @Name             NVARCHAR(80) = NULL,
    @Price            DECIMAL(18,2),
    @CompareAtPrice   DECIMAL(18,2) = NULL,
    @StockQuantity    INT,
    @IsActive         BIT,
    @LowStockThreshold INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.ProductVariants WHERE Id = @VariantId)
        THROW 52003, N'Variant not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.ProductVariants WHERE Sku = @Sku AND Id <> @VariantId)
        THROW 52005, N'A variant with this SKU already exists.', 1;

    UPDATE dbo.ProductVariants
       SET Sku = @Sku,
           Name = ISNULL(@Name, N''),
           Price = @Price,
           CompareAtPrice = @CompareAtPrice,
           StockQuantity = @StockQuantity,
           IsActive = @IsActive,
           LowStockThreshold = @LowStockThreshold
     WHERE Id = @VariantId;

    SELECT pv.Id AS ProductVariantId, pv.Sku, pv.Name, pv.Price, pv.CompareAtPrice,
           pv.StockQuantity, pv.IsActive, pv.LowStockThreshold
    FROM dbo.ProductVariants AS pv
    WHERE pv.Id = @VariantId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Variant_Delete', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Variant_Delete AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Variant_Delete
    @VariantId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.ProductVariants WHERE Id = @VariantId)
        THROW 52003, N'Variant not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.OrderLines WHERE ProductVariantId = @VariantId)
        THROW 52006, N'Variant is referenced by orders.', 1;

    DELETE FROM dbo.ProductVariants WHERE Id = @VariantId;
END;
GO

/* Images */
IF OBJECT_ID(N'dbo.usp_Image_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Image_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Image_List
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pi.Id AS ProductImageId, pi.Url, pi.AltText, pi.DisplayOrder, pi.IsPrimary
    FROM dbo.ProductImages AS pi
    WHERE pi.ProductId = @ProductId
    ORDER BY pi.DisplayOrder, pi.Id;
END;
GO

IF OBJECT_ID(N'dbo.usp_Image_Add', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Image_Add AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Image_Add
    @ProductId    INT,
    @Url          NVARCHAR(MAX),
    @AltText      NVARCHAR(200) = NULL,
    @DisplayOrder INT = 0,
    @IsPrimary    BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0)
        THROW 52003, N'Product not found.', 1;

    INSERT INTO dbo.ProductImages (ProductId, Url, AltText, DisplayOrder, IsPrimary)
    VALUES (@ProductId, @Url, @AltText, @DisplayOrder, @IsPrimary);

    DECLARE @NewId INT = SCOPE_IDENTITY();

    SELECT pi.Id AS ProductImageId, pi.Url, pi.AltText, pi.DisplayOrder, pi.IsPrimary
    FROM dbo.ProductImages AS pi
    WHERE pi.Id = @NewId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Image_Delete', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Image_Delete AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Image_Delete
    @ImageId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.ProductImages WHERE Id = @ImageId)
        THROW 52003, N'Image not found.', 1;

    DELETE FROM dbo.ProductImages WHERE Id = @ImageId;
END;
GO
/* ---------------------------------------------------------------------
   Shops: create, update, status transitions.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Shop_Create', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_Create AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_Create
    @OwnerUserId NVARCHAR(450),
    @Name NVARCHAR(120),
    @Slug NVARCHAR(120),
    @Description NVARCHAR(600) = NULL,
    @Tagline NVARCHAR(160) = NULL,
    @ContactName NVARCHAR(120) = NULL,
    @Phone NVARCHAR(40) = NULL,
    @Email NVARCHAR(256) = NULL,
    @Website NVARCHAR(200) = NULL,
    @AddressLine1 NVARCHAR(200) = NULL,
    @City NVARCHAR(100) = NULL,
    @PostalCode NVARCHAR(40) = NULL,
    @Country NVARCHAR(80) = NULL,
    @PickupLocation NVARCHAR(120) = NULL,
    @CommissionRate DECIMAL(18,4) = 0.10,
    @FlatShippingRate DECIMAL(18,2) = NULL,
    @FreeShippingThreshold DECIMAL(18,2) = NULL,
    @Currency NVARCHAR(8) = 'USD',
    @ThemeKey NVARCHAR(40) = 'postal-classic'
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Shops WHERE Slug = @Slug)
        THROW 52005, N'A shop with this slug already exists.', 1;

    INSERT INTO dbo.Shops
        (OwnerUserId, Name, Slug, Description, Tagline, Status, StatusReason, ContactName, Phone, Email, Website,
         AddressLine1, City, PostalCode, Country, PickupLocation, CommissionRate, FlatShippingRate, FreeShippingThreshold,
         Currency, ThemeKey, LogoUrl, BannerUrl, IsFeatured, VerificationStatus, CreatedUtc, ApprovedUtc)
    VALUES
        (@OwnerUserId, @Name, @Slug, @Description, @Tagline, 0, NULL, @ContactName, @Phone, @Email, @Website,
         @AddressLine1, @City, @PostalCode, @Country, @PickupLocation, @CommissionRate, @FlatShippingRate, @FreeShippingThreshold,
         @Currency, @ThemeKey, NULL, NULL, 0, 0, SYSUTCDATETIME(), NULL);

    DECLARE @NewId INT = SCOPE_IDENTITY();

    SELECT s.Id, s.Name, s.Slug, s.Description, s.Tagline, s.Status, s.City, s.Country, s.LogoUrl, s.BannerUrl,
           s.IsFeatured, s.ThemeKey, s.Currency, s.CommissionRate, s.CreatedUtc, s.ApprovedUtc,
           s.ContactName, s.Phone, s.Email, s.Website, s.AddressLine1, s.PostalCode, s.PickupLocation,
           s.FlatShippingRate, s.FreeShippingThreshold, s.SuspendedUtc, s.RejectedUtc
    FROM dbo.Shops AS s WHERE s.Id = @NewId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Shop_Update', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_Update AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_Update
    @ShopId INT,
    @Name NVARCHAR(120),
    @Slug NVARCHAR(120),
    @Description NVARCHAR(600) = NULL,
    @Tagline NVARCHAR(160) = NULL,
    @ContactName NVARCHAR(120) = NULL,
    @Phone NVARCHAR(40) = NULL,
    @Email NVARCHAR(256) = NULL,
    @Website NVARCHAR(200) = NULL,
    @AddressLine1 NVARCHAR(200) = NULL,
    @City NVARCHAR(100) = NULL,
    @PostalCode NVARCHAR(40) = NULL,
    @Country NVARCHAR(80) = NULL,
    @PickupLocation NVARCHAR(120) = NULL,
    @CommissionRate DECIMAL(18,4) = 0.10,
    @FlatShippingRate DECIMAL(18,2) = NULL,
    @FreeShippingThreshold DECIMAL(18,2) = NULL,
    @Currency NVARCHAR(8) = 'USD',
    @ThemeKey NVARCHAR(40) = 'postal-classic',
    @IsFeatured BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Shops WHERE Id = @ShopId)
        THROW 52003, N'Shop not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Shops WHERE Slug = @Slug AND Id <> @ShopId)
        THROW 52005, N'A shop with this slug already exists.', 1;

    UPDATE dbo.Shops
       SET Name = @Name, Slug = @Slug, Description = @Description, Tagline = @Tagline,
           ContactName = @ContactName, Phone = @Phone, Email = @Email, Website = @Website,
           AddressLine1 = @AddressLine1, City = @City, PostalCode = @PostalCode, Country = @Country,
           PickupLocation = @PickupLocation, CommissionRate = @CommissionRate,
           FlatShippingRate = @FlatShippingRate, FreeShippingThreshold = @FreeShippingThreshold,
           Currency = @Currency, ThemeKey = @ThemeKey, IsFeatured = @IsFeatured
     WHERE Id = @ShopId;

    SELECT s.Id, s.Name, s.Slug, s.Description, s.Tagline, s.Status, s.City, s.Country, s.LogoUrl, s.BannerUrl,
           s.IsFeatured, s.ThemeKey, s.Currency, s.CommissionRate, s.CreatedUtc, s.ApprovedUtc,
           s.ContactName, s.Phone, s.Email, s.Website, s.AddressLine1, s.PostalCode, s.PickupLocation,
           s.FlatShippingRate, s.FreeShippingThreshold, s.SuspendedUtc, s.RejectedUtc
    FROM dbo.Shops AS s WHERE s.Id = @ShopId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Shop_SetStatus', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_SetStatus AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_SetStatus
    @ShopId INT,
    @Status INT,
    @Reason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Shops WHERE Id = @ShopId)
        THROW 52003, N'Shop not found.', 1;

    UPDATE dbo.Shops
       SET Status = @Status,
           StatusReason = @Reason,
           ApprovedUtc = CASE WHEN @Status = 1 THEN ISNULL(ApprovedUtc, SYSUTCDATETIME()) ELSE ApprovedUtc END,
           SuspendedUtc = CASE WHEN @Status = 3 THEN SYSUTCDATETIME() ELSE SuspendedUtc END,
           RejectedUtc = CASE WHEN @Status = 2 THEN SYSUTCDATETIME() ELSE RejectedUtc END
     WHERE Id = @ShopId;

    SELECT s.Id, s.Name, s.Slug, s.Description, s.Tagline, s.Status, s.City, s.Country, s.LogoUrl, s.BannerUrl,
           s.IsFeatured, s.ThemeKey, s.Currency, s.CommissionRate, s.CreatedUtc, s.ApprovedUtc,
           s.ContactName, s.Phone, s.Email, s.Website, s.AddressLine1, s.PostalCode, s.PickupLocation,
           s.FlatShippingRate, s.FreeShippingThreshold, s.SuspendedUtc, s.RejectedUtc
    FROM dbo.Shops AS s WHERE s.Id = @ShopId;
END;
GO

/* ---------------------------------------------------------------------
   Shop team members.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Shop_User_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_User_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_User_List
    @ShopId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT su.ShopId, su.UserId, su.Role, u.Email, u.DisplayName
    FROM dbo.ShopUsers AS su
    INNER JOIN dbo.Users AS u ON u.Id = su.UserId
    WHERE su.ShopId = @ShopId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Shop_User_Add', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_User_Add AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_User_Add
    @ShopId INT,
    @UserId NVARCHAR(450),
    @Role INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Shops WHERE Id = @ShopId)
        THROW 52003, N'Shop not found.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @UserId)
        THROW 52003, N'User not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.ShopUsers WHERE ShopId = @ShopId AND UserId = @UserId)
        THROW 52005, N'User is already a member of this shop.', 1;

    INSERT INTO dbo.ShopUsers (ShopId, UserId, Role, AcceptedTermsUtc, CreatedUtc)
    VALUES (@ShopId, @UserId, @Role, SYSUTCDATETIME(), SYSUTCDATETIME());
END;
GO

IF OBJECT_ID(N'dbo.usp_Shop_User_Remove', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Shop_User_Remove AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Shop_User_Remove
    @ShopId INT,
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.ShopUsers WHERE ShopId = @ShopId AND UserId = @UserId;

    IF @@ROWCOUNT = 0
        THROW 52003, N'Membership not found.', 1;
END;
GO

/* ---------------------------------------------------------------------
   Inventory.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Stock_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Stock_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Stock_List
    @ShopId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pv.Id AS ProductVariantId,
        p.Id AS ProductId,
        p.Name AS ProductName,
        pv.Sku,
        pv.Name AS VariantName,
        pv.StockQuantity,
        pv.LowStockThreshold,
        pv.IsActive,
        p.ShopId
    FROM dbo.ProductVariants AS pv
    INNER JOIN dbo.Products AS p ON p.Id = pv.ProductId
    WHERE p.IsDeleted = 0
      AND (@ShopId IS NULL OR p.ShopId = @ShopId)
    ORDER BY p.Name, pv.Sku;
END;
GO

IF OBJECT_ID(N'dbo.usp_StockMovements_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_StockMovements_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_StockMovements_List
    @VariantId INT = NULL,
    @ShopId INT = NULL,
    @Page INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 200 SET @PageSize = 50;

    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        sm.Id,
        sm.ProductVariantId,
        pv.Sku,
        p.Id AS ProductId,
        p.Name AS ProductName,
        sm.QuantityDelta,
        sm.StockAfter,
        sm.Reason,
        sm.Note,
        sm.CreatedUtc,
        sm.CreatedBy
    FROM dbo.StockMovements AS sm
    INNER JOIN dbo.ProductVariants AS pv ON pv.Id = sm.ProductVariantId
    INNER JOIN dbo.Products AS p ON p.Id = pv.ProductId
    WHERE (@VariantId IS NULL OR sm.ProductVariantId = @VariantId)
      AND (@ShopId IS NULL OR p.ShopId = @ShopId)
    ORDER BY sm.CreatedUtc DESC, sm.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END;
GO
/* ---------------------------------------------------------------------
   Orders: list, get, status update.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Order_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Order_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Order_List
    @ShopId  INT = NULL,
    @UserId  NVARCHAR(450) = NULL,
    @Status  INT = NULL,
    @Page    INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;
    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        o.Id, o.ShopId, s.Name AS ShopName, o.OrderNumber, o.UserId, o.Email, o.CustomerName, o.Phone,
        o.Status, o.PaymentMethod, o.CollectionStatus, o.CodAmount, o.Subtotal, o.ShippingTotal, o.TaxTotal,
        o.DiscountTotal, o.GrandTotal, o.RefundedTotal, o.CouponCode, o.PlacedUtc, o.PaidUtc, o.ShippedUtc,
        o.DeliveredUtc, o.CancelledUtc, o.TrackingNumber, o.Carrier
    FROM dbo.Orders AS o
    INNER JOIN dbo.Shops AS s ON s.Id = o.ShopId
    WHERE (@ShopId IS NULL OR o.ShopId = @ShopId)
      AND (@UserId IS NULL OR o.UserId = @UserId)
      AND (@Status IS NULL OR o.Status = @Status)
    ORDER BY o.PlacedUtc DESC, o.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.Orders AS o
    WHERE (@ShopId IS NULL OR o.ShopId = @ShopId)
      AND (@UserId IS NULL OR o.UserId = @UserId)
      AND (@Status IS NULL OR o.Status = @Status);
END;
GO

IF OBJECT_ID(N'dbo.usp_Order_Get', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Order_Get AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Order_Get
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.Id, o.ShopId, s.Name AS ShopName, o.OrderNumber, o.UserId, o.Email, o.CustomerName, o.Phone,
        o.Status, o.PaymentMethod, o.CollectionStatus, o.CodAmount, o.Subtotal, o.ShippingTotal, o.TaxTotal,
        o.DiscountTotal, o.GrandTotal, o.RefundedTotal, o.CouponCode, o.CouponId, o.ShippingName, o.ShippingLine1,
        o.ShippingLine2, o.ShippingCity, o.ShippingState, o.ShippingPostalCode, o.ShippingCountry, o.CustomerNote,
        o.PlacedUtc, o.PaidUtc, o.ShippedUtc, o.DeliveredUtc, o.CancelledUtc, o.TrackingNumber, o.Carrier,
        o.CarrierShipmentId, o.LabelUrl, o.CarrierError
    FROM dbo.Orders AS o
    INNER JOIN dbo.Shops AS s ON s.Id = o.ShopId
    WHERE o.Id = @OrderId;

    IF @@ROWCOUNT = 0 THROW 52003, N'Order not found.', 1;

    SELECT ol.Id, ol.OrderId, ol.ProductVariantId, ol.ProductId, ol.ProductName, ol.VariantName, ol.Sku,
           ol.ImageUrl, ol.Quantity, ol.UnitPrice, ol.LineTotal
    FROM dbo.OrderLines AS ol
    WHERE ol.OrderId = @OrderId
    ORDER BY ol.Id;

    SELECT p.Id, p.OrderId, p.Provider, p.Status, p.Amount, p.Currency, p.TransactionId AS ProviderReference,
           p.CreatedUtc, p.CompletedUtc AS CapturedUtc
    FROM dbo.Payments AS p
    WHERE p.OrderId = @OrderId
    ORDER BY p.Id;
END;
GO

IF OBJECT_ID(N'dbo.usp_Order_UpdateStatus', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Order_UpdateStatus AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Order_UpdateStatus
    @OrderId INT,
    @Status INT,
    @TrackingNumber NVARCHAR(64) = NULL,
    @Carrier NVARCHAR(40) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE Id = @OrderId)
        THROW 52003, N'Order not found.', 1;

    UPDATE dbo.Orders
       SET Status = @Status,
           TrackingNumber = COALESCE(@TrackingNumber, TrackingNumber),
           Carrier = COALESCE(@Carrier, Carrier),
           PaidUtc = CASE WHEN @Status = 1 AND PaidUtc IS NULL THEN SYSUTCDATETIME() ELSE PaidUtc END,
           ShippedUtc = CASE WHEN @Status = 2 AND ShippedUtc IS NULL THEN SYSUTCDATETIME() ELSE ShippedUtc END,
           DeliveredUtc = CASE WHEN @Status = 3 AND DeliveredUtc IS NULL THEN SYSUTCDATETIME() ELSE DeliveredUtc END,
           CancelledUtc = CASE WHEN @Status = 4 AND CancelledUtc IS NULL THEN SYSUTCDATETIME() ELSE CancelledUtc END
     WHERE Id = @OrderId;

    SELECT
        o.Id, o.ShopId, s.Name AS ShopName, o.OrderNumber, o.UserId, o.Email, o.CustomerName, o.Phone,
        o.Status, o.PaymentMethod, o.CollectionStatus, o.CodAmount, o.Subtotal, o.ShippingTotal, o.TaxTotal,
        o.DiscountTotal, o.GrandTotal, o.RefundedTotal, o.CouponCode, o.PlacedUtc, o.PaidUtc, o.ShippedUtc,
        o.DeliveredUtc, o.CancelledUtc, o.TrackingNumber, o.Carrier
    FROM dbo.Orders AS o
    INNER JOIN dbo.Shops AS s ON s.Id = o.ShopId
    WHERE o.Id = @OrderId;
END;
GO
/* ---------------------------------------------------------------------
   Customers (admin).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Customer_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Customer_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Customer_List
    @Query NVARCHAR(200) = NULL,
    @Page INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;
    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        u.Id AS UserId, u.Email, u.DisplayName, u.Phone, u.IsActive, u.CreatedUtc, u.LastLoginUtc,
        (SELECT COUNT(*) FROM dbo.Orders o WHERE o.UserId = u.Id) AS OrderCount,
        (SELECT ISNULL(SUM(o.GrandTotal), 0) FROM dbo.Orders o WHERE o.UserId = u.Id) AS TotalSpent
    FROM dbo.Users AS u
    WHERE (@Query IS NULL OR u.Email LIKE N'%' + @Query + N'%' OR u.DisplayName LIKE N'%' + @Query + N'%')
    ORDER BY u.CreatedUtc DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.Users AS u
    WHERE (@Query IS NULL OR u.Email LIKE N'%' + @Query + N'%' OR u.DisplayName LIKE N'%' + @Query + N'%');
END;
GO

IF OBJECT_ID(N'dbo.usp_Customer_Get', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Customer_Get AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Customer_Get
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.Id AS UserId, u.Email, u.DisplayName, u.Phone, u.IsActive, u.CreatedUtc, u.LastLoginUtc,
        (SELECT COUNT(*) FROM dbo.Orders o WHERE o.UserId = u.Id) AS OrderCount,
        (SELECT ISNULL(SUM(o.GrandTotal), 0) FROM dbo.Orders o WHERE o.UserId = u.Id) AS TotalSpent
    FROM dbo.Users AS u
    WHERE u.Id = @UserId;

    IF @@ROWCOUNT = 0 THROW 52003, N'Customer not found.', 1;
END;
GO
/* ---------------------------------------------------------------------
   Coupons (admin).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Coupon_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Coupon_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Coupon_List
    @Page INT = 1, @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;
    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        c.Id, c.Code, c.Description, c.Type, c.Value, c.MinSubtotal, c.MaxUses, c.UsedCount,
        c.StartsUtc, c.ExpiresUtc, c.IsActive, c.CreatedUtc
    FROM dbo.Coupons AS c
    ORDER BY c.CreatedUtc DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount FROM dbo.Coupons;
END;
GO

IF OBJECT_ID(N'dbo.usp_Coupon_Get', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Coupon_Get AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Coupon_Get
    @CouponId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.Id, c.Code, c.Description, c.Type, c.Value, c.MinSubtotal, c.MaxUses, c.UsedCount,
        c.StartsUtc, c.ExpiresUtc, c.IsActive, c.CreatedUtc
    FROM dbo.Coupons AS c
    WHERE c.Id = @CouponId;

    IF @@ROWCOUNT = 0 THROW 52003, N'Coupon not found.', 1;
END;
GO

IF OBJECT_ID(N'dbo.usp_Coupon_Create', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Coupon_Create AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Coupon_Create
    @Code NVARCHAR(64),
    @Description NVARCHAR(200) = NULL,
    @Type INT,
    @Value DECIMAL(18,2),
    @MinSubtotal DECIMAL(18,2) = NULL,
    @MaxUses INT = NULL,
    @StartsUtc DATETIME2 = NULL,
    @ExpiresUtc DATETIME2 = NULL,
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Coupons WHERE Code = @Code)
        THROW 52005, N'A coupon with this code already exists.', 1;

    INSERT INTO dbo.Coupons
        (Code, Description, Type, Value, MinSubtotal, MaxUses, UsedCount, StartsUtc, ExpiresUtc, IsActive, CreatedUtc)
    VALUES
        (@Code, @Description, @Type, @Value, @MinSubtotal, @MaxUses, 0, @StartsUtc, @ExpiresUtc, @IsActive, SYSUTCDATETIME());

    DECLARE @NewId INT = SCOPE_IDENTITY();

    SELECT c.Id, c.Code, c.Description, c.Type, c.Value, c.MinSubtotal, c.MaxUses, c.UsedCount,
           c.StartsUtc, c.ExpiresUtc, c.IsActive, c.CreatedUtc
    FROM dbo.Coupons AS c WHERE c.Id = @NewId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Coupon_Update', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Coupon_Update AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Coupon_Update
    @CouponId INT,
    @Code NVARCHAR(64),
    @Description NVARCHAR(200) = NULL,
    @Type INT,
    @Value DECIMAL(18,2),
    @MinSubtotal DECIMAL(18,2) = NULL,
    @MaxUses INT = NULL,
    @StartsUtc DATETIME2 = NULL,
    @ExpiresUtc DATETIME2 = NULL,
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Coupons WHERE Id = @CouponId)
        THROW 52003, N'Coupon not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Coupons WHERE Code = @Code AND Id <> @CouponId)
        THROW 52005, N'A coupon with this code already exists.', 1;

    UPDATE dbo.Coupons
       SET Code = @Code, Description = @Description, Type = @Type, Value = @Value,
           MinSubtotal = @MinSubtotal, MaxUses = @MaxUses, StartsUtc = @StartsUtc, ExpiresUtc = @ExpiresUtc,
           IsActive = @IsActive
     WHERE Id = @CouponId;

    SELECT c.Id, c.Code, c.Description, c.Type, c.Value, c.MinSubtotal, c.MaxUses, c.UsedCount,
           c.StartsUtc, c.ExpiresUtc, c.IsActive, c.CreatedUtc
    FROM dbo.Coupons AS c WHERE c.Id = @CouponId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Coupon_Delete', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Coupon_Delete AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Coupon_Delete
    @CouponId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Coupons WHERE Id = @CouponId)
        THROW 52003, N'Coupon not found.', 1;

    DELETE FROM dbo.Coupons WHERE Id = @CouponId;
END;
GO
/* ---------------------------------------------------------------------
   Reviews (admin).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Review_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Review_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Review_List
    @ProductId INT = NULL,
    @Status INT = NULL,
    @Page INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;
    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        r.Id, r.ProductId, p.Name AS ProductName, r.UserId, u.Email AS UserEmail,
        r.Rating, r.Title, r.Body, r.Status, r.CreatedUtc
    FROM dbo.Reviews AS r
    INNER JOIN dbo.Products AS p ON p.Id = r.ProductId
    LEFT JOIN dbo.Users AS u ON u.Id = r.UserId
    WHERE (@ProductId IS NULL OR r.ProductId = @ProductId)
      AND (@Status IS NULL OR r.Status = @Status)
    ORDER BY r.CreatedUtc DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.Reviews AS r
    WHERE (@ProductId IS NULL OR r.ProductId = @ProductId)
      AND (@Status IS NULL OR r.Status = @Status);
END;
GO

IF OBJECT_ID(N'dbo.usp_Review_SetStatus', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Review_SetStatus AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Review_SetStatus
    @ReviewId INT,
    @Status INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Reviews WHERE Id = @ReviewId)
        THROW 52003, N'Review not found.', 1;

    UPDATE dbo.Reviews SET Status = @Status WHERE Id = @ReviewId;
END;
GO

IF OBJECT_ID(N'dbo.usp_Review_Delete', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Review_Delete AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Review_Delete
    @ReviewId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Reviews WHERE Id = @ReviewId)
        THROW 52003, N'Review not found.', 1;

    DELETE FROM dbo.Reviews WHERE Id = @ReviewId;
END;
GO
/* ---------------------------------------------------------------------
   Returns (admin/vendor).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Return_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Return_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Return_List
    @ShopId INT = NULL,
    @UserId NVARCHAR(450) = NULL,
    @Status INT = NULL,
    @Page INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;
    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        rr.Id, rr.Reference, rr.OrderId, o.OrderNumber, rr.ShopId, s.Name AS ShopName,
        rr.UserId, rr.Status, rr.CustomerNote, rr.RequestedUtc, rr.DecidedUtc, rr.ReceivedUtc,
        rr.RefundAmount
    FROM dbo.ReturnRequests AS rr
    INNER JOIN dbo.Orders AS o ON o.Id = rr.OrderId
    INNER JOIN dbo.Shops AS s ON s.Id = rr.ShopId
    WHERE (@ShopId IS NULL OR rr.ShopId = @ShopId)
      AND (@UserId IS NULL OR rr.UserId = @UserId)
      AND (@Status IS NULL OR rr.Status = @Status)
    ORDER BY rr.RequestedUtc DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.ReturnRequests AS rr
    WHERE (@ShopId IS NULL OR rr.ShopId = @ShopId)
      AND (@UserId IS NULL OR rr.UserId = @UserId)
      AND (@Status IS NULL OR rr.Status = @Status);
END;
GO

IF OBJECT_ID(N'dbo.usp_Return_SetStatus', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Return_SetStatus AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Return_SetStatus
    @ReturnId INT,
    @Status INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.ReturnRequests WHERE Id = @ReturnId)
        THROW 52003, N'Return not found.', 1;

    UPDATE dbo.ReturnRequests SET Status = @Status WHERE Id = @ReturnId;
END;
GO
/* ---------------------------------------------------------------------
   Payouts (admin/vendor).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Payout_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Payout_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Payout_List
    @ShopId INT = NULL,
    @Page INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;
    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        p.Id, p.ShopId, s.Name AS ShopName, p.NetAmount AS Amount, p.Status, p.Reference, p.CreatedUtc, p.CreatedUtc AS PaidUtc
    FROM dbo.VendorPayouts AS p
    INNER JOIN dbo.Shops AS s ON s.Id = p.ShopId
    WHERE (@ShopId IS NULL OR p.ShopId = @ShopId)
    ORDER BY p.CreatedUtc DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.VendorPayouts AS p
    WHERE (@ShopId IS NULL OR p.ShopId = @ShopId);
END;
GO

IF OBJECT_ID(N'dbo.usp_Payout_SetStatus', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Payout_SetStatus AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Payout_SetStatus
    @PayoutId INT,
    @Status INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.VendorPayouts WHERE Id = @PayoutId)
        THROW 52003, N'Payout not found.', 1;

    UPDATE dbo.VendorPayouts
       SET Status = @Status
     WHERE Id = @PayoutId;
END;
GO
/* ---------------------------------------------------------------------
   Audit log (admin).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Audit_List', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Audit_List AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Audit_List
    @Actor NVARCHAR(256) = NULL,
    @Action NVARCHAR(80) = NULL,
    @EntityType NVARCHAR(40) = NULL,
    @Page INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 200 SET @PageSize = 50;
    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        a.Id, a.Actor, a.Action, a.EntityType, a.EntityId, a.Summary, a.CreatedUtc
    FROM dbo.AuditLogs AS a
    WHERE (@Actor IS NULL OR a.Actor = @Actor)
      AND (@Action IS NULL OR a.Action = @Action)
      AND (@EntityType IS NULL OR a.EntityType = @EntityType)
    ORDER BY a.CreatedUtc DESC, a.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.AuditLogs AS a
    WHERE (@Actor IS NULL OR a.Actor = @Actor)
      AND (@Action IS NULL OR a.Action = @Action)
      AND (@EntityType IS NULL OR a.EntityType = @EntityType);
END;
GO
/* ---------------------------------------------------------------------
   Reviews: create (customer).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.usp_Review_Create', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Review_Create AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Review_Create
    @ProductId INT,
    @UserId NVARCHAR(450) = NULL,
    @ReviewerName NVARCHAR(120),
    @Rating INT,
    @Title NVARCHAR(140) = NULL,
    @Body NVARCHAR(4000) = NULL,
    @IsVerifiedPurchase BIT = 0,
    @Status INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @Rating < 1 OR @Rating > 5
        THROW 52001, N'Rating must be between 1 and 5.', 1;

    DECLARE @ShopId INT = (SELECT ShopId FROM dbo.Products WHERE Id = @ProductId AND IsDeleted = 0);
    IF @ShopId IS NULL
        THROW 52003, N'Product not found.', 1;

    INSERT INTO dbo.Reviews
        (ProductId, ShopId, UserId, ReviewerName, Rating, Title, Body, IsVerifiedPurchase, Status, CreatedUtc, PublishedUtc)
    VALUES
        (@ProductId, @ShopId, @UserId, @ReviewerName, @Rating, @Title, @Body, @IsVerifiedPurchase, @Status, SYSUTCDATETIME(), SYSUTCDATETIME());

    DECLARE @NewId INT = SCOPE_IDENTITY();

    SELECT r.Id, r.ProductId, p.Name AS ProductName, r.UserId, u.Email AS UserEmail,
           r.Rating, r.Title, r.Body, r.Status, r.CreatedUtc
    FROM dbo.Reviews AS r
    INNER JOIN dbo.Products AS p ON p.Id = r.ProductId
    LEFT JOIN dbo.Users AS u ON u.Id = r.UserId
    WHERE r.Id = @NewId;
END;
GO
