# Kwikby: schema fixes and stock check

Run against the MVStores database (or KwikbyMP, depending on the database you use), SQL Server 2014 compatible.

1. **Back up the database** first.
2. `08_PreChecks.sql`: lists any rows that would block the fixes. Each result should be empty. If not, fix those rows first.
3. `07_SchemaFixes.sql`: applies the fixes in one transaction:
   - RejectedUtc and VerifiedUtc become datetime2.
   - VerificationStatus becomes NOT NULL with default 0.
   - Shop status must be 0 to 4.
   - Category slugs are unique among global categories, and among each shop's own categories.
4. `09_StockDriftCheck.sql`: read-only. Lists products whose shop-listing stock differs from the sum of their active variant stock. Use it to decide the stock source before removing a column.

The scripts are safe to re-run. 07 skips anything already in place.
