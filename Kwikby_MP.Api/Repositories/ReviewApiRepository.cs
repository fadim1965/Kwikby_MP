using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IReviewApiRepository
{
    Task<PagedResponse<ReviewResponse>> ListAsync(int? productId, int? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<ReviewResponse> CreateAsync(ReviewCreateRequest request, string? userId, CancellationToken cancellationToken);

    Task SetStatusAsync(int reviewId, int status, CancellationToken cancellationToken);

    Task DeleteAsync(int reviewId, CancellationToken cancellationToken);
}

public sealed class ReviewApiRepository : IReviewApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public ReviewApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<PagedResponse<ReviewResponse>> ListAsync(int? productId, int? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        await using var result = await _db
            .QueryMultipleAsync("usp_Review_List", new { ProductId = productId, Status = status, Page = page, PageSize = pageSize }, cancellationToken).ConfigureAwait(false);

        var rows = await result.ReadAsync<ReviewResponse>().ConfigureAwait(false);
        var totalRow = await result.ReadSingleOrDefaultAsync<TotalCountRow>().ConfigureAwait(false);

        return new PagedResponse<ReviewResponse>
        {
            Items = rows.ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalRow?.TotalCount ?? 0
        };
    }

    public async Task<ReviewResponse> CreateAsync(ReviewCreateRequest request, string? userId, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(request.ReviewerName)
            ? (userId ?? "Anonymous")
            : request.ReviewerName.Trim();

        var row = await _db.QuerySingleOrDefaultAsync<ReviewResponse>(
            "usp_Review_Create",
            new
            {
                request.ProductId,
                UserId = userId,
                ReviewerName = name,
                request.Rating,
                request.Title,
                request.Body,
                IsVerifiedPurchase = false,
                Status = 1
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.BadRequest("Review could not be created.");
        return row;
    }

    public Task SetStatusAsync(int reviewId, int status, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Review_SetStatus", new { ReviewId = reviewId, Status = status }, cancellationToken);

    public Task DeleteAsync(int reviewId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Review_Delete", new { ReviewId = reviewId }, cancellationToken);

    private sealed class TotalCountRow
    {
        public int TotalCount { get; set; }
    }
}
