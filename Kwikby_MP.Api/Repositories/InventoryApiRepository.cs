using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IInventoryApiRepository
{
    Task<IReadOnlyList<StockItemResponse>> ListAsync(int? shopId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovementResponse>> MovementsAsync(int? variantId, int? shopId, int page, int pageSize, CancellationToken cancellationToken);

    Task AdjustAsync(int variantId, StockAdjustRequest request, string actor, CancellationToken cancellationToken);
}

public sealed class InventoryApiRepository : IInventoryApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public InventoryApiRepository(IStoredProcedureGateway db) => _db = db;

    public Task<IReadOnlyList<StockItemResponse>> ListAsync(int? shopId, CancellationToken cancellationToken) =>
        _db.QueryAsync<StockItemResponse>("usp_Stock_List", new { ShopId = shopId }, cancellationToken);

    public Task<IReadOnlyList<StockMovementResponse>> MovementsAsync(
        int? variantId, int? shopId, int page, int pageSize, CancellationToken cancellationToken) =>
        _db.QueryAsync<StockMovementResponse>(
            "usp_StockMovements_List",
            new { VariantId = variantId, ShopId = shopId, Page = page, PageSize = pageSize },
            cancellationToken);

    public Task AdjustAsync(int variantId, StockAdjustRequest request, string actor, CancellationToken cancellationToken) =>
        _db.ExecuteAsync(
            "usp_AdjustStock",
            new
            {
                ProductVariantId = variantId,
                QuantityChange = request.QuantityDelta,
                Reason = MapReason(request.Reason),
                Note = request.Note,
                CreatedBy = actor
            },
            cancellationToken);

    private static int MapReason(string reason) =>
        reason.Trim().ToLowerInvariant() switch
        {
            "initialstock" or "initial_stock" or "initial" => 0,
            "restock" => 1,
            "sale" => 2,
            "return" => 3,
            "adjustment" => 4,
            "cancellation" => 5,
            _ => 4
        };
}
