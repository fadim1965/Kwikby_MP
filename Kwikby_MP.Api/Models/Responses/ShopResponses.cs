namespace kwikby_MP.Api.Models.Responses;

/// <summary>
/// Shop list and shop detail share this shape. Fields only returned by the detail
/// procedure stay null when the list procedure did not select them.
/// </summary>
public sealed class ShopTeamMemberResponse
{
    public int ShopId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public int Role { get; set; }
}

public sealed class StockItemResponse
{
    public int ProductVariantId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string? VariantName { get; set; }

    public int StockQuantity { get; set; }

    public int LowStockThreshold { get; set; }

    public bool IsActive { get; set; }

    public int ShopId { get; set; }
}

public sealed class StockMovementResponse
{
    public int Id { get; set; }

    public int ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int QuantityDelta { get; set; }

    public int StockAfter { get; set; }

    public int Reason { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string? CreatedBy { get; set; }
}

public sealed class ShopResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Tagline { get; set; }

    public int Status { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? LogoUrl { get; set; }

    public string? BannerUrl { get; set; }

    public bool IsFeatured { get; set; }

    public string? ThemeKey { get; set; }

    public string? Currency { get; set; }

    public decimal? CommissionRate { get; set; }

    public DateTime? CreatedUtc { get; set; }

    public DateTime? ApprovedUtc { get; set; }

    // Detail-only fields.
    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? AddressLine1 { get; set; }

    public string? PostalCode { get; set; }

    public string? PickupLocation { get; set; }

    public decimal? FlatShippingRate { get; set; }

    public decimal? FreeShippingThreshold { get; set; }

    public DateTime? SuspendedUtc { get; set; }

    public DateTime? RejectedUtc { get; set; }
}
