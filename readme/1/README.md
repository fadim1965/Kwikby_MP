# Stock consolidation: variant stock is the source of truth

## What changed
| File | Change |
|---|---|
| Services/StockSync.cs (new) | Sets each active listing's StockQuantity to the total of its product's active variants. |
| Services/InventoryService.cs | Manual adjustments refresh the listing total in the same save. |
| Services/OrderService.cs | Validation checks the variant. A sale takes stock from the variant. Cancellations and returns put stock back on the variant. Each refreshes the listing total. |
| Services/CartService.cs | Add-to-cart and quantity checks read the variant's stock. |
| Domain/Shop.cs | The listing's StockQuantity comment now says it is derived. |
| Tests/StockSyncTests.cs (new) | Three tests: total of active variants, negative counts, and unsaved changes. |

CatalogService.cs needs no change. Its listing reads are now correct because the listing total is kept in step with the variants.

## Still to change (files not in this package)
1. **Services/ReturnService.cs, lines ~425–432.** Returns still prefer the listing. Change it to add the units to the variant, then call `StockSync.RefreshListingsAsync(_db, variant.Product, now)`.
2. **Controllers/ProductsController.cs (~384, ~367) and Areas/Vendor/Controllers/ProductsController.cs (~413, ~396).** Variant stock edits must be followed by `StockSync.RefreshListingsAsync` before SaveChanges. The listing created from the form should take its total from the variants, not from a separate input.
3. **Services/Imports/ProductImportService.cs, lines ~293–302 and ~227.** Same rule: after setting variant stock, refresh the listing total.
4. **Data repair (once).** Run this on MVStores to bring existing listings into line with their variants:

```sql
UPDATE sl
SET sl.StockQuantity = x.VariantStock
FROM dbo.ShopListings AS sl
INNER JOIN (
    SELECT ProductId, SUM(CASE WHEN StockQuantity > 0 THEN StockQuantity ELSE 0 END) AS VariantStock
    FROM dbo.ProductVariants
    WHERE IsActive = 1
    GROUP BY ProductId
) AS x ON x.ProductId = sl.ProductId
WHERE sl.StockQuantity <> x.VariantStock;
```

Then run `09_StockDriftCheck.sql`. It should return no rows.

## Checks
- Run the new tests: `dotnet test Tests\WebDS_Test.Tests.csproj --filter StockSyncTests`.
- Sale, cancellation and manual-adjustment flows should each leave the variant and listing in agreement. Verify this in the browser after the remaining changes are made.
