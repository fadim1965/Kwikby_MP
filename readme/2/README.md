# Stock fixes, part 2 (before the data repair)

## Changed
| File | Change |
|---|---|
| Services/ReturnService.cs | Returns put units back on the variant, then refresh the listing total with StockSync. The old listing-first rule is removed. |
| Services/Imports/ProductImportService.cs | Imported variant stock is set as before. The shop's active listing is created if missing, and the listing total is recalculated from the variants. |

## Not changed
- **Controllers/ProductsController.cs (storefront).** It reads stock only, so it needs no change.

## Still needed before the data repair is safe
The admin and vendor product controllers also write variant stock, and they do not call StockSync yet:
- Areas/Admin/Controllers/ProductsController.cs (variant writes near lines 367, 384; the listing total near 452–458)
- Areas/Vendor/Controllers/ProductsController.cs (variant writes near lines 396, 413; the listing total near 483–489)

After a variant is saved there, call `await StockSync.RefreshListingsAsync(_db, product, DateTime.UtcNow, ct);` before SaveChanges, and take the listing total from the variants rather than from a separate input.

Until those two files are changed, a manual edit in the admin or vendor area can make a listing drift again. Run `09_StockDriftCheck.sql` after the data repair to confirm.

## Import behaviour change
Imported products now get an active listing for their shop. Before this change, imported products could not be added to a cart, because the cart needs a listing.
