using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IAuditApiRepository
{
    Task<PagedResponse<AuditLogResponse>> ListAsync(string? actor, string? action, string? entityType, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class AuditApiRepository : IAuditApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public AuditApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<PagedResponse<AuditLogResponse>> ListAsync(string? actor, string? action, string? entityType, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        await using var result = await _db
            .QueryMultipleAsync("usp_Audit_List", new { Actor = actor, Action = action, EntityType = entityType, Page = page, PageSize = pageSize }, cancellationToken).ConfigureAwait(false);

        var rows = await result.ReadAsync<AuditLogResponse>().ConfigureAwait(false);
        var totalRow = await result.ReadSingleOrDefaultAsync<TotalCountRow>().ConfigureAwait(false);

        return new PagedResponse<AuditLogResponse>
        {
            Items = rows.ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalRow?.TotalCount ?? 0
        };
    }

    private sealed class TotalCountRow
    {
        public int TotalCount { get; set; }
    }
}
