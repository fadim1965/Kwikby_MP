using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class VariantCreateRequest
{
    [Required, StringLength(64, MinimumLength = 1)] public string Sku { get; set; } = string.Empty;

    [StringLength(80)] public string? Name { get; set; }

    [Range(0, double.MaxValue)] public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public int LowStockThreshold { get; set; } = 5;
}

public sealed class VariantUpdateRequest
{
    [Required, StringLength(64, MinimumLength = 1)] public string Sku { get; set; } = string.Empty;

    [StringLength(80)] public string? Name { get; set; }

    [Range(0, double.MaxValue)] public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public int LowStockThreshold { get; set; } = 5;
}

public sealed class ImageAddRequest
{
    [Required] public string Url { get; set; } = string.Empty;

    [StringLength(200)] public string? AltText { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPrimary { get; set; }
}
