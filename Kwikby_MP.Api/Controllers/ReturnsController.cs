using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/returns")]
public sealed class ReturnsController : ApiControllerBase
{
    private readonly IReturnApiRepository _returns;

    public ReturnsController(IReturnApiRepository returns) => _returns = returns;

    [Authorize(Policy = "ApiCustomer")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ReturnResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ReturnResponse>>> List(
        [FromQuery] int? shopId, [FromQuery] string? userId, [FromQuery] int? status,
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            if (User.IsInRole("Customer") && !User.IsInRole("Admin"))
            {
                userId = User.FindFirst("sub")?.Value;
                shopId = null;
            }
            else
            {
                var claim = User.FindFirst("shop_id")?.Value;
                if (int.TryParse(claim, out var id)) shopId = id;
            }
        }

        return Ok(await _returns.ListAsync(shopId, userId, status, page, pageSize, cancellationToken));
    }

    [Authorize(Policy = "ApiVendor")]
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] int status, CancellationToken cancellationToken)
    {
        await _returns.SetStatusAsync(id, status, cancellationToken);
        return NoContent();
    }
}
