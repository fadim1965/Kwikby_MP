<#
  Generates Database/00_Schema.sql from the EF Core model.

  The marketplace schema is defined by the entity classes and the DbContext
  (Domain/*.cs and Data/ApplicationDbContext.cs). Running EF Core's script
  generator produces the exact SQL Server DDL for every table, key, index and
  foreign key, which is more reliable than writing it by hand.

  Prerequisites: .NET 9 SDK. Run from the repository root, after the
  marketplace code (Domain, Data, Services) is in this repository.
#>
param(
    [string]$ContextName = "ApplicationDbContext",
    [string]$Output = "Database/00_Schema.sql"
)

$ErrorActionPreference = "Stop"

dotnet tool update --global dotnet-ef --version 9.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.*

# A migration named InitialSchema captures the whole current model.
dotnet ef migrations add InitialSchema --context $ContextName --output-dir Data/Migrations

# Idempotent: safe to run on a new database or on one that is already partly built.
dotnet ef migrations script --idempotent --context $ContextName --output $Output

Write-Host "Schema written to $Output"
