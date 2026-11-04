namespace kwikby_MP.Api.Models.Responses;

public sealed class PayoutResponse
{
    public int Id { get; set; }

    public int ShopId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int Status { get; set; }

    public string Reference { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime? PaidUtc { get; set; }
}
