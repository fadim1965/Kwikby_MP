namespace kwikby_MP.Api.Models.Responses;

public sealed class CategoryResponse
{
    public int Id { get; set; }

    public int? ShopId { get; set; }

    public bool IsGlobal { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ParentId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public decimal? CommissionRate { get; set; }
}

public sealed class ProductSummaryResponse
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public bool IsFeatured { get; set; }

    public int CategoryId { get; set; }

    public int ShopId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public string ShopSlug { get; set; } = string.Empty;

    public decimal MinPrice { get; set; }

    public int TotalStock { get; set; }

    public decimal? AvgRating { get; set; }

    public int ReviewCount { get; set; }
}

public sealed class ProductDetailResponse
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public int ShopId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public string ShopSlug { get; set; } = string.Empty;

    public int ReviewCount { get; set; }

    public decimal? AvgRating { get; set; }

    public IReadOnlyList<ProductVariantResponse> Variants { get; set; } = Array.Empty<ProductVariantResponse>();

    public IReadOnlyList<ProductImageResponse> Images { get; set; } = Array.Empty<ProductImageResponse>();
}

public sealed class ProductListItemResponse
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public bool IsFeatured { get; set; }

    public int CategoryId { get; set; }

    public int ShopId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public string ShopSlug { get; set; } = string.Empty;

    public decimal MinPrice { get; set; }

    public int TotalStock { get; set; }

    public decimal? AvgRating { get; set; }

    public int ReviewCount { get; set; }

    public bool IsPublished { get; set; }

    public int ModerationStatus { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

public sealed class ProductVariantResponse
{
    public int ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int StockQuantity { get; set; }

    public int LowStockThreshold { get; set; }
}

public sealed class ProductImageResponse
{
    public int ProductImageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPrimary { get; set; }
}
