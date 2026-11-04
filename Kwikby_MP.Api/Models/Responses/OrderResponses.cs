namespace kwikby_MP.Api.Models.Responses;

public sealed class OrderLineResponse
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int? ProductVariantId { get; set; }

    public int? ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? VariantName { get; set; }

    public string? Sku { get; set; }

    public string? ImageUrl { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public sealed class PaymentResponse
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public int Status { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string? ProviderReference { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? CapturedUtc { get; set; }
}

public sealed class OrderResponse
{
    public int Id { get; set; }

    public int ShopId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public string OrderNumber { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public int Status { get; set; }

    public int PaymentMethod { get; set; }

    public int CollectionStatus { get; set; }

    public decimal CodAmount { get; set; }

    public decimal Subtotal { get; set; }

    public decimal ShippingTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public decimal RefundedTotal { get; set; }

    public string? CouponCode { get; set; }

    public string ShippingName { get; set; } = string.Empty;

    public string ShippingLine1 { get; set; } = string.Empty;

    public string? ShippingLine2 { get; set; }

    public string ShippingCity { get; set; } = string.Empty;

    public string? ShippingState { get; set; }

    public string ShippingPostalCode { get; set; } = string.Empty;

    public string ShippingCountry { get; set; } = string.Empty;

    public string? CustomerNote { get; set; }

    public DateTime PlacedUtc { get; set; }

    public DateTime? PaidUtc { get; set; }

    public DateTime? ShippedUtc { get; set; }

    public DateTime? DeliveredUtc { get; set; }

    public DateTime? CancelledUtc { get; set; }

    public string? TrackingNumber { get; set; }

    public string? Carrier { get; set; }

    public IReadOnlyList<OrderLineResponse> Lines { get; set; } = Array.Empty<OrderLineResponse>();

    public IReadOnlyList<PaymentResponse> Payments { get; set; } = Array.Empty<PaymentResponse>();
}
