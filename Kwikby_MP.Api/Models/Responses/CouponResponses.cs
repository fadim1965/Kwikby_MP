namespace kwikby_MP.Api.Models.Responses;

public sealed class CouponResponse
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Type { get; set; }

    public decimal Value { get; set; }

    public decimal MinSubtotal { get; set; }

    public int? MaxUses { get; set; }

    public int UsedCount { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }
}
