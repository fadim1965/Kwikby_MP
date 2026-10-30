<#
.SYNOPSIS
  Places the sample product photos under wwwroot/uploads/shops/{ShopId}/products/
  and writes import-ready CSV files that point at those photos.

.PARAMETER ProjectRoot
  The Kwikby_MP project folder (the folder that contains Kwikby_MP.csproj).

.PARAMETER Server
  SQL Server instance that holds the Kwikby database.

.PARAMETER Database
  Database name.

.EXAMPLE
  .\Place-SampleImages.ps1 -ProjectRoot "C:\Users\metrif\source\repos\DS\Kwikby_MP"
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectRoot,

    [string] $Server = "(localdb)\MSSQLLocalDB",

    [string] $Database = "KwikbyMP"
)

$ErrorActionPreference = "Stop"
$shopSlugs = @("northwind-home", "lumen-audio", "atlas-notebooks")

if (-not (Test-Path (Join-Path $ProjectRoot "Kwikby_MP.csproj"))) {
    throw "Kwikby_MP.csproj was not found in '$ProjectRoot'. Check the -ProjectRoot path."
}

# 1. Read each shop's ShopId from the database.
$slugList = ($shopSlugs | ForEach-Object { "'$_'" }) -join ","
$query = "SET NOCOUNT ON; SELECT Slug + '|' + CAST(Id AS VARCHAR(20)) FROM dbo.Shops WHERE Slug IN ($slugList);"
$lines = & sqlcmd -S $Server -d $Database -h -1 -W -Q $query
if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed. Check the server name, the database name and that sqlcmd is installed."
}

$ids = @{}
foreach ($line in $lines) {
    if ($line -match '^\s*([a-z0-9-]+)\|(\d+)\s*$') {
        $ids[$matches[1]] = [int]$matches[2]
    }
}
foreach ($slug in $shopSlugs) {
    if (-not $ids.ContainsKey($slug)) {
        throw "Shop '$slug' was not found in the database. Run 05_Shops.sql first."
    }
}

# 2. Copy each photo to wwwroot/uploads/shops/{ShopId}/products/{guid}.png
$manifest = Import-Csv -Path (Join-Path $PSScriptRoot "image-manifest.csv") -Encoding UTF8
foreach ($row in $manifest) {
    $shopId = $ids[$row.Shop]
    $target = Join-Path $ProjectRoot ("wwwroot\uploads\shops\" + $shopId + "\products")
    New-Item -ItemType Directory -Force -Path $target | Out-Null

    $source = Join-Path $PSScriptRoot ("images\" + $row.Shop + "\" + $row.Guid + ".png")
    Copy-Item -Path $source -Destination (Join-Path $target ($row.Guid + ".png")) -Force
}

# 3. Write import-ready CSV files with the real ShopIds in the Images column.
$readyDir = Join-Path $PSScriptRoot "ready"
New-Item -ItemType Directory -Force -Path $readyDir | Out-Null
foreach ($slug in $shopSlugs) {
    $text = Get-Content -Path (Join-Path $PSScriptRoot ("csv\" + $slug + ".csv")) -Raw -Encoding UTF8
    $text = $text.Replace("__SHOP_ID__", [string]$ids[$slug])
    Set-Content -Path (Join-Path $readyDir ($slug + ".csv")) -Value $text -Encoding UTF8 -NoNewline
}

# 4. Summary
Write-Host ""
Write-Host "Photos placed: $($manifest.Count) files under $ProjectRoot\wwwroot\uploads\shops\<ShopId>\products\"
Write-Host ""
foreach ($slug in $shopSlugs) {
    Write-Host ("  {0,-18} ShopId {1,-5} import file: ready\{0}.csv" -f $slug, $ids[$slug])
}
Write-Host ""
Write-Host "Next: in Admin > Import, choose each shop and upload its file from the ready folder."
