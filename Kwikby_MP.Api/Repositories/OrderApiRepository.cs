using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IOrderApiRepository
{
    Task<PagedResponse<OrderResponse>> ListAsync(OrderListQuery query, CancellationToken cancellationToken);

    Task<OrderResponse?> GetAsync(int orderId, CancellationToken cancellationToken);

    Task<OrderResponse> UpdateStatusAsync(int orderId, OrderStatusUpdateRequest request, CancellationToken cancellationToken);
}

public sealed class OrderApiRepository : IOrderApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public OrderApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<PagedResponse<OrderResponse>> ListAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();

        await using var result = await _db
            .QueryMultipleAsync("usp_Order_List",
                new { query.ShopId, UserId = query.UserId, query.Status, query.Page, query.PageSize },
                cancellationToken).ConfigureAwait(false);

        var rows = await result.ReadAsync<OrderListRow>().ConfigureAwait(false);
        var totalRow = await result.ReadSingleOrDefaultAsync<TotalCountRow>().ConfigureAwait(false);
        var total = totalRow?.TotalCount ?? 0;

        var items = rows.Select(row => new OrderResponse
        {
            Id = row.Id,
            ShopId = row.ShopId,
            ShopName = row.ShopName,
            OrderNumber = row.OrderNumber,
            UserId = row.UserId,
            Email = row.Email,
            CustomerName = row.CustomerName,
            Phone = row.Phone,
            Status = row.Status,
            PaymentMethod = row.PaymentMethod,
            CollectionStatus = row.CollectionStatus,
            CodAmount = row.CodAmount,
            Subtotal = row.Subtotal,
            ShippingTotal = row.ShippingTotal,
            TaxTotal = row.TaxTotal,
            DiscountTotal = row.DiscountTotal,
            GrandTotal = row.GrandTotal,
            RefundedTotal = row.RefundedTotal,
            CouponCode = row.CouponCode,
            PlacedUtc = row.PlacedUtc,
            PaidUtc = row.PaidUtc,
            ShippedUtc = row.ShippedUtc,
            DeliveredUtc = row.DeliveredUtc,
            CancelledUtc = row.CancelledUtc,
            TrackingNumber = row.TrackingNumber,
            Carrier = row.Carrier
        }).ToArray();

        return new PagedResponse<OrderResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<OrderResponse?> GetAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var result = await _db
            .QueryMultipleAsync("usp_Order_Get", new { OrderId = orderId }, cancellationToken).ConfigureAwait(false);

        var order = await result.ReadSingleOrDefaultAsync<OrderRow>().ConfigureAwait(false);
        if (order is null) return null;

        var lines = await result.ReadAsync<OrderLineResponse>().ConfigureAwait(false);
        var payments = await result.ReadAsync<PaymentResponse>().ConfigureAwait(false);

        return new OrderResponse
        {
            Id = order.Id,
            ShopId = order.ShopId,
            ShopName = order.ShopName,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            Email = order.Email,
            CustomerName = order.CustomerName,
            Phone = order.Phone,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            CollectionStatus = order.CollectionStatus,
            CodAmount = order.CodAmount,
            Subtotal = order.Subtotal,
            ShippingTotal = order.ShippingTotal,
            TaxTotal = order.TaxTotal,
            DiscountTotal = order.DiscountTotal,
            GrandTotal = order.GrandTotal,
            RefundedTotal = order.RefundedTotal,
            CouponCode = order.CouponCode,
            ShippingName = order.ShippingName,
            ShippingLine1 = order.ShippingLine1,
            ShippingLine2 = order.ShippingLine2,
            ShippingCity = order.ShippingCity,
            ShippingState = order.ShippingState,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingCountry = order.ShippingCountry,
            CustomerNote = order.CustomerNote,
            PlacedUtc = order.PlacedUtc,
            PaidUtc = order.PaidUtc,
            ShippedUtc = order.ShippedUtc,
            DeliveredUtc = order.DeliveredUtc,
            CancelledUtc = order.CancelledUtc,
            TrackingNumber = order.TrackingNumber,
            Carrier = order.Carrier,
            Lines = lines,
            Payments = payments
        };
    }

    public async Task<OrderResponse> UpdateStatusAsync(int orderId, OrderStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await using var result = await _db
            .QueryMultipleAsync("usp_Order_UpdateStatus",
                new { OrderId = orderId, Status = request.Status, request.TrackingNumber, request.Carrier },
                cancellationToken).ConfigureAwait(false);

        var order = await result.ReadSingleOrDefaultAsync<OrderRow>().ConfigureAwait(false);
        if (order is null) throw Infrastructure.ApiException.NotFound("Order not found.");

        var detail = await GetAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (detail is null) throw Infrastructure.ApiException.NotFound("Order not found.");
        return detail;
    }

    private sealed class OrderListRow
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
        public DateTime PlacedUtc { get; set; }
        public DateTime? PaidUtc { get; set; }
        public DateTime? ShippedUtc { get; set; }
        public DateTime? DeliveredUtc { get; set; }
        public DateTime? CancelledUtc { get; set; }
        public string? TrackingNumber { get; set; }
        public string? Carrier { get; set; }
    }

    private sealed class TotalCountRow
    {
        public int TotalCount { get; set; }
    }

    private sealed class OrderRow
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
        public int? CouponId { get; set; }
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
        public string? CarrierShipmentId { get; set; }
        public string? LabelUrl { get; set; }
        public string? CarrierError { get; set; }
    }
}
