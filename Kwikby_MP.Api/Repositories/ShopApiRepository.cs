using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IShopApiRepository
{
    Task<IReadOnlyList<ShopResponse>> ListAsync(CancellationToken cancellationToken);

    Task<ShopResponse?> GetAsync(int shopId, CancellationToken cancellationToken);

    Task<ShopResponse> CreateAsync(ShopCreateRequest request, CancellationToken cancellationToken);

    Task<ShopResponse> UpdateAsync(int shopId, ShopUpdateRequest request, CancellationToken cancellationToken);

    Task<ShopResponse> SetStatusAsync(int shopId, ShopStatusRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShopTeamMemberResponse>> ListTeamAsync(int shopId, CancellationToken cancellationToken);

    Task AddTeamMemberAsync(int shopId, ShopTeamAddRequest request, CancellationToken cancellationToken);

    Task RemoveTeamMemberAsync(int shopId, string userId, CancellationToken cancellationToken);
}

public sealed class ShopApiRepository : IShopApiRepository
{
    private readonly IStoredProcedureGateway _db;

    public ShopApiRepository(IStoredProcedureGateway db) => _db = db;

    public Task<IReadOnlyList<ShopResponse>> ListAsync(CancellationToken cancellationToken) =>
        _db.QueryAsync<ShopResponse>(
            "usp_Shop_List",
            new { Status = (int?)null, IncludeAll = false },
            cancellationToken);

    public Task<ShopResponse?> GetAsync(int shopId, CancellationToken cancellationToken) =>
        _db.QuerySingleOrDefaultAsync<ShopResponse>(
            "usp_Shop_Get",
            new { ShopId = shopId },
            cancellationToken);

    public async Task<ShopResponse> CreateAsync(ShopCreateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<ShopResponse>(
            "usp_Shop_Create",
            new
            {
                request.OwnerUserId,
                request.Name,
                request.Slug,
                request.Description,
                request.Tagline,
                request.ContactName,
                request.Phone,
                request.Email,
                request.Website,
                request.AddressLine1,
                request.City,
                request.PostalCode,
                request.Country,
                request.PickupLocation,
                request.CommissionRate,
                request.FlatShippingRate,
                request.FreeShippingThreshold,
                request.Currency,
                request.ThemeKey
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.BadRequest("Shop could not be created.");
        return row;
    }

    public async Task<ShopResponse> UpdateAsync(int shopId, ShopUpdateRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<ShopResponse>(
            "usp_Shop_Update",
            new
            {
                ShopId = shopId,
                request.Name,
                request.Slug,
                request.Description,
                request.Tagline,
                request.ContactName,
                request.Phone,
                request.Email,
                request.Website,
                request.AddressLine1,
                request.City,
                request.PostalCode,
                request.Country,
                request.PickupLocation,
                request.CommissionRate,
                request.FlatShippingRate,
                request.FreeShippingThreshold,
                request.Currency,
                request.ThemeKey,
                IsFeatured = request.IsFeatured
            },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.NotFound("Shop not found.");
        return row;
    }

    public async Task<ShopResponse> SetStatusAsync(int shopId, ShopStatusRequest request, CancellationToken cancellationToken)
    {
        var row = await _db.QuerySingleOrDefaultAsync<ShopResponse>(
            "usp_Shop_SetStatus",
            new { ShopId = shopId, Status = request.Status, Reason = request.Reason },
            cancellationToken).ConfigureAwait(false);
        if (row is null) throw Infrastructure.ApiException.NotFound("Shop not found.");
        return row;
    }

    public Task<IReadOnlyList<ShopTeamMemberResponse>> ListTeamAsync(int shopId, CancellationToken cancellationToken) =>
        _db.QueryAsync<ShopTeamMemberResponse>("usp_Shop_User_List", new { ShopId = shopId }, cancellationToken);

    public Task AddTeamMemberAsync(int shopId, ShopTeamAddRequest request, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Shop_User_Add", new { ShopId = shopId, UserId = request.UserId, Role = request.Role }, cancellationToken);

    public Task RemoveTeamMemberAsync(int shopId, string userId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Shop_User_Remove", new { ShopId = shopId, UserId = userId }, cancellationToken);
}
