using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IReturnApiRepository
{
    Task<PagedResponse<ReturnResponse>> ListAsync(int? shopId, string? userId, int? status, int page, int pageSize, CancellationToken cancellationToken);

    Task SetStatusAsync(int returnId, int status, CancellationToken cancellationToken);
}

public sealed class ReturnApiRepository : IReturnApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public ReturnApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<PagedResponse<ReturnResponse>> ListAsync(int? shopId, string? userId, int? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        await using var result = await _db
            .QueryMultipleAsync("usp_Return_List", new { ShopId = shopId, UserId = userId, Status = status, Page = page, PageSize = pageSize }, cancellationToken).ConfigureAwait(false);

        var rows = await result.ReadAsync<ReturnResponse>().ConfigureAwait(false);
        var totalRow = await result.ReadSingleOrDefaultAsync<TotalCountRow>().ConfigureAwait(false);

        return new PagedResponse<ReturnResponse>
        {
            Items = rows.ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalRow?.TotalCount ?? 0
        };
    }

    public Task SetStatusAsync(int returnId, int status, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Return_SetStatus", new { ReturnId = returnId, Status = status }, cancellationToken);

    private sealed class TotalCountRow
    {
        public int TotalCount { get; set; }
    }
}
