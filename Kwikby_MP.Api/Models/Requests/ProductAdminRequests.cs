using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class ProductCreateRequest
{
    [Required] public int ShopId { get; set; }

    [Required, StringLength(160, MinimumLength = 1)] public string Name { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 1)] public string Slug { get; set; } = string.Empty;

    [StringLength(200)] public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    [StringLength(120)] public string? Brand { get; set; }

    [Required] public int CategoryId { get; set; }

    public int? TaxClassId { get; set; }

    public bool IsPublished { get; set; } = true;

    public bool IsFeatured { get; set; }

    public int ModerationStatus { get; set; }
}

public sealed class ProductUpdateRequest
{
    [Required, StringLength(160, MinimumLength = 1)] public string Name { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 1)] public string Slug { get; set; } = string.Empty;

    [StringLength(200)] public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    [StringLength(120)] public string? Brand { get; set; }

    [Required] public int CategoryId { get; set; }

    public int? TaxClassId { get; set; }

    public bool IsPublished { get; set; } = true;

    public bool IsFeatured { get; set; }
}

public sealed class ProductPublishRequest
{
    public bool IsPublished { get; set; }
}

public sealed class ProductModerationRequest
{
    [Range(0, 3)] public int ModerationStatus { get; set; }

    [StringLength(500)] public string? Reason { get; set; }
}

public sealed class ProductListQuery
{
    public int? ShopId { get; set; }

    public int? CategoryId { get; set; }

    public string? Query { get; set; }

    public bool? IsPublished { get; set; }

    public int? ModerationStatus { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        PageSize = Math.Clamp(PageSize, 1, 100);
    }
}
