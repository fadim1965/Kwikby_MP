# Kwikby sample catalogue

Three fictional shops with 12 products each (36 products). Each product has one 1200 x 1200 PNG photo.
The photos are generated placeholders, not real product images.

## Where the photos go

Every photo is stored at:

    wwwroot/uploads/shops/{ShopId}/products/{guid}.png

- `{ShopId}` is the shop's database Id, so renaming a shop never breaks its photos.
- `{guid}` is a random GUID that is different for every photo.

The ShopId is only known after the shops exist, so the CSV files in `csv/` contain the placeholder `__SHOP_ID__`. The script below fills it in.

## Contents

| Path | What it is |
|---|---|
| `csv/` | The three import files, with `__SHOP_ID__` placeholders |
| `images/<shop>/<guid>.png` | The 36 photos (PNG, 1200 x 1200) |
| `image-manifest.csv` | Which GUID belongs to which SKU |
| `Place-SampleImages.ps1` | Copies the photos into the project and writes the finished CSV files |

## Steps

1. **Create the categories and shops.** Run `04_Categories.sql`, then `05_Shops.sql`, against the Kwikby database.
2. **Place the photos.** Open PowerShell in this folder and run:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\Place-SampleImages.ps1 -ProjectRoot "C:\Users\metrif\source\repos\DS\Kwikby_MP" -Server "(localdb)\MSSQLLocalDB" -Database "KwikbyMP"
   ```

   The script reads each shop's ShopId from the database, copies the photos to `wwwroot/uploads/shops/<ShopId>/products/`, and writes the finished CSV files to `ready/`. It prints the ShopId it used for each shop.
3. **Import.** In the admin area, open **Import**, choose the shop, and upload its file from `ready/`:
   - `northwind-home.csv` for Northwind Home
   - `lumen-audio.csv` for Lumen Audio
   - `atlas-notebooks.csv` for Atlas Notebooks

   Tick **Create new products** the first time.

## Notes

- Each variant row has its own photo. The importer adds these as gallery images on the same product, so a product with three sizes will show three photos.
- Re-running the script is safe. It overwrites the same files.
- The photos follow the product photo rules: PNG, at least 800 px on the shortest side, square.
