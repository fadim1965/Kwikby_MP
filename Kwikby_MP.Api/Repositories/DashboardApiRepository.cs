using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Responses;

namespace kwikby_MP.Api.Repositories;

public interface IDashboardApiRepository
{
    Task<ShopDashboardResponse> GetShopAsync(int shopId, int days, CancellationToken cancellationToken);

    Task<PlatformDashboardResponse> GetPlatformAsync(int days, CancellationToken cancellationToken);
}

public sealed class DashboardApiRepository : IDashboardApiRepository
{
    private const int DefaultDays = 30;
    private const int MaxDays = 3_650;

    private readonly IStoredProcedureGateway _db;

    public DashboardApiRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<ShopDashboardResponse> GetShopAsync(
        int shopId, int days, CancellationToken cancellationToken)
    {
        days = ClampDays(days);

        await using var result = await _db
            .QueryMultipleAsync("usp_GetShopDashboard", new { ShopId = shopId, Days = days }, cancellationToken)
            .ConfigureAwait(false);

        var figures = await result.ReadSingleOrDefaultAsync<ShopDashboardFigures>().ConfigureAwait(false)
                      ?? new ShopDashboardFigures();
        var topProducts = await result.ReadAsync<TopProductResponse>().ConfigureAwait(false);

        return new ShopDashboardResponse
        {
            Days = days,
            Figures = figures,
            TopProducts = topProducts
        };
    }

    public async Task<PlatformDashboardResponse> GetPlatformAsync(
        int days, CancellationToken cancellationToken)
    {
        days = ClampDays(days);

        var dashboard = await _db
            .QuerySingleOrDefaultAsync<PlatformDashboardResponse>(
                "usp_GetPlatformDashboard", new { Days = days }, cancellationToken)
            .ConfigureAwait(false);

        dashboard ??= new PlatformDashboardResponse();
        dashboard.Days = days;
        return dashboard;
    }

    private static int ClampDays(int days) =>
        days switch
        {
            < 1 => DefaultDays,
            > MaxDays => MaxDays,
            _ => days
        };
}
