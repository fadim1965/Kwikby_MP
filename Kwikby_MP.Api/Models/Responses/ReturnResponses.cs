namespace kwikby_MP.Api.Models.Responses;

public sealed class ReturnResponse
{
    public int Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public int OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public int ShopId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public int Status { get; set; }

    public string? CustomerNote { get; set; }

    public DateTime RequestedUtc { get; set; }

    public DateTime? DecidedUtc { get; set; }

    public DateTime? ReceivedUtc { get; set; }

    public decimal RefundAmount { get; set; }
}
