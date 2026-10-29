/* =====================================================================
   Kwikby: stored procedures (SQL Server 2016 SP1 or later)
   File:       Database/02_StoredProcedures.sql
   Depends on: the EF-generated schema (01_Schema.sql, see Database/README.md)

   Scope: read paths (search, product detail, dashboards) and atomic stock
   adjustments. Order placement, payment and ledger logic stays in the
   C# services, which are unit-tested; duplicating it in T-SQL would let the
   two drift apart.

   Enum values used here (from Domain/Enums.cs):
     ListingStatus: Published = 0, Draft = 1, PendingReview = 2, Rejected = 3
     ReviewStatus:  Pending = 0, Published = 1
     OrderStatus:   Pending = 0, Paid = 1, Shipped = 2, Delivered = 3,
                    Cancelled = 4, Refunded = 5
     ShopStatus:    PendingApproval = 0, Approved = 1, Rejected = 2,
                    Suspended = 3, Closed = 4
     StockMovementReason: InitialStock = 0, Restock = 1, Sale = 2,
                    Return = 3, Adjustment = 4, Cancellation = 5
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---------- Search and browse: one page of product cards -------------- */
CREATE OR ALTER PROCEDURE dbo.usp_SearchProducts
    @Query       NVARCHAR(200) = NULL,
    @CategoryId  INT           = NULL,
    @ShopId      INT           = NULL,
    @MinPrice    DECIMAL(18,2) = NULL,
    @MaxPrice    DECIMAL(18,2) = NULL,
    @InStockOnly BIT           = 0,
    @SortBy      VARCHAR(20)   = 'relevance',   -- relevance | price_asc | price_desc | rating | newest
    @Page        INT           = 1,
    @PageSize    INT           = 24
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 24;

    ;WITH Base AS (
        SELECT
            p.Id          AS ProductId,
            p.Name,
            p.Slug,
            p.Brand,
            p.IsFeatured,
            p.CreatedUtc,
            p.CategoryId,
            p.ShopId,
            s.Name        AS ShopName,
            s.Slug        AS ShopSlug,
            pr.MinPrice,
            st.TotalStock,
            rv.AvgRating,
            rv.ReviewCount
        FROM dbo.Products AS p
        INNER JOIN dbo.Shops AS s ON s.Id = p.ShopId AND s.Status = 1
        CROSS APPLY (
            SELECT MIN(v.Price) AS MinPrice
            FROM dbo.ProductVariants AS v
            WHERE v.ProductId = p.Id AND v.IsActive = 1
        ) AS pr
        CROSS APPLY (
            SELECT ISNULL(SUM(v.StockQuantity), 0) AS TotalStock
            FROM dbo.ProductVariants AS v
            WHERE v.ProductId = p.Id AND v.IsActive = 1
        ) AS st
        CROSS APPLY (
            SELECT CAST(AVG(CAST(r.Rating AS DECIMAL(5,2))) AS DECIMAL(5,2)) AS AvgRating,
                   COUNT(*) AS ReviewCount
            FROM dbo.Reviews AS r
            WHERE r.ProductId = p.Id AND r.Status = 1
        ) AS rv
        WHERE p.IsDeleted = 0
          AND p.IsPublished = 1
          AND p.ModerationStatus = 0                     -- Published
          AND pr.MinPrice IS NOT NULL                    -- at least one active variant
          AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
          AND (@ShopId     IS NULL OR p.ShopId     = @ShopId)
          AND (@MinPrice   IS NULL OR pr.MinPrice >= @MinPrice)
          AND (@MaxPrice   IS NULL OR pr.MinPrice <= @MaxPrice)
          AND (@InStockOnly = 0 OR st.TotalStock > 0)
          AND (@Query IS NULL
               OR p.Name  LIKE N'%' + @Query + N'%'
               OR p.Brand LIKE N'%' + @Query + N'%'
               OR p.ShortDescription LIKE N'%' + @Query + N'%')
    )
    SELECT
        COUNT(*) OVER () AS TotalCount,
        ProductId, Name, Slug, Brand, IsFeatured, CategoryId,
        ShopId, ShopName, ShopSlug, MinPrice, TotalStock, AvgRating, ReviewCount
    FROM Base
    ORDER BY
        CASE WHEN @SortBy = 'price_asc'  THEN MinPrice   END ASC,
        CASE WHEN @SortBy = 'price_desc' THEN MinPrice   END DESC,
        CASE WHEN @SortBy = 'rating'     THEN AvgRating  END DESC,
        CASE WHEN @SortBy = 'newest'     THEN CreatedUtc END DESC,
        IsFeatured DESC,
        ProductId
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO

/* ---------- Product page: product, variants, images, rating ----------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetProductDetail
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: the product and its shop
    SELECT p.Id AS ProductId, p.Name, p.Slug, p.Brand, p.ShortDescription, p.Description,
           p.CategoryId, p.ShopId, s.Name AS ShopName, s.Slug AS ShopSlug
    FROM dbo.Products AS p
    INNER JOIN dbo.Shops AS s ON s.Id = p.ShopId AND s.Status = 1
    WHERE p.Id = @ProductId AND p.IsDeleted = 0 AND p.IsPublished = 1 AND p.ModerationStatus = 0;

    -- Result set 2: active variants
    SELECT v.Id AS ProductVariantId, v.Sku, v.Name, v.Price, v.CompareAtPrice,
           v.StockQuantity, v.LowStockThreshold
    FROM dbo.ProductVariants AS v
    INNER JOIN dbo.Products AS p ON p.Id = v.ProductId
    WHERE v.ProductId = @ProductId AND v.IsActive = 1 AND p.ModerationStatus = 0
    ORDER BY v.Price, v.Id;

    -- Result set 3: images in display order
    SELECT i.Id AS ProductImageId, i.Url, i.AltText, i.DisplayOrder, i.IsPrimary
    FROM dbo.ProductImages AS i
    WHERE i.ProductId = @ProductId
    ORDER BY i.IsPrimary DESC, i.DisplayOrder, i.Id;

    -- Result set 4: published reviews summary
    SELECT COUNT(*) AS ReviewCount,
           CAST(AVG(CAST(r.Rating AS DECIMAL(5,2))) AS DECIMAL(5,2)) AS AvgRating
    FROM dbo.Reviews AS r
    WHERE r.ProductId = @ProductId AND r.Status = 1;
END;
GO

/* ---------- Shop dashboard for the vendor workspace ------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetShopDashboard
    @ShopId INT,
    @Days   INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Since DATETIME2 = DATEADD(DAY, -@Days, SYSUTCDATETIME());

    -- Result set 1: headline figures
    SELECT
        (SELECT COUNT(*) FROM dbo.Orders AS o
          WHERE o.ShopId = @ShopId AND o.PlacedUtc >= @Since)                                   AS OrdersPlaced,
        (SELECT COUNT(*) FROM dbo.Orders AS o
          WHERE o.ShopId = @ShopId AND o.PlacedUtc >= @Since AND o.Status IN (1,2,3))           AS OrdersPaidOrLater,
        (SELECT COUNT(*) FROM dbo.Orders AS o
          WHERE o.ShopId = @ShopId AND o.PlacedUtc >= @Since AND o.Status = 4)                  AS OrdersCancelled,
        (SELECT ISNULL(SUM(o.GrandTotal), 0) FROM dbo.Orders AS o
          WHERE o.ShopId = @ShopId AND o.PlacedUtc >= @Since AND o.Status IN (1,2,3))           AS Revenue,
        (SELECT COUNT(*) FROM dbo.ProductVariants AS v
            INNER JOIN dbo.Products AS p ON p.Id = v.ProductId
          WHERE p.ShopId = @ShopId AND v.IsActive = 1 AND v.StockQuantity <= v.LowStockThreshold) AS LowStockVariants,
        (SELECT COUNT(*) FROM dbo.Products AS p
          WHERE p.ShopId = @ShopId AND p.IsDeleted = 0 AND p.ModerationStatus = 2)              AS ListingsAwaitingReview;

    -- Result set 2: top products by units sold (paid or later)
    SELECT TOP (5)
        ol.ProductName,
        SUM(ol.Quantity)  AS UnitsSold,
        SUM(ol.LineTotal) AS Sales
    FROM dbo.OrderLines AS ol
    INNER JOIN dbo.Orders AS o ON o.Id = ol.OrderId
    WHERE o.ShopId = @ShopId AND o.PlacedUtc >= @Since AND o.Status IN (1,2,3)
    GROUP BY ol.ProductName
    ORDER BY SUM(ol.Quantity) DESC, ol.ProductName;
END;
GO

/* ---------- Platform dashboard for administrators --------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetPlatformDashboard
    @Days INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Since DATETIME2 = DATEADD(DAY, -@Days, SYSUTCDATETIME());

    SELECT
        (SELECT ISNULL(SUM(o.GrandTotal), 0) FROM dbo.Orders AS o
          WHERE o.PlacedUtc >= @Since AND o.Status IN (1,2,3))                       AS Gmv,
        (SELECT COUNT(*) FROM dbo.Orders AS o WHERE o.PlacedUtc >= @Since)           AS OrdersPlaced,
        (SELECT COUNT(*) FROM dbo.Shops AS s WHERE s.Status = 1)                     AS ActiveShops,
        (SELECT COUNT(*) FROM dbo.Shops AS s WHERE s.Status = 0)                     AS ShopsAwaitingApproval,
        (SELECT COUNT(*) FROM dbo.Products AS p
          WHERE p.IsDeleted = 0 AND p.ModerationStatus = 2)                          AS ListingsAwaitingReview,
        (SELECT COUNT(*) FROM dbo.Reviews AS r WHERE r.Status = 0)                   AS ReviewsAwaitingModeration;
END;
GO

/* ---------- Atomic stock adjustment with an audit trail --------------- */
CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
    @ProductVariantId INT,
    @QuantityDelta    INT,
    @Reason           INT,                 -- StockMovementReason
    @Note             NVARCHAR(500) = NULL,
    @OrderId          INT = NULL,
    @CreatedBy        NVARCHAR(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @QuantityDelta = 0
        THROW 52001, N'The quantity change must not be zero.', 1;
    IF @Reason NOT BETWEEN 0 AND 5
        THROW 52002, N'Unknown stock movement reason.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Current INT;
        SELECT @Current = v.StockQuantity
        FROM dbo.ProductVariants AS v WITH (UPDLOCK, ROWLOCK)
        WHERE v.Id = @ProductVariantId;

        IF @Current IS NULL
            THROW 52003, N'Product variant not found.', 1;
        IF @Current + @QuantityDelta < 0
            THROW 52004, N'Not enough stock for this change.', 1;

        UPDATE dbo.ProductVariants
        SET StockQuantity = @Current + @QuantityDelta
        WHERE Id = @ProductVariantId;

        INSERT INTO dbo.StockMovements
            (ProductVariantId, QuantityDelta, StockAfter, Reason, Note, OrderId, CreatedBy, CreatedUtc)
        VALUES
            (@ProductVariantId, @QuantityDelta, @Current + @QuantityDelta, @Reason, @Note, @OrderId,
             @CreatedBy, SYSUTCDATETIME());

        COMMIT TRANSACTION;
        SELECT @Current + @QuantityDelta AS StockAfter;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
