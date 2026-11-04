using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/payouts")]
public sealed class PayoutsController : ApiControllerBase
{
    private readonly IPayoutApiRepository _payouts;

    public PayoutsController(IPayoutApiRepository payouts) => _payouts = payouts;

    [Authorize(Policy = "ApiVendor")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PayoutResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PayoutResponse>>> List(
        [FromQuery] int? shopId, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("shop_id")?.Value;
            if (int.TryParse(claim, out var id)) shopId = id;
            else return Forbid();
        }

        return Ok(await _payouts.ListAsync(shopId, page, pageSize, cancellationToken));
    }

    [Authorize(Policy = "ApiAdmin")]
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] int status, CancellationToken cancellationToken)
    {
        await _payouts.SetStatusAsync(id, status, cancellationToken);
        return NoContent();
    }
}
