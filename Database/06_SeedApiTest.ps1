<#
.SYNOPSIS
    Creates (or refreshes) the MVStores_ApiTest scratch database with the schema,
    the API stored procedures and a small deterministic dataset.

.DESCRIPTION
    Developer/test helper only. It refuses to run against any database whose name is
    not exactly MVStores_ApiTest, so it cannot accidentally write to the live MVStores
    database.

    Steps:
      1. Create MVStores_ApiTest if it does not exist.
      2. Apply Database/00_Schema.sql (idempotent EF-generated schema).
      3. Deploy Database/02_StoredProcedures.sql and 04_ApiStoredProcedures.sql via
         Database/05_Deploy.ps1.
      4. Insert/refresh the test admin, vendor, shop, category, product and order.

    The seed is idempotent: the same IDs are deleted and reinserted on every run.

.EXAMPLE
    ./Database/06_SeedApiTest.ps1

.EXAMPLE
    ./Database/06_SeedApiTest.ps1 -ConnectionString "Data Source=.;Initial Catalog=MVStores_ApiTest;Integrated Security=True;TrustServerCertificate=True"
#>
[CmdletBinding()]
param(
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$databaseName = 'MVStores_ApiTest'

function Resolve-ConnectionString {
    param([string]$Explicit)

    if (-not [string]::IsNullOrWhiteSpace($Explicit)) { return $Explicit }

    if (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__MultiVendorsStoresConn)) {
        return $env:ConnectionStrings__MultiVendorsStoresConn
    }

    $local = Join-Path $repoRoot 'appsettings.Local.json'
    if (Test-Path -LiteralPath $local) {
        $raw = Get-Content -LiteralPath $local -Raw
        $match = [regex]::Match($raw, '"MultiVendorsStoresConn"\s*:\s*"([^"]+)"')
        if ($match.Success) {
            # The local file points at the live database. Point a copy at the scratch
            # database instead of requiring a second secret.
            $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $match.Groups[1].Value
            $builder['Initial Catalog'] = $databaseName
            return $builder.ConnectionString
        }
    }

    throw "No connection string. Pass -ConnectionString, set ConnectionStrings__MultiVendorsStoresConn, or add appsettings.Local.json."
}

function Assert-ScratchDatabase {
    param([string]$Value)

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $Value
    $catalog = $builder['Initial Catalog']
    if ([string]::IsNullOrWhiteSpace($catalog) -or
        -not [string]::Equals($catalog, $databaseName, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to run: Initial Catalog must be '$databaseName' (was '$catalog'). This script must never touch the live database."
    }
}

# Split a T-SQL script on batches the way sqlcmd does, ignoring GO inside block
# comments, line comments and string literals.
function Split-SqlBatches {
    param([string]$Sql)

    $batches = [System.Collections.Generic.List[string]]::new()
    $current = [System.Text.StringBuilder]::new()
    $inBlockComment = $false
    $inString = $false

    foreach ($line in ($Sql -split "\r?\n")) {
        if (-not $inBlockComment -and -not $inString -and $line -match '^\s*GO\s*(--.*)?$') {
            $batches.Add($current.ToString())
            [void]$current.Clear()
            continue
        }

        [void]$current.AppendLine($line)

        $i = 0
        while ($i -lt $line.Length) {
            if ($inBlockComment) {
                $end = $line.IndexOf('*/', $i)
                if ($end -lt 0) { $i = $line.Length; break }
                $inBlockComment = $false
                $i = $end + 2
                continue
            }

            if ($inString) {
                $quote = $line.IndexOf("'", $i)
                if ($quote -lt 0) { $i = $line.Length; break }
                if ($quote + 1 -lt $line.Length -and $line[$quote + 1] -eq "'") {
                    $i = $quote + 2
                    continue
                }
                $inString = $false
                $i = $quote + 1
                continue
            }

            if ($line[$i] -eq '-' -and $i + 1 -lt $line.Length -and $line[$i + 1] -eq '-') { break }
            if ($line[$i] -eq '/' -and $i + 1 -lt $line.Length -and $line[$i + 1] -eq '*') {
                $inBlockComment = $true
                $i += 2
                continue
            }
            if ($line[$i] -eq "'") {
                $inString = $true
                $i++
                continue
            }
            $i++
        }
    }

    $batches.Add($current.ToString())
    return $batches
}

function Invoke-SqlScript {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Path
    )

    $sql = Get-Content -LiteralPath $Path -Raw
    $batches = Split-SqlBatches -Sql $sql
    $number = 0

    foreach ($batch in $batches) {
        $number++
        if ([string]::IsNullOrWhiteSpace($batch)) { continue }

        $command = $Connection.CreateCommand()
        $command.CommandText = $batch
        $command.CommandTimeout = 180
        try {
            [void]$command.ExecuteNonQuery()
        }
        catch {
            throw ("{0} batch {1} failed: {2}" -f (Split-Path $Path -Leaf), $number, $_.Exception.Message)
        }
        finally {
            $command.Dispose()
        }
    }

    Write-Host ("    {0}: {1} batch(es)" -f (Split-Path $Path -Leaf), $number)
}

function New-PasswordHash {
    param([string]$Password)

    $salt = New-Object byte[] 16
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($salt) } finally { $rng.Dispose() }

    $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $Password, $salt, 210000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try { $key = $kdf.GetBytes(32) } finally { $kdf.Dispose() }

    return ('210000.{0}.{1}' -f [Convert]::ToBase64String($salt), [Convert]::ToBase64String($key))
}

$connectionString = Resolve-ConnectionString -Explicit $ConnectionString
Assert-ScratchDatabase -Value $connectionString

Add-Type -AssemblyName System.Data
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connectionString
$serverConnectionString = (New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connectionString)
$serverConnectionString['Initial Catalog'] = 'master'

Write-Host "Creating $databaseName if needed ..."
$master = New-Object System.Data.SqlClient.SqlConnection $serverConnectionString.ConnectionString
$master.Open()
try {
    $create = $master.CreateCommand()
    $create.CommandText = "IF DB_ID(N'$databaseName') IS NULL CREATE DATABASE [$databaseName];"
    $create.CommandTimeout = 180
    try { [void]$create.ExecuteNonQuery() } finally { $create.Dispose() }
}
finally {
    $master.Close()
}

$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$connection.Open()
try {
    Write-Host 'Applying schema and procedures ...'
    Invoke-SqlScript -Connection $connection -Path (Join-Path $repoRoot 'Database\00_Schema.sql')
    $connection.Close()

    & (Join-Path $repoRoot 'Database\05_Deploy.ps1') -ConnectionString $builder.ConnectionString

    $connection.Open()
    Write-Host 'Seeding test data ...'

    $sql = @'
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE FROM dbo.OrderLines WHERE Id = 9001;
DELETE FROM dbo.Orders WHERE Id = 9001;
DELETE FROM dbo.ProductImages WHERE Id = 9001;
DELETE FROM dbo.ProductVariants WHERE Id = 9001;
DELETE FROM dbo.Products WHERE Id = 9001;
DELETE FROM dbo.Categories WHERE Id IN (9001, 9002);
DELETE FROM dbo.ShopUsers WHERE ShopId IN (9001, 9002);
DELETE FROM dbo.Shops WHERE Id IN (9001, 9002);
DELETE FROM dbo.UserRoles WHERE UserId IN ('api-admin-user', 'api-vendor-user');
DELETE FROM dbo.Users WHERE Id IN ('api-admin-user', 'api-vendor-user');

INSERT INTO dbo.Users
    (Id, Email, NormalizedEmail, DisplayName, Phone, PasswordHash, SecurityStamp, IsActive,
     AccessFailedCount, LockoutEndUtc, CreatedUtc, LastLoginUtc, TwoFactorEnabled)
VALUES
    ('api-admin-user', 'admin@api.test', 'ADMIN@API.TEST', 'API Admin', NULL, @AdminHash,
     'api-admin-security-stamp', 1, 0, NULL, SYSUTCDATETIME(), NULL, 0),
    ('api-vendor-user', 'vendor@api.test', 'VENDOR@API.TEST', 'API Vendor', NULL, @VendorHash,
     'api-vendor-security-stamp', 1, 0, NULL, SYSUTCDATETIME(), NULL, 0);

INSERT INTO dbo.UserRoles (UserId, RoleName) VALUES
    ('api-admin-user', 'Admin'),
    ('api-admin-user', 'Customer'),
    ('api-vendor-user', 'Customer');

SET IDENTITY_INSERT dbo.Shops ON;
INSERT INTO dbo.Shops
    (Id, OwnerUserId, Name, Slug, Description, Tagline, Status, StatusReason, ContactName,
     Phone, Email, Website, AddressLine1, City, PostalCode, Country, PickupLocation,
     CommissionRate, FlatShippingRate, FreeShippingThreshold, Currency, ThemeKey, LogoUrl,
     BannerUrl, IsFeatured, VerificationStatus, CreatedUtc, ApprovedUtc)
VALUES
    (9001, 'api-vendor-user', 'API Test Shop', 'api-test-shop',
     'A shop created for API smoke tests.', 'Test shop', 1, NULL, 'API Vendor',
     '+961 1 000 000', 'vendor@api.test', 'https://example.test', '1 Test Street', 'Beirut',
     '1100', 'Lebanon', 'Beirut', 0.10, 5.00, 50.00, 'USD', 'postal-classic', NULL, NULL,
     1, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    (9002, 'api-vendor-user', 'Hidden Pending Shop', 'hidden-pending-shop',
     'A pending shop that public endpoints must not expose.', 'Pending', 0, NULL, 'API Vendor',
     NULL, 'pending@api.test', NULL, NULL, 'Tripoli', NULL, 'Lebanon', NULL,
     0.10, 5.00, 50.00, 'USD', 'mono', NULL, NULL, 0, 0, SYSUTCDATETIME(), NULL);
SET IDENTITY_INSERT dbo.Shops OFF;

INSERT INTO dbo.ShopUsers (ShopId, UserId, Role, AcceptedTermsUtc, CreatedUtc)
VALUES (9001, 'api-vendor-user', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

SET IDENTITY_INSERT dbo.Categories ON;
INSERT INTO dbo.Categories
    (Id, ShopId, IsGlobal, Name, Slug, Description, ParentId, DisplayOrder, IsActive, CommissionRate)
VALUES
    (9001, NULL, 1, 'API Test Category', 'api-test-category',
     'Category for API smoke tests.', NULL, 1, 1, NULL),
    (9002, NULL, 1, 'Inactive API Category', 'inactive-api-category',
     'Must not appear in the public category list.', NULL, 2, 0, NULL);
SET IDENTITY_INSERT dbo.Categories OFF;

SET IDENTITY_INSERT dbo.Products ON;
INSERT INTO dbo.Products
    (Id, ShopId, Name, Slug, ShortDescription, Description, Brand, CategoryId, IsPublished,
     IsDeleted, IsFeatured, CreatedUtc, UpdatedUtc, TaxClassId, ModerationStatus)
VALUES
    (9001, 9001, 'API Test Product', 'api-test-product',
     'A product for smoke tests.', 'Long description for the API test product.', 'TestBrand',
     9001, 1, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, 0);
SET IDENTITY_INSERT dbo.Products OFF;

SET IDENTITY_INSERT dbo.ProductVariants ON;
INSERT INTO dbo.ProductVariants
    (Id, ProductId, Sku, Name, Price, CompareAtPrice, StockQuantity, IsActive,
     LowStockThreshold, RowVersion)
VALUES
    (9001, 9001, 'API-TEST-001', 'Default', 19.99, 24.99, 10, 1, 2, 0x01);
SET IDENTITY_INSERT dbo.ProductVariants OFF;

SET IDENTITY_INSERT dbo.ProductImages ON;
INSERT INTO dbo.ProductImages (Id, ProductId, Url, AltText, DisplayOrder, IsPrimary)
VALUES (9001, 9001, 'https://example.test/api-test-product.png', 'API Test Product', 0, 1);
SET IDENTITY_INSERT dbo.ProductImages OFF;

SET IDENTITY_INSERT dbo.Orders ON;
INSERT INTO dbo.Orders
    (Id, ShopId, OrderNumber, UserId, Email, CustomerName, Phone, Status, PaymentMethod,
     CollectionStatus, CodAmount, IdempotencyKey, Subtotal, ShippingTotal, TaxTotal,
     DiscountTotal, GrandTotal, RefundedTotal, CouponCode, CouponId, ShippingName,
     ShippingLine1, ShippingLine2, ShippingCity, ShippingState, ShippingPostalCode,
     ShippingCountry, CustomerNote, PlacedUtc, PaidUtc)
VALUES
    (9001, 9001, 'API-TEST-0001', NULL, 'buyer@api.test', 'API Buyer', NULL, 1, 0,
     0, 0, NULL, 19.99, 5.00, 0, 0, 24.99, 0, NULL, NULL, 'API Buyer',
     '1 Test Street', NULL, 'Beirut', '', '1100', 'Lebanon', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());
SET IDENTITY_INSERT dbo.Orders OFF;

SET IDENTITY_INSERT dbo.OrderLines ON;
INSERT INTO dbo.OrderLines
    (Id, OrderId, ProductVariantId, ProductId, ProductName, VariantName, Sku, ImageUrl,
     Quantity, UnitPrice, LineTotal)
VALUES
    (9001, 9001, 9001, 9001, 'API Test Product', 'Default', 'API-TEST-001', NULL,
     1, 19.99, 19.99);
SET IDENTITY_INSERT dbo.OrderLines OFF;

COMMIT TRANSACTION;
'@

    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 120
    [void]$command.Parameters.Add('@AdminHash', [System.Data.SqlDbType]::NVarChar, 400)
    [void]$command.Parameters.Add('@VendorHash', [System.Data.SqlDbType]::NVarChar, 400)
    $command.Parameters['@AdminHash'].Value = (New-PasswordHash 'Admin#12345')
    $command.Parameters['@VendorHash'].Value = (New-PasswordHash 'Vendor#12345')
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }

    Write-Host ''
    Write-Host "Ready: $databaseName" -ForegroundColor Green
    Write-Host 'Test accounts (scratch database only):'
    Write-Host '  admin@api.test  / Admin#12345   (Admin)'
    Write-Host '  vendor@api.test / Vendor#12345  (Customer + shop 9001)'
    Write-Host ''
    Write-Host 'Run the smoke test with:'
    Write-Host '  $env:ConnectionStrings__MultiVendorsStoresConn = "<scratch connection string>"'
    Write-Host '  ./docs/api-smoke.ps1 -StartApi'
}
finally {
    $connection.Close()
}
