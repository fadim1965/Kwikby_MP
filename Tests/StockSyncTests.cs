using Microsoft.EntityFrameworkCore;
using WebDS_Test.Domain;
using WebDS_Test.Services;
using Xunit;

namespace WebDS_Test.Tests;

/// <summary>
/// Variant stock is the source of truth; a listing's StockQuantity is the total of the product's active variants.
/// </summary>
public class StockSyncTests
{
    private static Product NewProduct(params (string Sku, int Stock, bool Active)[] variants)
    {
        var product = new Product
        {
            ShopId = 1,
            Name = "Test product",
            Slug = "test-product-" + Guid.NewGuid().ToString("N")[..8],
            ShortDescription = string.Empty,
            Description = string.Empty,
            IsPublished = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        foreach (var (sku, stock, active) in variants)
        {
            product.Variants.Add(new ProductVariant
            {
                Sku = sku,
                Name = string.Empty,
                Price = 10m,
                StockQuantity = stock,
                IsActive = active,
                LowStockThreshold = 5
            });
        }

        product.Listings.Add(new ShopListing
        {
            ShopId = 1,
            Price = 10m,
            StockQuantity = 0,
            IsActive = true,
            UpdatedUtc = DateTime.UtcNow
        });

        return product;
    }

    [Fact]
    public async Task Listing_total_is_the_sum_of_active_variants_only()
    {
        await using var db = TestSupport.NewDb();
        var product = NewProduct(("SYNC-1", 3, true), ("SYNC-2", 4, true), ("SYNC-3", 50, false));
        db.Products.Add(product);
        await db.SaveChangesAsync();

        await StockSync.RefreshListingsAsync(db, product, DateTime.UtcNow);

        Assert.Equal(7, product.Listings.Single().StockQuantity);
    }

    [Fact]
    public async Task Negative_variant_counts_count_as_zero()
    {
        await using var db = TestSupport.NewDb();
        var product = NewProduct(("NEG-1", 5, true), ("NEG-2", -2, true));
        db.Products.Add(product);
        await db.SaveChangesAsync();

        await StockSync.RefreshListingsAsync(db, product, DateTime.UtcNow);

        Assert.Equal(5, product.Listings.Single().StockQuantity);
    }

    [Fact]
    public async Task Unsaved_variant_changes_are_included_in_the_total()
    {
        await using var db = TestSupport.NewDb();
        var product = NewProduct(("TRK-1", 6, true), ("TRK-2", 6, true));
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // A sale takes 4 units from one variant but is not saved yet.
        product.Variants.First(v => v.Sku == "TRK-1").StockQuantity -= 4;
        await StockSync.RefreshListingsAsync(db, product, DateTime.UtcNow);

        Assert.Equal(8, product.Listings.Single().StockQuantity);
    }
}
