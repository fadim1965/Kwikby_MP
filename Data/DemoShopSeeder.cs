using Microsoft.EntityFrameworkCore;
using kwikby_MP.Domain;
using kwikby_MP.Services;

namespace kwikby_MP.Data;

/// <summary>
/// Optional sample marketplace for testing: five approved shops with 25 products each, owned by five
/// vendor accounts. Off unless <c>Seed:DemoShops</c> is true.
/// </summary>
/// <remarks>
/// <para>
/// Idempotent: a shop whose slug already exists is skipped, an owner account that already exists is
/// reused, and categories are matched by slug. Running it twice adds nothing.
/// </para>
/// <para>
/// It writes only what a vendor could create through the app: shops, owners, products with variants and
/// images, and one shop listing per product. It adds no orders, reviews or ledger entries, so the
/// payout and refund tests start from a clean ledger.
/// </para>
/// <para>
/// The owner accounts share one password, <c>Seed:DemoVendorPassword</c>. Outside Development it must
/// be set explicitly; there is no built-in default to leak.
/// </para>
/// </remarks>
public static class DemoShopSeeder
{
    public const string DevelopmentPassword = "Vendor#12345";

    private static readonly (string Name, string Slug, string Description, int Order)[] ExtraCategories =
    {
        ("Books & Notebooks", "books-notebooks", "Notebooks, planners and sketchbooks.", 7),
        ("Gifts & Crafts", "gifts-crafts", "Handmade gifts, souvenirs and gift wrap.", 8),
        ("Electronics & Accessories", "electronics", "Cables, chargers, audio and desk accessories.", 9)
    };

    public static async Task<int> SeedAsync(
        ApplicationDbContext db,
        IPasswordHasher hasher,
        IConfiguration config,
        bool isDevelopment,
        ILogger logger,
        CancellationToken ct = default)
    {
        var password = config["Seed:DemoVendorPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!isDevelopment)
            {
                logger.LogWarning(
                    "Seed:DemoShops is on but Seed:DemoVendorPassword is not set; skipping the demo shops outside Development.");
                return 0;
            }

            password = DevelopmentPassword;
        }

        await EnsureCategoriesAsync(db, ct);
        var categories = await db.Categories
            .Where(c => c.IsGlobal)
            .ToDictionaryAsync(c => c.Slug, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);

        var created = 0;
        foreach (var data in DemoShopData.Shops)
        {
            if (await db.Shops.AnyAsync(s => s.Slug == data.Slug, ct))
            {
                continue;
            }

            var owner = await GetOrCreateOwnerAsync(db, hasher, data, password, ct);
            var now = DateTime.UtcNow;

            var shop = new Shop
            {
                OwnerUserId = owner.Id,
                Name = data.Name,
                Slug = data.Slug,
                Tagline = data.Tagline,
                Description = data.Description,
                Status = ShopStatus.Approved,
                ApprovedUtc = now,
                ContactName = data.OwnerName,
                Email = data.OwnerEmail,
                City = data.City,
                Country = "Lebanon",
                PickupLocation = $"{data.City} pickup point",
                CommissionRate = data.Commission,
                FlatShippingRate = data.ShippingRate,
                FreeShippingThreshold = data.FreeShippingThreshold,
                Currency = "USD",
                ThemeKey = data.ThemeKey,
                IsFeatured = data.Featured,
                CreatedUtc = now
            };
            shop.Users.Add(new ShopUser { UserId = owner.Id, Role = ShopRole.Owner, AcceptedTermsUtc = now, CreatedUtc = now });
            db.Shops.Add(shop);
            await db.SaveChangesAsync(ct);

            foreach (var item in data.Products)
            {
                if (!categories.TryGetValue(item.Category, out var categoryId))
                {
                    // The demo data references categories that the base seeder normally provides;
                    // in a bare store (e.g. tests) create the missing global category on demand so
                    // no product is silently skipped.
                    var newCategory = new Category
                    {
                        Name = item.Category.Replace("-", " ").Trim(),
                        Slug = item.Category,
                        Description = $"Auto-created for demo seeding ({item.Category}).",
                        DisplayOrder = 100,
                        IsGlobal = true,
                        IsActive = true
                    };
                    db.Categories.Add(newCategory);
                    await db.SaveChangesAsync(ct);
                    categoryId = newCategory.Id;
                    categories[item.Category] = categoryId;
                    logger.LogInformation("Created missing category '{Category}' for demo product '{Product}'.", item.Category, item.Name);
                }

                var product = BuildProduct(data, item, shop.Id, categoryId, now);
                db.Products.Add(product);
                await db.SaveChangesAsync(ct);

                var active = product.Variants.Where(v => v.IsActive).ToList();
                db.ShopListings.Add(new ShopListing
                {
                    ShopId = shop.Id,
                    ProductId = product.Id,
                    Price = active.Min(v => (decimal?)v.Price),
                    StockQuantity = active.Sum(v => v.StockQuantity),
                    IsActive = true,
                    UpdatedUtc = now
                });
            }

            await db.SaveChangesAsync(ct);
            created++;
            logger.LogInformation("Seeded demo shop '{Shop}' with {Count} products", data.Name, data.Products.Length);
        }

        return created;
    }

    private static async Task EnsureCategoriesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        foreach (var (name, slug, description, order) in ExtraCategories)
        {
            if (await db.Categories.AnyAsync(c => c.IsGlobal && c.Slug == slug, ct))
            {
                continue;
            }

            db.Categories.Add(new Category
            {
                Name = name,
                Slug = slug,
                Description = description,
                DisplayOrder = order,
                IsGlobal = true,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<ApplicationUser> GetOrCreateOwnerAsync(
        ApplicationDbContext db, IPasswordHasher hasher, DemoShop data, string password, CancellationToken ct)
    {
        var normalized = data.OwnerEmail.ToUpperInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            Email = data.OwnerEmail,
            NormalizedEmail = normalized,
            DisplayName = data.OwnerName,
            PasswordHash = hasher.Hash(password),
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = AccountService.CustomerRole });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    private static Product BuildProduct(DemoShop shop, DemoProduct item, int shopId, int categoryId, DateTime now)
    {
        var slug = Slugify(item.Name);
        var product = new Product
        {
            ShopId = shopId,
            Name = item.Name,
            Slug = slug,
            ShortDescription = $"{item.Name} from {shop.Name}.",
            Description = $"{item.Name}. Sample catalogue item created for platform testing (Seed:DemoShops).",
            Brand = shop.Brand,
            CategoryId = categoryId,
            IsPublished = true,
            IsFeatured = item.Featured,
            CreatedUtc = now.AddDays(-(item.Sku.Sum(c => (int)c) % 120)),
            UpdatedUtc = now
        };

        var hasSecond = item.VariantName is not null && item.VariantPrice is not null;
        product.Variants.Add(new ProductVariant
        {
            Sku = item.Sku,
            Name = hasSecond ? shop.BaseVariantName : string.Empty,
            Price = item.Price,
            CompareAtPrice = item.CompareAt,
            StockQuantity = item.Stock,
            IsActive = true,
            LowStockThreshold = 5
        });

        if (hasSecond)
        {
            product.Variants.Add(new ProductVariant
            {
                Sku = item.Sku + "-P",
                Name = item.VariantName!,
                Price = item.VariantPrice!.Value,
                StockQuantity = item.VariantStock,
                IsActive = true,
                LowStockThreshold = 5
            });
        }

        for (var i = 0; i < 3; i++)
        {
            product.Images.Add(new ProductImage
            {
                Url = DbSeeder.PlaceholderImage(item.Name, item.Category, i),
                AltText = i == 0 ? item.Name : i == 1 ? $"{item.Name} - alternate view" : $"{item.Name} - detail",
                DisplayOrder = i,
                IsPrimary = i == 0
            });
        }

        return product;
    }

    private static string Slugify(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length > 150 ? slug[..150] : slug;
    }
}
