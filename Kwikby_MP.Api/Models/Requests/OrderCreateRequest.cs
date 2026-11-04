using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class CartItemRequest
{
    [Required] public int ProductVariantId { get; set; }

    [Range(1, 1000)] public int Quantity { get; set; } = 1;
}

public sealed class OrderCreateRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;

    [Required] public string CustomerName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [Required] public string ShippingName { get; set; } = string.Empty;

    [Required] public string ShippingLine1 { get; set; } = string.Empty;

    public string? ShippingLine2 { get; set; }

    [Required] public string ShippingCity { get; set; } = string.Empty;

    public string? ShippingState { get; set; }

    [Required] public string ShippingPostalCode { get; set; } = string.Empty;

    [Required] public string ShippingCountry { get; set; } = string.Empty;

    public string? CustomerNote { get; set; }

    public string? IdempotencyKey { get; set; }

    public int PaymentMethod { get; set; }

    public string? CouponCode { get; set; }

    [Required] public List<CartItemRequest> Items { get; set; } = new();
}
