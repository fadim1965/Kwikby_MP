# Kwikby database

Run the scripts in this order on the SQL Server database the application uses
(`MVStores`).

| Order | File | What it does |
|---|---|---|
| 1 | `00_Schema.sql` | All marketplace tables, keys, indexes and foreign keys. Generated from the EF Core model by `01_GenerateSchema.ps1`. |
| 2 | `02_StoredProcedures.sql` | Search, product detail, vendor and platform dashboards, and atomic stock adjustment. |
| 3 | `03_HeroEngine.sql` | Hero banner tables, indexes, the earned-shop view, and the booking, delivery and impression procedures. Only needed by the MVC hero feature. |
| 4 | `04_ApiStoredProcedures.sql` | Auth, category and shop procedures used by `Kwikby_MP.Api`. |
| 5 | `05_Deploy.ps1` | Deploys `02`, optionally `03`, and `04` idempotently. |
| 6 | `06_SeedApiTest.ps1` | Creates/refreshes the `MVStores_ApiTest` scratch database and seeds the API smoke-test data. |

## SQL Server 2014 compatibility

The live server is SQL Server 2014 (12.0, compatibility level 120). The scripts are
written for that version:

- Do **not** use `CREATE OR ALTER`; it arrived in SQL Server 2016 SP1.
- Do **not** use `DROP ... IF EXISTS`, `STRING_AGG`, `OPENJSON`, `JSON_VALUE`,
  `STRING_SPLIT`, `AT TIME ZONE`, `TRIM`, `CONCAT_WS`, `DATETRUNC`, `GREATEST` or
  `LEAST`.
- Install procedures with the portable pattern:

```sql
IF OBJECT_ID(N'dbo.usp_X', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_X AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_X ...
GO
```

`THROW` and `OFFSET/FETCH` are available on 2014 and are used in the scripts.

## Deploying to a scratch database first

Never apply the scripts to the live database before testing them on a scratch copy.
`06_SeedApiTest.ps1` does the whole setup and refuses to run unless the target
catalog is exactly `MVStores_ApiTest`:

```powershell
cd <repository root>
./Database/06_SeedApiTest.ps1
```

It reads the SQL credentials from `appsettings.Local.json` (or
`ConnectionStrings__MultiVendorsStoresConn`) and switches the catalog to the scratch
database. You can also pass a full connection string:

```powershell
./Database/06_SeedApiTest.ps1 -ConnectionString `
  'Data Source=.;Initial Catalog=MVStores_ApiTest;Integrated Security=True;TrustServerCertificate=True'
```

The seed is idempotent: it deletes and reinserts the same test rows.

## Deploying procedures to an existing database

Review the diff and take a backup first, then run:

```powershell
./Database/05_Deploy.ps1 `
  -ConnectionString 'Server=.;Database=MVStores;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True'
```

`05_Deploy.ps1`:

- resolves the connection string from `-ConnectionString`, then
  `ConnectionStrings__MultiVendorsStoresConn`, then `appsettings.Local.json`
- splits each script on real `GO` batch separators, ignoring `GO` inside block
  comments, line comments and string literals
- stops on the first failed batch and reports the file and batch number
- is idempotent and can be re-run

`03_HeroEngine.sql` is included only with `-IncludeHero`:

```powershell
./Database/05_Deploy.ps1 -IncludeHero
```

## Generating the schema

`00_Schema.sql` is generated, not hand-written. It must be produced from the same
model the application runs on, otherwise the tables and the code can disagree.

```powershell
cd <repository root>
./Database/01_GenerateSchema.ps1
```

This script adds the EF Core design package, creates a migration named
`InitialSchema`, and writes an idempotent SQL script. Commit the generated
`Data/Migrations` folder with the code.

Once the schema is generated, the application should apply migrations at start-up in
place of `EnsureCreated`. The existing `*Schema.cs` helper classes can then be
retired.

## Notes

- Procedures run in the same database as the EF Core context. They use the EF column
  names, which are the property names.
- Order placement, payments and the vendor ledger stay in the C# services, which are
  unit-tested. Stored procedures cover reads and atomic stock changes only.
- Keep the scripts idempotent. Use `IF OBJECT_ID(...) IS NULL` for tables and the
  portable create/alter pattern above for procedures and views.
- Run the scripts against a test copy before production.
- The API needs `EXECUTE` permission on the `usp_*` procedures. See
  `docs/API_GUIDE.md`, section 5.5.
