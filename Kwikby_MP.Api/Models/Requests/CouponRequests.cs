using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public class CouponCreateRequest
{
    [Required, StringLength(64, MinimumLength = 1)] public string Code { get; set; } = string.Empty;

    [StringLength(256)] public string? Description { get; set; }

    public int Type { get; set; }

    public decimal Value { get; set; }

    public decimal MinSubtotal { get; set; }

    public int? MaxUses { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class CouponUpdateRequest : CouponCreateRequest { }
