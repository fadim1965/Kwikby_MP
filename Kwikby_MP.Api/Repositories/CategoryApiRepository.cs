using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface ICategoryApiRepository
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<CategoryResponse?> GetAsync(int categoryId, CancellationToken cancellationToken);

    Task<CategoryResponse> CreateAsync(CategoryCreateRequest request, CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateAsync(int categoryId, CategoryUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int categoryId, CancellationToken cancellationToken);
}

public sealed class CategoryApiRepository : ICategoryApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public CategoryApiRepository(IStoredProcedureGateway db) => _db = db;

    public Task<IReadOnlyList<CategoryResponse>> ListAsync(
        bool includeInactive, CancellationToken cancellationToken) =>
        _db.QueryAsync<CategoryResponse>(
            "usp_Category_List",
            new { IncludeInactive = includeInactive },
            cancellationToken);

    public Task<CategoryResponse?> GetAsync(int categoryId, CancellationToken cancellationToken) =>
        _db.QuerySingleOrDefaultAsync<CategoryResponse>(
            "usp_Category_Get",
            new { CategoryId = categoryId },
            cancellationToken);

    public async Task<CategoryResponse> CreateAsync(CategoryCreateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<CategoryResponse>(
            "usp_Category_Create",
            new
            {
                request.Name,
                request.Slug,
                request.Description,
                request.ParentId,
                request.DisplayOrder,
                IsActive = request.IsActive,
                request.CommissionRate,
                request.IsGlobal,
                request.ShopId
            },
            cancellationToken);
        if (row is null) throw Infrastructure.ApiException.NotFound("Category could not be created.");
        return row;
    }

    public async Task<CategoryResponse> UpdateAsync(int categoryId, CategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<CategoryResponse>(
            "usp_Category_Update",
            new
            {
                CategoryId = categoryId,
                request.Name,
                request.Slug,
                request.Description,
                request.ParentId,
                request.DisplayOrder,
                IsActive = request.IsActive,
                request.CommissionRate
            },
            cancellationToken);
        if (row is null) throw Infrastructure.ApiException.NotFound("Category not found.");
        return row;
    }

    public Task DeleteAsync(int categoryId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Category_Delete", new { CategoryId = categoryId }, cancellationToken);
}
