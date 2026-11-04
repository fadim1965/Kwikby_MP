using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardApiRepository _dashboards;

    public DashboardController(IDashboardApiRepository dashboards) => _dashboards = dashboards;

    /// <summary>Platform KPIs. Admin only.</summary>
    [HttpGet("admin/dashboard")]
    [Authorize(Policy = ApiPolicies.Admin)]
    [ProducesResponseType(typeof(PlatformDashboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformDashboardResponse>> Platform(
        [FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        Ok(await _dashboards.GetPlatformAsync(days, cancellationToken));

    /// <summary>KPIs for one shop. Admin, or a member of that shop.</summary>
    [HttpGet("shops/{shopId:int}/dashboard")]
    [Authorize(Policy = ApiPolicies.Vendor)]
    [ProducesResponseType(typeof(ShopDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShopDashboardResponse>> Shop(
        int shopId, [FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole("Admin"))
        {
            var hasShop = User.FindAll("shop_id").Any(claim => claim.Value == shopId.ToString());
            if (!hasShop)
            {
                return ForbiddenProblem("This token does not belong to that shop.");
            }
        }

        return Ok(await _dashboards.GetShopAsync(shopId, days, cancellationToken));
    }
}
