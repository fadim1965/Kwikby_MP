using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class ShopCreateRequest
{
    [Required] public string OwnerUserId { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)] public string Name { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)] public string Slug { get; set; } = string.Empty;

    [StringLength(600)] public string? Description { get; set; }

    [StringLength(160)] public string? Tagline { get; set; }

    [StringLength(120)] public string? ContactName { get; set; }

    [StringLength(40)] public string? Phone { get; set; }

    [StringLength(256)] public string? Email { get; set; }

    [StringLength(200)] public string? Website { get; set; }

    [StringLength(200)] public string? AddressLine1 { get; set; }

    [StringLength(100)] public string? City { get; set; }

    [StringLength(40)] public string? PostalCode { get; set; }

    [StringLength(80)] public string? Country { get; set; }

    [StringLength(120)] public string? PickupLocation { get; set; }

    [Range(0, 1)] public decimal CommissionRate { get; set; } = 0.10m;

    public decimal? FlatShippingRate { get; set; }

    public decimal? FreeShippingThreshold { get; set; }

    [StringLength(8)] public string Currency { get; set; } = "USD";

    [StringLength(40)] public string ThemeKey { get; set; } = "postal-classic";
}

public sealed class ShopUpdateRequest
{
    [Required, StringLength(120, MinimumLength = 1)] public string Name { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)] public string Slug { get; set; } = string.Empty;

    [StringLength(600)] public string? Description { get; set; }

    [StringLength(160)] public string? Tagline { get; set; }

    [StringLength(120)] public string? ContactName { get; set; }

    [StringLength(40)] public string? Phone { get; set; }

    [StringLength(256)] public string? Email { get; set; }

    [StringLength(200)] public string? Website { get; set; }

    [StringLength(200)] public string? AddressLine1 { get; set; }

    [StringLength(100)] public string? City { get; set; }

    [StringLength(40)] public string? PostalCode { get; set; }

    [StringLength(80)] public string? Country { get; set; }

    [StringLength(120)] public string? PickupLocation { get; set; }

    [Range(0, 1)] public decimal CommissionRate { get; set; } = 0.10m;

    public decimal? FlatShippingRate { get; set; }

    public decimal? FreeShippingThreshold { get; set; }

    [StringLength(8)] public string Currency { get; set; } = "USD";

    [StringLength(40)] public string ThemeKey { get; set; } = "postal-classic";

    public bool IsFeatured { get; set; }
}

public sealed class ShopStatusRequest
{
    [Range(0, 4)] public int Status { get; set; }

    [StringLength(500)] public string? Reason { get; set; }
}

public sealed class ShopTeamAddRequest
{
    [Required] public string UserId { get; set; } = string.Empty;

    public int Role { get; set; }
}

public sealed class StockAdjustRequest
{
    [Required] public int QuantityDelta { get; set; }

    [Required, StringLength(40)] public string Reason { get; set; } = string.Empty;

    [StringLength(200)] public string? Note { get; set; }
}
