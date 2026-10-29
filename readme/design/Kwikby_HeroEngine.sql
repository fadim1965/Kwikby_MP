/* =====================================================================
   Kwikby: hero banner engine for SQL Server
   File:      Kwikby_HeroEngine.sql
   Requires:  SQL Server 2016 SP1 or later (CREATE OR ALTER).
   Depends on: dbo.Shops (Id int, Status int, CreatedUtc datetime2)
               dbo.Categories (Id int)
               Shop status 1 = Approved (see Domain/Shop.cs).
   Run in SSMS against the KwikbyMP database. Safe to re-run.

   Contents
     Tables:      PlatformSettings, VendorBanners, HeroSlots,
                  CampaignBookings, HeroImpressionStaging
     Indexes:     filtered and covering indexes for the hot paths
     View:        vw_EarnedShopCandidates (performance score is a placeholder)
     Procedures:  usp_CreateCampaignBooking, usp_ReviewCampaignBooking,
                  usp_GetHeroSlots, usp_RecordHeroImpression,
                  usp_FlushHeroImpressions

   Billing terms
     DailyRate: Price = total fee for the StartAt..EndAt window.
     CPM:       Price = fee per 1,000 impressions; ImpressionCap is required.
     Platform:  Kwikby's own banner, no shop, no fee.
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---------- 1. Platform settings (exactly one row) -------------------- */
IF OBJECT_ID(N'dbo.PlatformSettings', N'U') IS NULL
CREATE TABLE dbo.PlatformSettings (
    SettingID             TINYINT      NOT NULL CONSTRAINT PK_PlatformSettings PRIMARY KEY,
    SponsoredBillingModel VARCHAR(10)  NOT NULL CONSTRAINT DF_PlatformSettings_Model DEFAULT 'DailyRate',
    UpdatedUtc            DATETIME2(0) NOT NULL CONSTRAINT DF_PlatformSettings_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_PlatformSettings_Singleton CHECK (SettingID = 1),
    CONSTRAINT CK_PlatformSettings_Model CHECK (SponsoredBillingModel IN ('DailyRate', 'CPM'))
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PlatformSettings WHERE SettingID = 1)
    INSERT INTO dbo.PlatformSettings (SettingID, SponsoredBillingModel) VALUES (1, 'DailyRate');
GO

/* ---------- 2. Banner creatives ---------------------------------------- */
IF OBJECT_ID(N'dbo.VendorBanners', N'U') IS NULL
CREATE TABLE dbo.VendorBanners (
    BannerID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VendorBanners PRIMARY KEY,
    ShopID          INT NULL CONSTRAINT FK_VendorBanners_Shops REFERENCES dbo.Shops (Id),  -- NULL = Kwikby banner
    BannerName      NVARCHAR(100) NOT NULL,
    DesktopImageUrl VARCHAR(512)  NOT NULL,
    MobileImageUrl  VARCHAR(512)  NOT NULL,
    TargetUrl       VARCHAR(512)  NOT NULL,
    CategoryID      INT NULL CONSTRAINT FK_VendorBanners_Categories REFERENCES dbo.Categories (Id), -- NULL = homepage-eligible
    IsPerkSlot      BIT NOT NULL CONSTRAINT DF_VendorBanners_Perk DEFAULT 0,      -- 1 = earned (free) placement
    IsApproved      BIT NOT NULL CONSTRAINT DF_VendorBanners_Approved DEFAULT 0,  -- set only by moderation
    CreatedUtc      DATETIME2(0) NOT NULL CONSTRAINT DF_VendorBanners_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_VendorBanners_Desktop CHECK (DesktopImageUrl LIKE 'https://%'),
    CONSTRAINT CK_VendorBanners_Mobile  CHECK (MobileImageUrl  LIKE 'https://%'),
    CONSTRAINT CK_VendorBanners_Target  CHECK (TargetUrl LIKE 'https://%' OR TargetUrl LIKE '/%')
);
GO

/* ---------- 3. Slot layout, per page (data, not code) ------------------ */
IF OBJECT_ID(N'dbo.HeroSlots', N'U') IS NULL
CREATE TABLE dbo.HeroSlots (
    SlotID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HeroSlots PRIMARY KEY,
    PageScope     VARCHAR(10) NOT NULL,
    CategoryID    INT NULL CONSTRAINT FK_HeroSlots_Categories REFERENCES dbo.Categories (Id), -- NULL on Home
    Position      TINYINT NOT NULL,
    SlotType      VARCHAR(10) NOT NULL,
    PreferNewShop BIT NOT NULL CONSTRAINT DF_HeroSlots_PreferNew DEFAULT 0,  -- reserves a slot for shops under 90 days
    CONSTRAINT CK_HeroSlots_Scope CHECK (PageScope IN ('Home', 'Category')),
    CONSTRAINT CK_HeroSlots_Pos   CHECK (Position BETWEEN 1 AND 4),
    CONSTRAINT CK_HeroSlots_Type  CHECK (SlotType IN ('Platform', 'Sponsored', 'Earned')),
    CONSTRAINT CK_HeroSlots_Scope_Category CHECK ((PageScope = 'Home') = (CategoryID IS NULL)),
    CONSTRAINT UQ_HeroSlots UNIQUE (PageScope, CategoryID, Position)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.HeroSlots WHERE PageScope = 'Home')
BEGIN
    INSERT INTO dbo.HeroSlots (PageScope, CategoryID, Position, SlotType, PreferNewShop) VALUES
        ('Home', NULL, 1, 'Platform',  0),
        ('Home', NULL, 2, 'Sponsored', 0),
        ('Home', NULL, 3, 'Earned',    1),
        ('Home', NULL, 4, 'Earned',    0);
END
GO

/* ---------- 4. Bookings: what was sold, at what price, and its status -- */
IF OBJECT_ID(N'dbo.CampaignBookings', N'U') IS NULL
CREATE TABLE dbo.CampaignBookings (
    BookingID         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CampaignBookings PRIMARY KEY,
    BannerID          INT NOT NULL CONSTRAINT FK_CampaignBookings_Banners REFERENCES dbo.VendorBanners (BannerID),
    ShopID            INT NULL CONSTRAINT FK_CampaignBookings_Shops REFERENCES dbo.Shops (Id),
    CategoryID        INT NULL CONSTRAINT FK_CampaignBookings_Categories REFERENCES dbo.Categories (Id),
    BillingModel      VARCHAR(10)   NOT NULL,
    Price             DECIMAL(12,2) NOT NULL CONSTRAINT DF_CampaignBookings_Price DEFAULT 0,
    RankValue         DECIMAL(12,4) NOT NULL CONSTRAINT DF_CampaignBookings_Rank DEFAULT 0, -- per-day value or per-1,000 value, for ranking
    ImpressionCap     INT NULL,
    ImpressionsServed INT NOT NULL CONSTRAINT DF_CampaignBookings_Served DEFAULT 0,
    StartAt           DATETIME2(0) NOT NULL,
    EndAt             DATETIME2(0) NOT NULL,
    Status            VARCHAR(12)  NOT NULL CONSTRAINT DF_CampaignBookings_Status DEFAULT 'Pending',
    PaymentRef        NVARCHAR(100) NULL,
    CreatedUtc        DATETIME2(0) NOT NULL CONSTRAINT DF_CampaignBookings_Created DEFAULT SYSUTCDATETIME(),
    ReviewedBy        NVARCHAR(256) NULL,
    ReviewedUtc       DATETIME2(0) NULL,
    CONSTRAINT CK_CampaignBookings_Model  CHECK (BillingModel IN ('DailyRate', 'CPM', 'Platform')),
    CONSTRAINT CK_CampaignBookings_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected', 'Cancelled', 'Ended')),
    CONSTRAINT CK_CampaignBookings_Dates  CHECK (EndAt > StartAt),
    CONSTRAINT CK_CampaignBookings_Cap    CHECK (BillingModel <> 'CPM' OR (ImpressionCap IS NOT NULL AND ImpressionCap > 0)),
    CONSTRAINT CK_CampaignBookings_Shop   CHECK ((BillingModel = 'Platform') = (ShopID IS NULL)),
    CONSTRAINT CK_CampaignBookings_Money  CHECK (Price >= 0 AND ImpressionsServed >= 0)
);
GO

/* ---------- 5. Impression staging: written per view, flushed in batches -- */
IF OBJECT_ID(N'dbo.HeroImpressionStaging', N'U') IS NULL
CREATE TABLE dbo.HeroImpressionStaging (
    ImpressionID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HeroImpressionStaging PRIMARY KEY,
    BookingID    INT NOT NULL CONSTRAINT FK_HeroImpressionStaging_Bookings REFERENCES dbo.CampaignBookings (BookingID),
    ViewedUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_HeroImpressionStaging_Viewed DEFAULT SYSUTCDATETIME()
);
GO

/* ---------- 6. Indexes for the hot paths ------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CampaignBookings_Live' AND object_id = OBJECT_ID(N'dbo.CampaignBookings'))
    CREATE NONCLUSTERED INDEX IX_CampaignBookings_Live
        ON dbo.CampaignBookings (CategoryID, StartAt, EndAt)
        INCLUDE (BannerID, ShopID, BillingModel, RankValue, Price, ImpressionCap, ImpressionsServed)
        WHERE Status = 'Approved';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CampaignBookings_Shop' AND object_id = OBJECT_ID(N'dbo.CampaignBookings'))
    CREATE NONCLUSTERED INDEX IX_CampaignBookings_Shop
        ON dbo.CampaignBookings (ShopID, Status, StartAt, EndAt);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VendorBanners_Category' AND object_id = OBJECT_ID(N'dbo.VendorBanners'))
    CREATE NONCLUSTERED INDEX IX_VendorBanners_Category
        ON dbo.VendorBanners (CategoryID, IsPerkSlot, IsApproved)
        INCLUDE (ShopID, DesktopImageUrl, MobileImageUrl, TargetUrl);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HeroImpressionStaging_Viewed' AND object_id = OBJECT_ID(N'dbo.HeroImpressionStaging'))
    CREATE NONCLUSTERED INDEX IX_HeroImpressionStaging_Viewed
        ON dbo.HeroImpressionStaging (ViewedUtc)
        INCLUDE (BookingID);
GO

/* ---------- 7. Earned-shop candidates ---------------------------------- */
/* PerformanceScore is a placeholder (0). Replace it with a capped formula
   from ratings, return rate and sales before the earned slots are used. */
CREATE OR ALTER VIEW dbo.vw_EarnedShopCandidates
AS
SELECT
    s.Id AS ShopID,
    CASE WHEN s.CreatedUtc >= DATEADD(DAY, -90, SYSUTCDATETIME()) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsNewShop,
    CAST(0 AS DECIMAL(9,4)) AS PerformanceScore
FROM dbo.Shops AS s
WHERE s.Status = 1;   -- Approved only: suspended, closed or pending shops never appear
GO

/* ---------- 8. Vendor requests a booking (Pending until reviewed) ------ */
CREATE OR ALTER PROCEDURE dbo.usp_CreateCampaignBooking
    @ShopID        INT,
    @BannerID      INT,
    @CategoryID    INT = NULL,
    @StartAt       DATETIME2(0),
    @EndAt         DATETIME2(0),
    @Price         DECIMAL(12,2),
    @ImpressionCap INT = NULL,
    @PaymentRef    NVARCHAR(100) = NULL,
    @BookingID     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Model VARCHAR(10) = (SELECT SponsoredBillingModel FROM dbo.PlatformSettings WHERE SettingID = 1);
    DECLARE @Days DECIMAL(12,4);
    DECLARE @Rank DECIMAL(12,4);

    IF @EndAt <= @StartAt
        THROW 50001, N'EndAt must be after StartAt.', 1;
    IF @Model = 'CPM' AND (@ImpressionCap IS NULL OR @ImpressionCap <= 0)
        THROW 50002, N'CPM bookings need a positive impression cap.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.VendorBanners WHERE BannerID = @BannerID AND ShopID = @ShopID AND IsApproved = 1)
        THROW 50003, N'The banner must belong to this shop and be approved.', 1;

    IF @Model = 'DailyRate'
    BEGIN
        SET @ImpressionCap = NULL;
        SET @Days = CAST(DATEDIFF(MINUTE, @StartAt, @EndAt) AS DECIMAL(12,4)) / 1440.0;
        SET @Rank = @Price / NULLIF(@Days, 0);      -- value per day
    END
    ELSE
        SET @Rank = @Price;                          -- value per 1,000 impressions

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Serialise bookings per shop so two requests cannot both pass the spacing check.
        DECLARE @Locked INT;
        SELECT @Locked = Id FROM dbo.Shops WITH (UPDLOCK, HOLDLOCK) WHERE Id = @ShopID;
        IF @Locked IS NULL
            THROW 50004, N'Shop not found.', 1;

        -- One spotlight per shop: no other pending or approved booking within 14 days of these dates.
        IF EXISTS (SELECT 1 FROM dbo.CampaignBookings
                   WHERE ShopID = @ShopID
                     AND Status IN ('Pending', 'Approved')
                     AND StartAt < DATEADD(DAY, 14, @EndAt)
                     AND EndAt   > DATEADD(DAY, -14, @StartAt))
            THROW 50005, N'This shop already has a spotlight within 14 days of these dates.', 1;

        INSERT INTO dbo.CampaignBookings
            (BannerID, ShopID, CategoryID, BillingModel, Price, RankValue, ImpressionCap,
             StartAt, EndAt, Status, PaymentRef)
        VALUES
            (@BannerID, @ShopID, @CategoryID, @Model, @Price, @Rank, @ImpressionCap,
             @StartAt, @EndAt, 'Pending', @PaymentRef);

        SET @BookingID = SCOPE_IDENTITY();
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ---------- 9. Admin approves or rejects a pending booking ------------- */
CREATE OR ALTER PROCEDURE dbo.usp_ReviewCampaignBooking
    @BookingID  INT,
    @Approve    BIT,
    @ReviewedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.CampaignBookings
    SET Status      = CASE WHEN @Approve = 1 THEN 'Approved' ELSE 'Rejected' END,
        ReviewedBy  = @ReviewedBy,
        ReviewedUtc = SYSUTCDATETIME()
    WHERE BookingID = @BookingID AND Status = 'Pending';

    IF @@ROWCOUNT = 0
        THROW 50006, N'Booking not found or not pending.', 1;
END;
GO

/* ---------- 10. Delivery: the four slots for one page ------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetHeroSlots
    @CategoryID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now     DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @Scope   VARCHAR(10)  = CASE WHEN @CategoryID IS NULL THEN 'Home' ELSE 'Category' END;
    DECLARE @Model   VARCHAR(10)  = (SELECT SponsoredBillingModel FROM dbo.PlatformSettings WHERE SettingID = 1);
    DECLARE @Pos     TINYINT = 1;
    DECLARE @Type    VARCHAR(10);
    DECLARE @PreferNew BIT;

    CREATE TABLE #Slots (
        Position        TINYINT      NOT NULL PRIMARY KEY,
        SlotType        VARCHAR(10)  NOT NULL,
        BookingID       INT          NULL,
        BannerID        INT          NULL,
        ShopID          INT          NULL,
        DesktopImageUrl VARCHAR(512) NULL,
        MobileImageUrl  VARCHAR(512) NULL,
        TargetUrl       VARCHAR(512) NULL
    );
    CREATE TABLE #UsedShops (ShopID INT NOT NULL PRIMARY KEY);

    WHILE @Pos <= 4
    BEGIN
        SET @Type = NULL;
        SET @PreferNew = 0;

        SELECT @Type = hs.SlotType, @PreferNew = hs.PreferNewShop
        FROM dbo.HeroSlots AS hs
        WHERE hs.PageScope = @Scope AND hs.Position = @Pos
          AND ((hs.CategoryID = @CategoryID) OR (@CategoryID IS NULL AND hs.CategoryID IS NULL));

        IF @Type = 'Platform'
        BEGIN
            INSERT INTO #Slots (Position, SlotType, BookingID, BannerID, ShopID, DesktopImageUrl, MobileImageUrl, TargetUrl)
            SELECT TOP (1) @Pos, 'Platform', b.BookingID, vb.BannerID, NULL,
                   vb.DesktopImageUrl, vb.MobileImageUrl, vb.TargetUrl
            FROM dbo.CampaignBookings AS b
            INNER JOIN dbo.VendorBanners AS vb ON vb.BannerID = b.BannerID
            WHERE b.BillingModel = 'Platform' AND b.Status = 'Approved'
              AND @Now BETWEEN b.StartAt AND b.EndAt
              AND vb.IsApproved = 1 AND vb.ShopID IS NULL
              AND ((b.CategoryID = @CategoryID) OR (@CategoryID IS NULL AND b.CategoryID IS NULL))
            ORDER BY b.BookingID DESC;
        END
        ELSE IF @Type = 'Sponsored'
        BEGIN
            -- Current-model bookings first, then highest value per unit, then random among equals.
            INSERT INTO #Slots (Position, SlotType, BookingID, BannerID, ShopID, DesktopImageUrl, MobileImageUrl, TargetUrl)
            SELECT TOP (1) @Pos, 'Sponsored', b.BookingID, vb.BannerID, b.ShopID,
                   vb.DesktopImageUrl, vb.MobileImageUrl, vb.TargetUrl
            FROM dbo.CampaignBookings AS b
            INNER JOIN dbo.VendorBanners AS vb ON vb.BannerID = b.BannerID
            INNER JOIN dbo.Shops AS s ON s.Id = b.ShopID
            WHERE b.BillingModel IN ('DailyRate', 'CPM') AND b.Status = 'Approved'
              AND @Now BETWEEN b.StartAt AND b.EndAt
              AND (b.ImpressionCap IS NULL OR b.ImpressionsServed < b.ImpressionCap)
              AND vb.IsApproved = 1 AND s.Status = 1
              AND NOT EXISTS (SELECT 1 FROM #UsedShops AS u WHERE u.ShopID = b.ShopID)
              AND ((b.CategoryID = @CategoryID) OR (@CategoryID IS NULL AND b.CategoryID IS NULL))
            ORDER BY CASE WHEN b.BillingModel = @Model THEN 0 ELSE 1 END,
                     b.RankValue DESC,
                     NEWID();
        END
        ELSE IF @Type = 'Earned'
        BEGIN
            -- Reserved new-shop slot first when PreferNewShop is set, then capped performance score.
            INSERT INTO #Slots (Position, SlotType, BookingID, BannerID, ShopID, DesktopImageUrl, MobileImageUrl, TargetUrl)
            SELECT TOP (1) @Pos, 'Earned', NULL, vb.BannerID, vb.ShopID,
                   vb.DesktopImageUrl, vb.MobileImageUrl, vb.TargetUrl
            FROM dbo.VendorBanners AS vb
            INNER JOIN dbo.vw_EarnedShopCandidates AS c ON c.ShopID = vb.ShopID
            WHERE vb.IsPerkSlot = 1 AND vb.IsApproved = 1
              AND NOT EXISTS (SELECT 1 FROM #UsedShops AS u WHERE u.ShopID = vb.ShopID)
              AND ((vb.CategoryID = @CategoryID) OR (@CategoryID IS NULL AND vb.CategoryID IS NULL))
            ORDER BY CASE WHEN @PreferNew = 1 AND c.IsNewShop = 1 THEN 0 ELSE 1 END,
                     c.PerformanceScore DESC,
                     NEWID();
        END

        -- One shop per page: record the shop so later slots skip it.
        INSERT INTO #UsedShops (ShopID)
        SELECT ShopID FROM #Slots WHERE Position = @Pos AND ShopID IS NOT NULL;

        SET @Pos = @Pos + 1;
    END

    -- Fallback: any slot still empty shows the most recent live Kwikby banner.
    DECLARE @FbBooking INT, @FbBanner INT, @FbDesk VARCHAR(512), @FbMob VARCHAR(512), @FbTarget VARCHAR(512);
    SELECT TOP (1)
        @FbBooking = b.BookingID, @FbBanner = vb.BannerID,
        @FbDesk = vb.DesktopImageUrl, @FbMob = vb.MobileImageUrl, @FbTarget = vb.TargetUrl
    FROM dbo.CampaignBookings AS b
    INNER JOIN dbo.VendorBanners AS vb ON vb.BannerID = b.BannerID
    WHERE b.BillingModel = 'Platform' AND b.Status = 'Approved'
      AND @Now BETWEEN b.StartAt AND b.EndAt AND vb.IsApproved = 1 AND vb.ShopID IS NULL
      AND ((b.CategoryID = @CategoryID) OR (@CategoryID IS NULL AND b.CategoryID IS NULL))
    ORDER BY b.BookingID DESC;

    IF @FbBanner IS NOT NULL
        INSERT INTO #Slots (Position, SlotType, BookingID, BannerID, ShopID, DesktopImageUrl, MobileImageUrl, TargetUrl)
        SELECT hs.Position, 'Platform', @FbBooking, @FbBanner, NULL, @FbDesk, @FbMob, @FbTarget
        FROM dbo.HeroSlots AS hs
        WHERE hs.PageScope = @Scope
          AND ((hs.CategoryID = @CategoryID) OR (@CategoryID IS NULL AND hs.CategoryID IS NULL))
          AND NOT EXISTS (SELECT 1 FROM #Slots AS s WHERE s.Position = hs.Position);

    SELECT Position, SlotType, BookingID, BannerID, ShopID, DesktopImageUrl, MobileImageUrl, TargetUrl
    FROM #Slots
    ORDER BY Position;
END;
GO

/* ---------- 11. Record one impression (called fire-and-forget) --------- */
CREATE OR ALTER PROCEDURE dbo.usp_RecordHeroImpression
    @BookingID INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.HeroImpressionStaging (BookingID) VALUES (@BookingID);
END;
GO

/* ---------- 12. Scheduled job (every 5 minutes): flush impressions ----- */
/* Rows newer than 30 seconds are left for the next run, so an in-flight
   insert is never counted half-way. Only the rows counted are deleted.
   READPAST lets the job skip rows another session is writing. */
CREATE OR ALTER PROCEDURE dbo.usp_FlushHeroImpressions
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Cutoff DATETIME2(3) = DATEADD(SECOND, -30, SYSUTCDATETIME());
    DECLARE @Now    DATETIME2(0) = SYSUTCDATETIME();

    CREATE TABLE #Batch (
        ImpressionID BIGINT NOT NULL PRIMARY KEY,
        BookingID    INT    NOT NULL
    );

    INSERT INTO #Batch (ImpressionID, BookingID)
    SELECT TOP (50000) s.ImpressionID, s.BookingID
    FROM dbo.HeroImpressionStaging AS s WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE s.ViewedUtc < @Cutoff
    ORDER BY s.ImpressionID;

    IF NOT EXISTS (SELECT 1 FROM #Batch)
    BEGIN
        -- Nothing to count, but still end bookings whose time has passed.
        UPDATE dbo.CampaignBookings
        SET Status = 'Ended'
        WHERE Status = 'Approved' AND EndAt < @Now;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE b
        SET b.ImpressionsServed = b.ImpressionsServed + x.Hits
        FROM dbo.CampaignBookings AS b
        INNER JOIN (SELECT BookingID, COUNT(*) AS Hits FROM #Batch GROUP BY BookingID) AS x
            ON x.BookingID = b.BookingID;

        DELETE s
        FROM dbo.HeroImpressionStaging AS s
        INNER JOIN #Batch AS t ON t.ImpressionID = s.ImpressionID;

        UPDATE dbo.CampaignBookings
        SET Status = 'Ended'
        WHERE Status = 'Approved'
          AND (EndAt < @Now OR (ImpressionCap IS NOT NULL AND ImpressionsServed >= ImpressionCap));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
