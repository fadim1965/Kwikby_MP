/* =====================================================================
   Kwikby: approved sample shops (idempotent)

   The script creates the three owner accounts if they do not exist. It copies
   the password hash of the administrator account (admin@libanpost.example),
   so the owners have the same password as the administrator. Use this for
   TEST databases only. Change the owners' passwords before going live.
   It requires the administrator account to exist (created by the demo seeder).

   Each shop is created Approved, and its owner is added to ShopUsers with the
   Owner role (ShopRole.Owner = 0). A shop whose Slug already exists is skipped.
   Enum values used (Domain/Shop.cs): ShopStatus.Approved = 1, ShopRole.Owner = 0.
   VerificationStatus is set to 0 (NotStarted). The C# property is not nullable, so NULL breaks loading.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

DECLARE @Shops TABLE (
    Name                     NVARCHAR(120) NOT NULL,
    Slug                     NVARCHAR(120) NOT NULL PRIMARY KEY,
    Description              NVARCHAR(600) NULL,
    Tagline                  NVARCHAR(160) NULL,
    OwnerEmail               NVARCHAR(256) NOT NULL,
    ContactName              NVARCHAR(120) NULL,
    Phone                    NVARCHAR(40)  NULL,
    Email                    NVARCHAR(256) NULL,
    AddressLine1             NVARCHAR(200) NULL,
    City                     NVARCHAR(100) NULL,
    PostalCode               NVARCHAR(40)  NULL,
    Country                  NVARCHAR(80)  NULL,
    PickupLocation           NVARCHAR(120) NULL,
    CommissionRate           DECIMAL(5,4)  NOT NULL,
    FlatShippingRate         DECIMAL(18,2) NULL,
    FreeShippingThreshold    DECIMAL(18,2) NULL,
    ThemeKey                 NVARCHAR(40)  NOT NULL
);

INSERT INTO @Shops
    (Name, Slug, Description, Tagline, OwnerEmail, ContactName, Phone, Email, AddressLine1, City, PostalCode,
     Country, PickupLocation, CommissionRate, FlatShippingRate, FreeShippingThreshold, ThemeKey)
VALUES
    (N'Northwind Home', N'northwind-home',
     N'Gifts, ceramics and linen for the home, made by small workshops.',
     N'Everyday pieces for the home.',
     N'owner.northwind@example.com', N'Northwind Home Owner', N'+961 1 000 001', N'hello@northwind.example',
     N'Jounieh Main Road 1', N'Jounieh', N'1200', N'Lebanon', N'Jounieh pickup point',
     0.0800, 4.50, 50.00, N'kwik-default'),
    (N'Lumen Audio', N'lumen-audio',
     N'Earphones, chargers, power banks and desk accessories.',
     N'Audio and charging, tested before dispatch.',
     N'owner.lumen@example.com', N'Lumen Audio Owner', N'+961 1 000 002', N'hello@lumen.example',
     N'Tripoli Coastal Road 12', N'Tripoli', N'1300', N'Lebanon', N'Tripoli pickup point',
     0.0900, 4.50, 70.00, N'kwik-default'),
    (N'Atlas Notebooks', N'atlas-notebooks',
     N'Notebooks, journals, planners and pens from a small Beirut studio.',
     N'Paper goods for daily use.',
     N'owner.atlas@example.com', N'Atlas Notebooks Owner', N'+961 1 000 003', N'hello@atlas.example',
     N'Hamra Street 45', N'Beirut', N'1100', N'Lebanon', N'Beirut pickup point',
     0.1000, 3.50, 40.00, N'kwik-default');

-- Stop if any owner account is missing.
-- Create the owner accounts that do not exist yet (test databases).
DECLARE @AdminHash NVARCHAR(400) =
    (SELECT TOP (1) PasswordHash FROM dbo.Users WHERE NormalizedEmail = UPPER(N'admin@libanpost.example'));

IF @AdminHash IS NULL
BEGIN
    RAISERROR(N'The administrator account admin@libanpost.example was not found. Run the demo seeder first.', 16, 1);
    RETURN;
END

INSERT INTO dbo.Users
    (Id, Email, NormalizedEmail, DisplayName, Phone, PasswordHash, SecurityStamp, IsActive,
     AccessFailedCount, LockoutEndUtc, CreatedUtc, LastLoginUtc, TwoFactorEnabled, TwoFactorSecret, TwoFactorRecoveryCodes)
SELECT REPLACE(NEWID(), N'-', N''), s.OwnerEmail, UPPER(s.OwnerEmail), s.ContactName, s.Phone, @AdminHash,
       REPLACE(NEWID(), N'-', N''), 1,
       0, NULL, SYSUTCDATETIME(), NULL, 0, NULL, NULL
FROM @Shops AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Users AS u WHERE u.NormalizedEmail = UPPER(s.OwnerEmail));

-- Compatible with SQL Server 2014.
DECLARE @Missing NVARCHAR(1000) = N'';
SELECT @Missing = @Missing + CASE WHEN @Missing = N'' THEN N'' ELSE N', ' END + s.OwnerEmail
FROM @Shops AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Users AS u WHERE u.NormalizedEmail = UPPER(s.OwnerEmail));

IF @Missing <> N''
BEGIN
    DECLARE @Msg NVARCHAR(2048);
    SET @Msg = N'Create these owner accounts first (sign-up page): ' + @Missing;
    RAISERROR(@Msg, 16, 1);
    RETURN;
END

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @NewShops TABLE (Id INT NOT NULL, OwnerUserId NVARCHAR(450) NOT NULL);

    INSERT INTO dbo.Shops
        (OwnerUserId, Name, Slug, Description, Tagline, Status, StatusReason,
         ContactName, Phone, Email, Website, AddressLine1, City, PostalCode, Country, PickupLocation,
         CommissionRate, FlatShippingRate, FreeShippingThreshold, Currency, ThemeKey,
         LogoUrl, BannerUrl, IsFeatured, CreatedUtc, ApprovedUtc, SuspendedUtc,
         BankAccountName, BankName, BankIban, BankStatementPath,
         BusinessRegistrationNumber, GovernmentIdPath, ProofOfAddressPath,
         RejectedUtc, TaxId, VerificationReason, VerificationStatus, VerifiedUtc)
    OUTPUT inserted.Id, inserted.OwnerUserId INTO @NewShops (Id, OwnerUserId)
    SELECT u.Id, s.Name, s.Slug, s.Description, s.Tagline, 1, NULL,
           s.ContactName, s.Phone, s.Email, NULL, s.AddressLine1, s.City, s.PostalCode, s.Country, s.PickupLocation,
           s.CommissionRate, s.FlatShippingRate, s.FreeShippingThreshold, N'USD', s.ThemeKey,
           NULL, NULL, 0, @Now, @Now, NULL,
           NULL, NULL, NULL, NULL,
           NULL, NULL, NULL,
           NULL, NULL, NULL, 0, NULL   -- VerificationStatus = 0 (NotStarted); the C# property is not nullable
    FROM @Shops AS s
    INNER JOIN dbo.Users AS u ON u.NormalizedEmail = UPPER(s.OwnerEmail)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Shops AS x WHERE x.Slug = s.Slug);
    -- Note: OwnerUserId must match the shop's owner; the join above sets it from the Users table.

    INSERT INTO dbo.ShopUsers (ShopId, UserId, Role, AcceptedTermsUtc, CreatedUtc)
    SELECT n.Id, n.OwnerUserId, 0, @Now, @Now
    FROM @NewShops AS n;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH

SELECT s.Id, s.Name, s.Slug, s.Status FROM dbo.Shops AS s
WHERE s.Slug IN (N'northwind-home', N'lumen-audio', N'atlas-notebooks');
