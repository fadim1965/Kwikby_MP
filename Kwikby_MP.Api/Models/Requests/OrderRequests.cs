using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class OrderListQuery
{
    public int? ShopId { get; set; }

    public string? UserId { get; set; }

    public int? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        PageSize = Math.Clamp(PageSize, 1, 100);
    }
}

public sealed class OrderStatusUpdateRequest
{
    [Range(0, 5)] public int Status { get; set; }

    [StringLength(64)] public string? TrackingNumber { get; set; }

    [StringLength(40)] public string? Carrier { get; set; }
}
