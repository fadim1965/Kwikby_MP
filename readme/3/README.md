# Admin and vendor product controllers: listing stock follows variants

## Changed
- Areas/Admin/Controllers/ProductsController.cs
- Areas/Vendor/Controllers/ProductsController.cs

In both files, the block that runs after a product is saved was changed:
- The shop's listing is created if it's missing, as before.
- Its StockQuantity is now set to the total of the product's active variants on every save. Previously it was set once, when the listing was created, and then went stale.
- The old fallback that used the variant sum when the active total was zero is removed. A product with no active stock now shows zero.

Variant stock was already saved by these controllers, so nothing else needed to change. The listing is recalculated from the variants after the save.

## Checks
- Edit a product's variant stock in the admin or vendor area, then confirm `09_StockDriftCheck.sql` returns no rows for it.
- Add a variant, remove one, and change stock. The listing total should follow each change.

## Before the data repair
Both controllers now keep listing stock in step with variants. The data repair can be run. Run `09_StockDriftCheck.sql` afterwards to confirm that no rows are returned.
