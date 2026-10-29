# Kwikby database

Run the scripts in this order on the `KwikbyMP` SQL Server database.

| Order | File | What it does |
|---|---|---|
| 1 | `00_Schema.sql` | All marketplace tables, keys, indexes and foreign keys. Generated from the EF Core model by `01_GenerateSchema.ps1`. |
| 2 | `02_StoredProcedures.sql` | Search, product detail, vendor and platform dashboards, and atomic stock adjustment. |
| 3 | `03_HeroEngine.sql` | Hero banner tables, indexes, the earned-shop view, and the booking, delivery and impression procedures. |

## Generating the schema

`00_Schema.sql` is generated, not hand-written. It must be produced from the same model the application runs on, otherwise the tables and the code can disagree.

```powershell
cd <repository root>
./Database/01_GenerateSchema.ps1
```

This script adds the EF Core design package, creates a migration named `InitialSchema`, and writes an idempotent SQL script. Commit the generated `Data/Migrations` folder with the code.

Once the schema is generated, the application should apply migrations at start-up in place of `EnsureCreated`. The existing `*Schema.cs` helper classes can then be retired.

## Notes

- Procedures run in the same database as the EF Core context. They use the EF column names, which are the property names.
- Order placement, payments and the vendor ledger stay in the C# services, which are unit-tested. Stored procedures cover reads and atomic stock changes only.
- Keep the scripts idempotent. Use `IF OBJECT_ID(...) IS NULL` for tables and `CREATE OR ALTER` for procedures and views.
- Run these scripts against a test copy before production.
