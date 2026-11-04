<#
.SYNOPSIS
    Applies the Kwikby stored-procedure scripts to a SQL Server database, in order.

.DESCRIPTION
    Every script is idempotent (the procedures are installed with the
    IF OBJECT_ID ... CREATE + ALTER pattern), so this is safe to re-run.

    The connection string is never a required command-line argument: by default
    the script reads the ConnectionStrings__MultiVendorsStoresConn environment
    variable, then appsettings.Local.json. Passing -ConnectionString works but
    exposes the password in the process command line; prefer the environment
    variable on shared machines.

    Run it against a scratch database first, for example:

        $env:ConnectionStrings__MultiVendorsStoresConn = "Data Source=.;Initial Catalog=MVStores_ApiTest;..."
        ./Database/05_Deploy.ps1

.PARAMETER ConnectionString
    Optional. SQL Server connection string. Defaults to the environment variable
    or appsettings.Local.json.

.PARAMETER IncludeHero
    Also deploy 03_HeroEngine.sql. That script is not needed by Phase 1 and is
    not yet rewritten for SQL Server 2014.

.PARAMETER WhatIf
    Print the file/batch order without executing anything.
#>
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [switch]$IncludeHero,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-ConnectionString {
    param([string]$Explicit)

    if (-not [string]::IsNullOrWhiteSpace($Explicit)) { return $Explicit }

    if (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__MultiVendorsStoresConn)) {
        return $env:ConnectionStrings__MultiVendorsStoresConn
    }

    $local = Join-Path $repoRoot "appsettings.Local.json"
    if (Test-Path -LiteralPath $local) {
        $raw = Get-Content -LiteralPath $local -Raw
        $match = [regex]::Match($raw, '"MultiVendorsStoresConn"\s*:\s*"([^"]+)"')
        if ($match.Success) { return $match.Groups[1].Value }
    }

    throw "No connection string. Set ConnectionStrings__MultiVendorsStoresConn, add it to appsettings.Local.json, or pass -ConnectionString."
}

# Split a T-SQL script on batches the way sqlcmd does, but ignore GO inside
# block comments, line comments and string literals. A naive regex split breaks
# on the GO statements that appear in the explanatory header comments.
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

$scripts = [System.Collections.Generic.List[string]]::new()
$scripts.Add("Database/02_StoredProcedures.sql")
if ($IncludeHero) { $scripts.Add("Database/03_HeroEngine.sql") }
$scripts.Add("Database/04_ApiStoredProcedures.sql")

$connectionString = Resolve-ConnectionString -Explicit $ConnectionString

Add-Type -AssemblyName System.Data

$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
try {
    $connection.Open()
    Write-Host ("Connected to {0}." -f $connection.Database)

    foreach ($relative in $scripts) {
        $path = Join-Path $repoRoot $relative
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Missing script: $path"
        }

        $sql = Get-Content -LiteralPath $path -Raw
        # Split on real batch separators only; see Split-SqlBatches above.
        $batches = Split-SqlBatches -Sql $sql
        Write-Host ("==> {0} ({1} batches)" -f $relative, $batches.Count)

        $index = 0
        foreach ($batch in $batches) {
            $index++
            if ([string]::IsNullOrWhiteSpace($batch)) { continue }

            if ($WhatIf) {
                Write-Host ("    [WhatIf] batch {0}" -f $index)
                continue
            }

            $command = $connection.CreateCommand()
            try {
                $command.CommandText = $batch
                $command.CommandTimeout = 120
                [void]$command.ExecuteNonQuery()
                Write-Host ("    batch {0} ok" -f $index)
            }
            catch {
                Write-Error ("Batch {0} of {1} failed: {2}" -f $index, $relative, $_.Exception.Message)
                throw
            }
            finally {
                $command.Dispose()
            }
        }
    }

    $summary = $connection.CreateCommand()
    try {
        $summary.CommandText = "SELECT name FROM sys.procedures WHERE name LIKE 'usp[_]%' ORDER BY name"
        $reader = $summary.ExecuteReader()
        $names = [System.Collections.Generic.List[string]]::new()
        while ($reader.Read()) { $names.Add([string]$reader["name"]) }
        $reader.Close()
        Write-Host ("`nStored procedures now in {0}: {1}" -f $connection.Database, $names.Count)
        foreach ($name in $names) { Write-Host ("  - {0}" -f $name) }
    }
    finally {
        $summary.Dispose()
    }
}
finally {
    $connection.Dispose()
}
