using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface ICouponApiRepository
{
    Task<PagedResponse<CouponResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<CouponResponse?> GetAsync(int couponId, CancellationToken cancellationToken);

    Task<CouponResponse> CreateAsync(CouponCreateRequest request, CancellationToken cancellationToken);

    Task<CouponResponse> UpdateAsync(int couponId, CouponUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int couponId, CancellationToken cancellationToken);
}

public sealed class CouponApiRepository : ICouponApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public CouponApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<PagedResponse<CouponResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        await using var result = await _db
            .QueryMultipleAsync("usp_Coupon_List", new { Page = page, PageSize = pageSize }, cancellationToken).ConfigureAwait(false);

        var rows = await result.ReadAsync<CouponResponse>().ConfigureAwait(false);
        var totalRow = await result.ReadSingleOrDefaultAsync<TotalCountRow>().ConfigureAwait(false);

        return new PagedResponse<CouponResponse>
        {
            Items = rows.ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalRow?.TotalCount ?? 0
        };
    }

    public Task<CouponResponse?> GetAsync(int couponId, CancellationToken cancellationToken) =>
        _db.QuerySingleOrDefaultAsync<CouponResponse>("usp_Coupon_Get", new { CouponId = couponId }, cancellationToken);

    public async Task<CouponResponse> CreateAsync(CouponCreateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<CouponResponse>(
            "usp_Coupon_Create",
            new
            {
                request.Code,
                request.Description,
                Type = request.Type,
                Value = request.Value,
                MinSubtotal = request.MinSubtotal,
                request.MaxUses,
                StartsUtc = request.StartsUtc,
                ExpiresUtc = request.ExpiresUtc,
                IsActive = request.IsActive
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.BadRequest("Coupon could not be created.");
        return row;
    }

    public async Task<CouponResponse> UpdateAsync(int couponId, CouponUpdateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<CouponResponse>(
            "usp_Coupon_Update",
            new
            {
                CouponId = couponId,
                request.Code,
                request.Description,
                Type = request.Type,
                Value = request.Value,
                MinSubtotal = request.MinSubtotal,
                request.MaxUses,
                StartsUtc = request.StartsUtc,
                ExpiresUtc = request.ExpiresUtc,
                IsActive = request.IsActive
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.NotFound("Coupon not found.");
        return row;
    }

    public Task DeleteAsync(int couponId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Coupon_Delete", new { CouponId = couponId }, cancellationToken);

    private sealed class TotalCountRow
    {
        public int TotalCount { get; set; }
    }
}
