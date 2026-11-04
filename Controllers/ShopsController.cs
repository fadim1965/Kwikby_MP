using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using kwikby_MP.Domain;
using kwikby_MP.Models;
using kwikby_MP.Services;

namespace kwikby_MP.Controllers;

public class ShopsController : StoreControllerBase
{
    private readonly IShopService _shops;
    private readonly IThemeCatalog _themes;
    private readonly IReviewService _reviews;

    public ShopsController(
        ICatalogService catalog,
        IOptions<StoreSettings> settings,
        ICartService cart,
        IShopService shops,
        IThemeCatalog themes,
        IReviewService reviews)
        : base(catalog, settings, cart)
    {
        _shops = shops;
        _themes = themes;
        _reviews = reviews;
    }

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        await LoadCartBadgeAsync(ct);
        var shops = await _shops.GetTradingShopsAsync(q, ct);

        // One grouped query covers the whole directory, however many shops are listed.
        var ratings = await _reviews.GetShopRatingsAsync(shops.Select(s => s.Id), ct);

        ViewData["Title"] = "Shops";
        return View(new ShopDirectoryViewModel
        {
            Shops = shops,
            Ratings = ratings,
            Search = q
        });
    }

    [HttpGet("shop/{slug}")]
    // Replace the existing Details method in Controllers/ShopsController.cs with this one.
    // Only the signature and the ProductQuery are new: price range, stock filter and empty-value cleanup.
    // Keep the rest of the method (the model, ViewData and return statement) as it is.

    public async Task<IActionResult> Details(
        string slug,
        int page = 1,
        string? q = null,
        string? category = null,
        string? brand = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool inStock = false,
        ProductSort sort = ProductSort.Newest,
        CancellationToken ct = default)
    {
        var shop = await _shops.GetBySlugAsync(slug, ct);
        if (shop is null || shop.Status != ShopStatus.Approved)
        {
            return NotFound();
        }

        await LoadCartBadgeAsync(ct);

        // Empty form fields arrive as "", which should mean "no filter".
        if (minPrice is { } low && maxPrice is { } high && low > high)
        {
            (minPrice, maxPrice) = (high, low);
        }

        var query = new ProductQuery
        {
            ShopId = shop.Id,
            ShopSlug = shop.Slug,
            Search = Clean(q),
            CategorySlug = Clean(category),
            Brand = Clean(brand),
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            InStockOnly = inStock,
            Sort = sort,
            Page = page,
            PageSize = Settings.ProductsPerPage
        };

        var results = await Catalog.BrowseAsync(query, ct);
        var categories = await Catalog.GetCategoryTreeAsync(shop.Id, ct);
        var theme = _themes.Get(shop.ThemeKey);

        var model = new ShopStorefrontViewModel
        {
            Shop = shop,
            Theme = theme,
            Results = results,
            Categories = categories,
            Brands = await Catalog.GetBrandsAsync(shop.Id, ct),
            PriceBounds = await Catalog.GetPriceBoundsAsync(shop.Id, ct),
            Rating = await _reviews.GetShopRatingAsync(shop.Id, ct)
        };

        ViewData["Title"] = shop.Name;

        return View(model);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    /*
    public async Task<IActionResult> Details(string slug, int page = 1, string? q = null, string? category = null, string? brand = null, ProductSort sort = ProductSort.Newest, CancellationToken ct = default)
    {
        var shop = await _shops.GetBySlugAsync(slug, ct);
        if (shop is null || shop.Status != ShopStatus.Approved)
        {
            return NotFound();
        }

        await LoadCartBadgeAsync(ct);

        var query = new ProductQuery
        {
            ShopId = shop.Id,
            ShopSlug = shop.Slug,
            Search = q,
            CategorySlug = category,
            Brand = brand,
            Sort = sort,
            Page = page,
            PageSize = Settings.ProductsPerPage
        };

        var results = await Catalog.BrowseAsync(query, ct);
        var categories = await Catalog.GetCategoryTreeAsync(shop.Id, ct);

        // Resolving the theme here (not in the view) matters: the layout renders <html> before the
        // body view runs, so ViewData set by the page would arrive too late to theme the chrome.
        var theme = _themes.Get(shop.ThemeKey);

        var model = new ShopStorefrontViewModel
        {
            Shop = shop,
            Theme = theme,
            Results = results,
            Categories = categories,
            Brands = await Catalog.GetBrandsAsync(shop.Id, ct),
            PriceBounds = await Catalog.GetPriceBoundsAsync(shop.Id, ct),
            Rating = await _reviews.GetShopRatingAsync(shop.Id, ct)
        };

        ViewData["Title"] = shop.Name;
        ViewData["ShopTheme"] = theme;
        return View(model);
    }
    */


}//StoreCotrollerBase

public sealed class ShopStorefrontViewModel
{
    public required Shop Shop { get; init; }

    /// <summary>The template theme resolved from <see cref="Shop.ThemeKey"/>.</summary>
    public required ShopTheme Theme { get; init; }

    public required PagedResult<ProductCard> Results { get; init; }
    public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();
    public IReadOnlyList<string> Brands { get; init; } = Array.Empty<string>();
    public PriceBounds PriceBounds { get; init; }

    /// <summary>The shop's aggregate rating, for the badge on its storefront.</summary>
    public ShopRating Rating { get; init; } = ShopRating.Empty;
}
