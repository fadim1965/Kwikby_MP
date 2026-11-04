using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface ICustomerApiRepository
{
    Task<PagedResponse<CustomerResponse>> ListAsync(string? query, int page, int pageSize, CancellationToken cancellationToken);

    Task<CustomerResponse?> GetAsync(string userId, CancellationToken cancellationToken);
}

public sealed class CustomerApiRepository : ICustomerApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public CustomerApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<PagedResponse<CustomerResponse>> ListAsync(string? query, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        await using var result = await _db
            .QueryMultipleAsync("usp_Customer_List", new { Query = query, Page = page, PageSize = pageSize }, cancellationToken).ConfigureAwait(false);

        var rows = await result.ReadAsync<CustomerResponse>().ConfigureAwait(false);
        var totalRow = await result.ReadSingleOrDefaultAsync<TotalCountRow>().ConfigureAwait(false);

        return new PagedResponse<CustomerResponse>
        {
            Items = rows.ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalRow?.TotalCount ?? 0
        };
    }

    public Task<CustomerResponse?> GetAsync(string userId, CancellationToken cancellationToken) =>
        _db.QuerySingleOrDefaultAsync<CustomerResponse>("usp_Customer_Get", new { UserId = userId }, cancellationToken);

    private sealed class TotalCountRow
    {
        public int TotalCount { get; set; }
    }
}
