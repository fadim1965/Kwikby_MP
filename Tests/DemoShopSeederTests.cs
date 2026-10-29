using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WebDS_Test.Data;
using WebDS_Test.Domain;
using WebDS_Test.Services;
using Xunit;

namespace WebDS_Test.Tests;

/// <summary>
/// The optional sample marketplace: five shops of 25 products, repeatable and safe to run twice.
/// </summary>
public class DemoShopSeederTests
{
    private static IPasswordHasher Hasher()
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(p => "hash:" + p);
        return hasher.Object;
    }

    private static IConfiguration Config(string? password = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoVendorPassword"] = password })
            .Build();

    [Fact]
    public async Task Seeds_five_approved_shops_with_twenty_five_products_each()
    {
        await using var db = TestSupport.NewDb();

        var created = await DemoShopSeeder.SeedAsync(db, Hasher(), Config(), true, NullLogger.Instance);

        Assert.Equal(5, created);
        var shops = await db.Shops.Include(s => s.Products).ToListAsync();
        Assert.Equal(5, shops.Count);
        Assert.All(shops, s =>
        {
            Assert.Equal(ShopStatus.Approved, s.Status);
            Assert.Equal(25, s.Products.Count);
        });
        Assert.Equal(125, await db.ShopListings.CountAsync());
    }

    [Fact]
    public async Task Every_shop_has_its_own_owner_who_is_a_shop_member()
    {
        await using var db = TestSupport.NewDb();
        await DemoShopSeeder.SeedAsync(db, Hasher(), Config(), true, NullLogger.Instance);

        var shops = await db.Shops.Include(s => s.Users).ToListAsync();
        Assert.Equal(5, shops.Select(s => s.OwnerUserId).Distinct().Count());
        Assert.All(shops, s => Assert.Contains(s.Users, u => u.UserId == s.OwnerUserId && u.Role == ShopRole.Owner));
    }

    [Fact]
    public async Task Skus_are_unique_and_every_product_has_a_category_and_images()
    {
        await using var db = TestSupport.NewDb();
        await DemoShopSeeder.SeedAsync(db, Hasher(), Config(), true, NullLogger.Instance);

        var skus = await db.ProductVariants.Select(v => v.Sku).ToListAsync();
        Assert.Equal(skus.Count, skus.Distinct().Count());

        var products = await db.Products.Include(p => p.Images).Include(p => p.Variants).ToListAsync();
        var categoryIds = await db.Categories.Select(c => c.Id).ToListAsync();
        Assert.All(products, p =>
        {
            Assert.Contains(p.CategoryId, categoryIds);
            Assert.Equal(3, p.Images.Count);
            Assert.NotEmpty(p.Variants);
        });
    }

    [Fact]
    public async Task Running_it_twice_adds_nothing()
    {
        await using var db = TestSupport.NewDb();
        await DemoShopSeeder.SeedAsync(db, Hasher(), Config(), true, NullLogger.Instance);

        var again = await DemoShopSeeder.SeedAsync(db, Hasher(), Config(), true, NullLogger.Instance);

        Assert.Equal(0, again);
        Assert.Equal(5, await db.Shops.CountAsync());
        Assert.Equal(125, await db.Products.CountAsync());
        Assert.Equal(5, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Outside_development_it_refuses_to_run_without_an_explicit_password()
    {
        await using var db = TestSupport.NewDb();

        var created = await DemoShopSeeder.SeedAsync(db, Hasher(), Config(), false, NullLogger.Instance);

        Assert.Equal(0, created);
        Assert.Empty(db.Shops);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task A_configured_password_is_used_for_the_owners()
    {
        await using var db = TestSupport.NewDb();

        await DemoShopSeeder.SeedAsync(db, Hasher(), Config("Private#Pass1"), false, NullLogger.Instance);

        Assert.All(await db.Users.ToListAsync(), u => Assert.Equal("hash:Private#Pass1", u.PasswordHash));
    }
}
