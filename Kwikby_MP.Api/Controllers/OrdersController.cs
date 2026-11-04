using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using kwikby_MP.Data;
using kwikby_MP.Domain;
using kwikby_MP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/orders")]
public sealed class OrdersController : ApiControllerBase
{
    private readonly IOrderApiRepository _orders;
    private readonly IOrderService _orderService;
    private readonly ApplicationDbContext _db;

    public OrdersController(IOrderApiRepository orders, IOrderService orderService, ApplicationDbContext db)
    {
        _orders = orders;
        _orderService = orderService;
        _db = db;
    }

    /// <summary>List orders scoped by role. Customer sees own, vendor sees own shop, admin sees all.</summary>
    [Authorize(Policy = "ApiCustomer")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> List(
        [FromQuery] OrderListQuery query, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            if (User.IsInRole("Customer") && !User.IsInRole("Admin"))
            {
                var userId = User.FindFirst("sub")?.Value;
                if (userId is null) return Forbid();
                query.UserId = userId;
                query.ShopId = null;
            }
            else
            {
                var claim = User.FindFirst("shop_id")?.Value;
                if (int.TryParse(claim, out var id)) query.ShopId = id;
            }
        }

        return Ok(await _orders.ListAsync(query, cancellationToken));
    }

    /// <summary>One order, scoped by role.</summary>
    [Authorize(Policy = "ApiCustomer")]
    [HttpGet("{orderId:int}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Get(int orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(orderId, cancellationToken);
        if (order is null) return NotFoundProblem("Order not found.");

        if (!User.IsInRole("Admin"))
        {
            var userId = User.FindFirst("sub")?.Value;
            var shopClaim = User.FindFirst("shop_id")?.Value;
            var isOwner = userId is not null && order.UserId == userId;
            var isShop = int.TryParse(shopClaim, out var sid) && sid == order.ShopId;
            if (!isOwner && !isShop) return Forbid();
        }

        return Ok(order);
    }

    /// <summary>Place an order (hybrid: reuses the tested C# OrderService).</summary>
    [Authorize(Policy = "ApiCustomer")]
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] OrderCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return ProblemResult(400, "Bad request", "The cart is empty.");

        var userId = User.FindFirst("sub")?.Value;

        // Load variants with product/shop for the cart.
        var variantIds = request.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants
            .Include(v => v.Product).ThenInclude(p => p!.Shop)
            .Include(v => v.Product).ThenInclude(p => p!.Listings)
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        var cart = new Cart { UserId = userId };
        foreach (var item in request.Items)
        {
            var variant = variants.FirstOrDefault(v => v.Id == item.ProductVariantId);
            if (variant is null) return ProblemResult(400, "Bad request", $"Variant {item.ProductVariantId} not found.");
            cart.Items.Add(new CartItem { ProductVariantId = variant.Id, ProductVariant = variant, Quantity = item.Quantity, AddedUtc = DateTime.UtcNow });
        }

        var checkout = new CheckoutRequest
        {
            Email = request.Email,
            CustomerName = request.CustomerName,
            Phone = request.Phone,
            ShippingName = request.ShippingName,
            ShippingLine1 = request.ShippingLine1,
            ShippingLine2 = request.ShippingLine2,
            ShippingCity = request.ShippingCity,
            ShippingState = request.ShippingState,
            ShippingPostalCode = request.ShippingPostalCode,
            ShippingCountry = request.ShippingCountry,
            CustomerNote = request.CustomerNote,
            IdempotencyKey = request.IdempotencyKey,
            PaymentMethod = (PaymentMethod)request.PaymentMethod,
            CouponCode = request.CouponCode
        };

        var result = await _orderService.PlaceOrderAsync(checkout, cart, cancellationToken);
        if (!result.Success || result.Order is null)
            return ProblemResult(400, "Order failed", result.Message);

        var created = await _orders.GetAsync(result.Order.Id, cancellationToken);
        return created is null
            ? ProblemResult(500, "Order created but could not be loaded", result.Message)
            : CreatedAtAction(nameof(Get), new { orderId = created.Id }, created);
    }

    /// <summary>Update order status. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPatch("{orderId:int}/status")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(
        int orderId, [FromBody] OrderStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var order = await _orders.GetAsync(orderId, cancellationToken);
            if (order is null) return NotFoundProblem("Order not found.");
            var claim = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(claim, out var id) || id != order.ShopId) return Forbid();
        }

        return Ok(await _orders.UpdateStatusAsync(orderId, request, cancellationToken));
    }
}
