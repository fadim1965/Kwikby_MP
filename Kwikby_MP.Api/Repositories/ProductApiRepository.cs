using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IProductApiRepository
{
    Task<PagedResponse<ProductSummaryResponse>> SearchAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<ProductDetailResponse?> GetAsync(int productId, CancellationToken cancellationToken);

    Task<PagedResponse<ProductListItemResponse>> ListAdminAsync(ProductListQuery query, CancellationToken cancellationToken);

    Task<ProductDetailResponse> CreateAsync(ProductCreateRequest request, CancellationToken cancellationToken);

    Task<ProductDetailResponse> UpdateAsync(int productId, ProductUpdateRequest request, CancellationToken cancellationToken);

    Task SoftDeleteAsync(int productId, CancellationToken cancellationToken);

    Task SetPublishedAsync(int productId, bool isPublished, CancellationToken cancellationToken);

    Task SetModerationAsync(int productId, int status, string? reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductVariantResponse>> ListVariantsAsync(int productId, CancellationToken cancellationToken);

    Task<ProductVariantResponse> CreateVariantAsync(int productId, VariantCreateRequest request, CancellationToken cancellationToken);

    Task<ProductVariantResponse> UpdateVariantAsync(int variantId, VariantUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteVariantAsync(int variantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductImageResponse>> ListImagesAsync(int productId, CancellationToken cancellationToken);

    Task<ProductImageResponse> AddImageAsync(int productId, ImageAddRequest request, CancellationToken cancellationToken);

    Task DeleteImageAsync(int imageId, CancellationToken cancellationToken);
}

public sealed class ProductApiRepository : IProductApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public ProductApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<PagedResponse<ProductSummaryResponse>> SearchAsync(
        ProductQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();

        var rows = await _db.QueryAsync<ProductSummaryRow>(
            "usp_SearchProducts",
            new
            {
                Query = string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim(),
                query.CategoryId,
                query.ShopId,
                query.MinPrice,
                query.MaxPrice,
                query.InStockOnly,
                query.SortBy,
                query.Page,
                query.PageSize
            },
            cancellationToken).ConfigureAwait(false);

        var total = rows.Count > 0 ? rows[0].TotalCount : 0;
        var items = rows.Select(row => new ProductSummaryResponse
        {
            ProductId = row.ProductId,
            Name = row.Name,
            Slug = row.Slug,
            Brand = row.Brand,
            IsFeatured = row.IsFeatured,
            CategoryId = row.CategoryId,
            ShopId = row.ShopId,
            ShopName = row.ShopName,
            ShopSlug = row.ShopSlug,
            MinPrice = row.MinPrice,
            TotalStock = row.TotalStock,
            AvgRating = row.AvgRating,
            ReviewCount = row.ReviewCount
        }).ToArray();

        return new PagedResponse<ProductSummaryResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<ProductDetailResponse?> GetAsync(int productId, CancellationToken cancellationToken)
    {
        await using var result = await _db
            .QueryMultipleAsync("usp_GetProductDetail", new { ProductId = productId }, cancellationToken)
            .ConfigureAwait(false);

        var product = await result.ReadSingleOrDefaultAsync<ProductRow>().ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var variants = await result.ReadAsync<ProductVariantResponse>().ConfigureAwait(false);
        var images = await result.ReadAsync<ProductImageResponse>().ConfigureAwait(false);
        var rating = await result.ReadSingleOrDefaultAsync<RatingRow>().ConfigureAwait(false);

        return new ProductDetailResponse
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Slug = product.Slug,
            Brand = product.Brand,
            ShortDescription = product.ShortDescription,
            Description = product.Description,
            CategoryId = product.CategoryId,
            ShopId = product.ShopId,
            ShopName = product.ShopName,
            ShopSlug = product.ShopSlug,
            ReviewCount = rating?.ReviewCount ?? 0,
            AvgRating = rating?.AvgRating,
            Variants = variants,
            Images = images
        };
    }

    public async Task<PagedResponse<ProductListItemResponse>> ListAdminAsync(
        ProductListQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();

        await using var result = await _db
            .QueryMultipleAsync("usp_Product_List",
                new
                {
                    query.ShopId,
                    query.CategoryId,
                    Query = string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim(),
                    query.IsPublished,
                    query.ModerationStatus,
                    query.Page,
                    query.PageSize
                },
                cancellationToken).ConfigureAwait(false);

        var rows = await result.ReadAsync<ProductListRow>().ConfigureAwait(false);
        var totalRow = await result.ReadSingleOrDefaultAsync<TotalCountRow>().ConfigureAwait(false);
        var total = totalRow?.TotalCount ?? 0;

        var items = rows.Select(row => new ProductListItemResponse
        {
            ProductId = row.ProductId,
            Name = row.Name,
            Slug = row.Slug,
            Brand = row.Brand,
            IsFeatured = row.IsFeatured,
            CategoryId = row.CategoryId,
            ShopId = row.ShopId,
            ShopName = row.ShopName,
            ShopSlug = row.ShopSlug,
            MinPrice = row.MinPrice,
            TotalStock = row.TotalStock,
            AvgRating = row.AvgRating,
            ReviewCount = row.ReviewCount,
            IsPublished = row.IsPublished,
            ModerationStatus = row.ModerationStatus,
            IsDeleted = row.IsDeleted,
            CreatedUtc = row.CreatedUtc,
            UpdatedUtc = row.UpdatedUtc
        }).ToArray();

        return new PagedResponse<ProductListItemResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<ProductDetailResponse> CreateAsync(
        ProductCreateRequest request, CancellationToken cancellationToken)
    {
        await using var result = await _db
            .QueryMultipleAsync("usp_Product_Create",
                new
                {
                    request.ShopId,
                    request.Name,
                    request.Slug,
                    request.ShortDescription,
                    request.Description,
                    request.Brand,
                    request.CategoryId,
                    request.TaxClassId,
                    request.IsPublished,
                    request.IsFeatured,
                    request.ModerationStatus
                },
                cancellationToken).ConfigureAwait(false);

        var row = await result.ReadSingleOrDefaultAsync<ProductRow>().ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.BadRequest("Product could not be created.");

        var detail = await GetAsync(row.ProductId, cancellationToken).ConfigureAwait(false);
        if (detail is null) throw Infrastructure.ApiException.NotFound("Product not found.");
        return detail;
    }

    public async Task<ProductDetailResponse> UpdateAsync(
        int productId, ProductUpdateRequest request, CancellationToken cancellationToken)
    {
        await using var result = await _db
            .QueryMultipleAsync("usp_Product_Update",
                new
                {
                    ProductId = productId,
                    request.Name,
                    request.Slug,
                    request.ShortDescription,
                    request.Description,
                    request.Brand,
                    request.CategoryId,
                    request.TaxClassId,
                    request.IsPublished,
                    request.IsFeatured
                },
                cancellationToken).ConfigureAwait(false);

        var row = await result.ReadSingleOrDefaultAsync<ProductRow>().ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.NotFound("Product not found.");

        var detail = await GetAsync(productId, cancellationToken).ConfigureAwait(false);
        if (detail is null) throw Infrastructure.ApiException.NotFound("Product not found.");
        return detail;
    }

    public Task SoftDeleteAsync(int productId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Product_SoftDelete", new { ProductId = productId }, cancellationToken);

    public Task SetPublishedAsync(int productId, bool isPublished, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Product_SetPublished", new { ProductId = productId, IsPublished = isPublished }, cancellationToken);

    public Task SetModerationAsync(int productId, int status, string? reason, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Product_SetModeration", new { ProductId = productId, ModerationStatus = status, Reason = reason }, cancellationToken);

    public Task<IReadOnlyList<ProductVariantResponse>> ListVariantsAsync(int productId, CancellationToken cancellationToken) =>
        _db.QueryAsync<ProductVariantResponse>("usp_Variant_List", new { ProductId = productId }, cancellationToken);

    public async Task<ProductVariantResponse> CreateVariantAsync(int productId, VariantCreateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<ProductVariantResponse>(
            "usp_Variant_Create",
            new
            {
                ProductId = productId,
                request.Sku,
                Name = request.Name,
                request.Price,
                request.CompareAtPrice,
                request.StockQuantity,
                IsActive = request.IsActive,
                request.LowStockThreshold
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.BadRequest("Variant could not be created.");
        return row;
    }

    public async Task<ProductVariantResponse> UpdateVariantAsync(int variantId, VariantUpdateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<ProductVariantResponse>(
            "usp_Variant_Update",
            new
            {
                VariantId = variantId,
                request.Sku,
                Name = request.Name,
                request.Price,
                request.CompareAtPrice,
                request.StockQuantity,
                IsActive = request.IsActive,
                request.LowStockThreshold
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.NotFound("Variant not found.");
        return row;
    }

    public Task DeleteVariantAsync(int variantId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Variant_Delete", new { VariantId = variantId }, cancellationToken);

    public Task<IReadOnlyList<ProductImageResponse>> ListImagesAsync(int productId, CancellationToken cancellationToken) =>
        _db.QueryAsync<ProductImageResponse>("usp_Image_List", new { ProductId = productId }, cancellationToken);

    public async Task<ProductImageResponse> AddImageAsync(int productId, ImageAddRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<ProductImageResponse>(
            "usp_Image_Add",
            new
            {
                ProductId = productId,
                request.Url,
                request.AltText,
                request.DisplayOrder,
                IsPrimary = request.IsPrimary
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.BadRequest("Image could not be added.");
        return row;
    }

    public Task DeleteImageAsync(int imageId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Image_Delete", new { ImageId = imageId }, cancellationToken);

    private sealed class ProductSummaryRow
    {
        public int TotalCount { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public bool IsFeatured { get; set; }
        public int CategoryId { get; set; }
        public int ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string ShopSlug { get; set; } = string.Empty;
        public decimal MinPrice { get; set; }
        public int TotalStock { get; set; }
        public decimal? AvgRating { get; set; }
        public int ReviewCount { get; set; }
    }

    private sealed class ProductRow
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public int ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string ShopSlug { get; set; } = string.Empty;
    }

    private sealed class ProductListRow
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public bool IsFeatured { get; set; }
        public int CategoryId { get; set; }
        public int ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string ShopSlug { get; set; } = string.Empty;
        public decimal MinPrice { get; set; }
        public int TotalStock { get; set; }
        public decimal? AvgRating { get; set; }
        public int ReviewCount { get; set; }
        public bool IsPublished { get; set; }
        public int ModerationStatus { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    private sealed class TotalCountRow
    {
        public int TotalCount { get; set; }
    }

    private sealed class RatingRow
    {
        public int ReviewCount { get; set; }
        public decimal? AvgRating { get; set; }
    }
}
