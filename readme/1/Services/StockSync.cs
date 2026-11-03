using Microsoft.EntityFrameworkCore;

namespace WebDS_Test.Services;

/// <summary>
/// Keeps each shop listing's StockQuantity equal to the total of its product's active variant stock.
/// Variants are the real per-option counts; the listing total exists so that the catalogue, cart and
/// search can read one number. Call this after any change to variant stock, before SaveChanges.
/// </summary>
public static class StockSync
{
    /// <summary>Sum of active variant stock for a product. Negative counts are treated as zero.</summary>
    public static int ProductStock(Product product) =>
        product.Variants.Where(v => v.IsActive).Sum(v => Math.Max(0, v.StockQuantity));

    /// <summary>
    /// Sets every active listing of the product to the current variant total. Loads the variant and
    /// listing collections if they are not loaded, so the total always covers every option.
    /// Variant changes that are tracked but not yet saved are included in the total.
    /// </summary>
    public static async Task RefreshListingsAsync(
        ApplicationDbContext db, Product product, DateTime nowUtc, CancellationToken ct = default)
    {
        var entry = db.Entry(product);
        if (!entry.Collection(p => p.Variants).IsLoaded)
        {
            await entry.Collection(p => p.Variants).LoadAsync(ct);
        }
        if (!entry.Collection(p => p.Listings).IsLoaded)
        {
            await entry.Collection(p => p.Listings).LoadAsync(ct);
        }

        var total = ProductStock(product);
        foreach (var listing in product.Listings.Where(l => l.IsActive))
        {
            if (listing.StockQuantity == total)
            {
                continue;
            }
            listing.StockQuantity = total;
            listing.UpdatedUtc = nowUtc;
        }
    }
}
