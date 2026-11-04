namespace kwikby_MP.Api.Models.Requests;

/// <summary>Query string for <c>GET /api/v1/products</c>. Matches usp_SearchProducts.</summary>
public sealed class ProductQuery
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "relevance", "price_asc", "price_desc", "rating", "newest"
    };

    public string? Query { get; set; }

    public int? CategoryId { get; set; }

    public int? ShopId { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool InStockOnly { get; set; }

    public string? SortBy { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    /// <summary>Clamp untrusted paging and reject an unknown sort column.</summary>
    public void Normalize()
    {
        if (Page < 1) Page = 1;
        PageSize = Math.Clamp(PageSize, 1, 100);

        SortBy = string.IsNullOrWhiteSpace(SortBy) ? "relevance" : SortBy.Trim().ToLowerInvariant();
        if (!AllowedSorts.Contains(SortBy))
        {
            throw Infrastructure.ApiException.BadRequest(
                "sortBy must be one of: relevance, price_asc, price_desc, rating, newest.");
        }

        if (MinPrice is < 0) throw Infrastructure.ApiException.BadRequest("minPrice must be zero or greater.");
        if (MaxPrice is < 0) throw Infrastructure.ApiException.BadRequest("maxPrice must be zero or greater.");
        if (MinPrice is not null && MaxPrice is not null && MinPrice > MaxPrice)
        {
            throw Infrastructure.ApiException.BadRequest("minPrice cannot be greater than maxPrice.");
        }
    }
}
