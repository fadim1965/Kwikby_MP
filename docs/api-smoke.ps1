<#
.SYNOPSIS
    End-to-end smoke test for the Kwikby REST API.

.DESCRIPTION
    Exercises the public catalogue, JWT sign-in, the authenticated "me" endpoint and both
    dashboards against a running API. With -StartApi it starts `dotnet run` itself, waits for
    /health/live, runs the checks and stops the process again.

    The script expects the test data from the development seed (admin@api.test /
    vendor@api.test) or accepts -AdminEmail/-AdminPassword/-VendorEmail/-VendorPassword.

    Nothing secret is printed: tokens and passwords are never written to the console.

.EXAMPLE
    # API already running on the default port
    ./docs/api-smoke.ps1

.EXAMPLE
    # Start the API against a scratch database, test it, then stop it
    $env:ConnectionStrings__MultiVendorsStoresConn = "Data Source=.;Initial Catalog=MVStores_ApiTest;..."
    ./docs/api-smoke.ps1 -StartApi
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5269',
    [switch]$StartApi,
    [string]$ConnectionString,
    [string]$AdminEmail = 'admin@api.test',
    [string]$AdminPassword = 'Admin#12345',
    [string]$VendorEmail = 'vendor@api.test',
    [string]$VendorPassword = 'Vendor#12345'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$script:failures = [System.Collections.Generic.List[string]]::new()

Add-Type -AssemblyName System.Net.Http

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
        if ($match.Success) { return $match.Groups[1].Value }
    }
    throw 'No connection string. Set ConnectionStrings__MultiVendorsStoresConn or pass -ConnectionString.'
}

function Assert-Equal {
    param([string]$Name, $Expected, $Actual)
    if ($Expected -ne $Actual) {
        $script:failures.Add(('{0}: expected {1}, got {2}' -f $Name, $Expected, $Actual))
        Write-Host ('FAIL  {0}  (expected {1}, got {2})' -f $Name, $Expected, $Actual) -ForegroundColor Red
    }
    else {
        Write-Host ('ok    {0}' -f $Name) -ForegroundColor DarkGreen
    }
}

function Assert-True {
    param([string]$Name, [bool]$Condition)
    if (-not $Condition) {
        $script:failures.Add($Name)
        Write-Host ('FAIL  {0}' -f $Name) -ForegroundColor Red
    }
    else {
        Write-Host ('ok    {0}' -f $Name) -ForegroundColor DarkGreen
    }
}

function Assert-Problem {
    param([string]$Name, $Response, [int]$Status)
    Assert-Equal ('{0} status' -f $Name) $Status $Response.Status
    Assert-Equal ('{0} content type' -f $Name) 'application/problem+json' $Response.ContentType
}

$script:http = New-Object System.Net.Http.HttpClient
$script:http.Timeout = [TimeSpan]::FromSeconds(30)

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body
    )

    $request = New-Object System.Net.Http.HttpRequestMessage((New-Object System.Net.Http.HttpMethod($Method)), ($BaseUrl + $Path))
    try {
        if (-not [string]::IsNullOrWhiteSpace($Token)) {
            $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Token)
        }
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 10 -Compress
            $request.Content = New-Object System.Net.Http.StringContent($json, [System.Text.Encoding]::UTF8, 'application/json')
        }

        $response = $script:http.SendAsync($request).GetAwaiter().GetResult()
        try {
            $raw = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $parsed = $null
            if (-not [string]::IsNullOrWhiteSpace($raw)) {
                try { $parsed = $raw | ConvertFrom-Json } catch { $parsed = $null }
            }
            return [pscustomobject]@{
                Status      = [int]$response.StatusCode
                Body        = $parsed
                Raw         = $raw
                ContentType = $response.Content.Headers.ContentType.MediaType
            }
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

$apiProcess = $null
$outLog = Join-Path $env:TEMP 'kwikby-api-smoke.out.log'
$errLog = Join-Path $env:TEMP 'kwikby-api-smoke.err.log'

if ($StartApi) {
    $cs = Resolve-ConnectionString -Explicit $ConnectionString
    $env:ConnectionStrings__MultiVendorsStoresConn = $cs
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    Write-Host ('Starting API on {0} ...' -f $BaseUrl)
    $apiProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', (Join-Path $repoRoot 'Kwikby_MP.Api\Kwikby_MP.Api.csproj'), '--no-launch-profile', '--urls', $BaseUrl) `
        -WorkingDirectory $repoRoot -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog

    $deadline = (Get-Date).AddSeconds(120)
    $started = $false
    while ((Get-Date) -lt $deadline) {
        if ($apiProcess.HasExited) { break }
        try {
            $probe = Invoke-Api -Method GET -Path '/health/live'
            if ($probe.Status -eq 200) { $started = $true; break }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    }

    if (-not $started) {
        if (Test-Path -LiteralPath $errLog) { Get-Content -LiteralPath $errLog -Tail 30 | Write-Host }
        throw 'The API did not become reachable. See the log above.'
    }
    Write-Host 'API is up.'
}

try {
    Write-Host "`nHealth"
    Assert-Equal 'GET /health/live' 200 (Invoke-Api -Method GET -Path '/health/live').Status
    $ready = Invoke-Api -Method GET -Path '/health/ready'
    Assert-Equal 'GET /health/ready' 200 $ready.Status
    Assert-True 'ready reports Healthy' ($ready.Body.status -eq 'Healthy')

    Write-Host "`nPublic catalogue"
    $products = Invoke-Api -Method GET -Path '/api/v1/products?pageSize=5'
    Assert-Equal 'GET /products' 200 $products.Status
    Assert-Equal 'products totalCount' 1 $products.Body.totalCount
    Assert-Equal 'products first name' 'API Test Product' (@($products.Body.items)[0].name)
    Assert-Equal 'products first price' 19.99 (@($products.Body.items)[0].minPrice)

    $detail = Invoke-Api -Method GET -Path '/api/v1/products/9001'
    Assert-Equal 'GET /products/9001' 200 $detail.Status
    Assert-Equal 'product variants' 1 (@($detail.Body.variants).Count)
    Assert-Equal 'product images' 1 (@($detail.Body.images).Count)
    Assert-Equal 'product shop slug' 'api-test-shop' $detail.Body.shopSlug

    $missingProduct = Invoke-Api -Method GET -Path '/api/v1/products/999999'
    Assert-Problem 'GET missing product' $missingProduct 404

    $categories = Invoke-Api -Method GET -Path '/api/v1/categories'
    Assert-Equal 'GET /categories' 200 $categories.Status
    Assert-True 'active category visible' (@($categories.Body.id) -contains 9001)
    Assert-True 'inactive category hidden' (-not (@($categories.Body.id) -contains 9002))

    $hiddenCategories = Invoke-Api -Method GET -Path '/api/v1/categories?includeInactive=true'
    Assert-True 'anonymous includeInactive stays filtered' (-not (@($hiddenCategories.Body.id) -contains 9002))

    $shops = Invoke-Api -Method GET -Path '/api/v1/shops'
    Assert-Equal 'GET /shops' 200 $shops.Status
    Assert-True 'approved shop visible' (@($shops.Body.id) -contains 9001)
    Assert-True 'pending shop hidden' (-not (@($shops.Body.id) -contains 9002))

    $shop = Invoke-Api -Method GET -Path '/api/v1/shops/9001'
    Assert-Equal 'GET /shops/9001' 200 $shop.Status
    Assert-Equal 'shop slug' 'api-test-shop' $shop.Body.slug
    $pendingShop = Invoke-Api -Method GET -Path '/api/v1/shops/9002'
    Assert-Problem 'pending shop detail' $pendingShop 404

    Write-Host "`nAuthentication"
    $adminLogin = Invoke-Api -Method POST -Path '/api/v1/auth/token' -Body @{ email = $AdminEmail; password = $AdminPassword }
    Assert-Equal 'POST /auth/token (admin)' 200 $adminLogin.Status
    Assert-True 'admin token returned' (-not [string]::IsNullOrWhiteSpace($adminLogin.Body.accessToken))
    Assert-Equal 'token type' 'Bearer' $adminLogin.Body.tokenType
    $adminToken = $adminLogin.Body.accessToken

    $adminMe = Invoke-Api -Method GET -Path '/api/v1/auth/me' -Token $adminToken
    Assert-Equal 'GET /auth/me (admin)' 200 $adminMe.Status
    Assert-True 'admin role present' (@($adminMe.Body.roles) -contains 'Admin')

    $vendorLogin = Invoke-Api -Method POST -Path '/api/v1/auth/token' -Body @{ email = $VendorEmail; password = $VendorPassword }
    Assert-Equal 'POST /auth/token (vendor)' 200 $vendorLogin.Status
    $vendorToken = $vendorLogin.Body.accessToken

    $vendorMe = Invoke-Api -Method GET -Path '/api/v1/auth/me' -Token $vendorToken
    Assert-Equal 'GET /auth/me (vendor)' 200 $vendorMe.Status
    Assert-True 'vendor shop membership present' (@($vendorMe.Body.shopIds) -contains 9001)

    $anonymousMe = Invoke-Api -Method GET -Path '/api/v1/auth/me'
    Assert-Problem 'GET /auth/me without token' $anonymousMe 401

    $badPassword = Invoke-Api -Method POST -Path '/api/v1/auth/token' -Body @{ email = $AdminEmail; password = 'wrong-password' }
    Assert-Problem 'bad password' $badPassword 401

    $invalidBody = Invoke-Api -Method POST -Path '/api/v1/auth/token' -Body @{ email = 'not-an-email'; password = '' }
    Assert-Problem 'invalid login body' $invalidBody 400

    Write-Host "`nDashboards and authorization"
    $platform = Invoke-Api -Method GET -Path '/api/v1/admin/dashboard?days=30' -Token $adminToken
    Assert-Equal 'GET /admin/dashboard (admin)' 200 $platform.Status
    Assert-Equal 'platform gmv' 24.99 $platform.Body.gmv
    Assert-Equal 'platform orders' 1 $platform.Body.ordersPlaced
    Assert-Equal 'platform active shops' 1 $platform.Body.activeShops
    Assert-Equal 'platform awaiting approval' 1 $platform.Body.shopsAwaitingApproval

    $shopDash = Invoke-Api -Method GET -Path '/api/v1/shops/9001/dashboard?days=30' -Token $vendorToken
    Assert-Equal 'GET /shops/9001/dashboard (vendor)' 200 $shopDash.Status
    Assert-Equal 'shop revenue' 24.99 $shopDash.Body.figures.revenue
    Assert-Equal 'shop orders' 1 $shopDash.Body.figures.ordersPlaced
    Assert-Equal 'shop top product' 'API Test Product' (@($shopDash.Body.topProducts)[0].productName)

    $wrongShop = Invoke-Api -Method GET -Path '/api/v1/shops/9002/dashboard' -Token $vendorToken
    Assert-Problem 'vendor cannot read another shop' $wrongShop 403

    $vendorPlatform = Invoke-Api -Method GET -Path '/api/v1/admin/dashboard' -Token $vendorToken
    Assert-Problem 'vendor cannot read platform dashboard' $vendorPlatform 403

    $anonymousPlatform = Invoke-Api -Method GET -Path '/api/v1/admin/dashboard'
    Assert-Problem 'anonymous cannot read platform dashboard' $anonymousPlatform 401
}
finally {
    $script:http.Dispose()
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        Write-Host 'API stopped.'
    }
}

if ($script:failures.Count -gt 0) {
    Write-Host ("`n{0} smoke check(s) failed." -f $script:failures.Count) -ForegroundColor Red
    $script:failures | ForEach-Object { Write-Host ('  - {0}' -f $_) }
    exit 1
}

Write-Host "`nAll smoke checks passed." -ForegroundColor Green
