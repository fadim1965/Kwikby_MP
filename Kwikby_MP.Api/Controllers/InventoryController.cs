using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/inventory")]
public sealed class InventoryController : ApiControllerBase
{
    private readonly IInventoryApiRepository _inventory;

    public InventoryController(IInventoryApiRepository inventory) => _inventory = inventory;

    /// <summary>Stock levels by shop. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StockItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockItemResponse>>> List(
        [FromQuery] int? shopId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(claim, out var id)) return Forbid();
            shopId = id;
        }

        return Ok(await _inventory.ListAsync(shopId, cancellationToken));
    }

    /// <summary>Adjust stock for a variant. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPost("{variantId:int}/adjust")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Adjust(
        int variantId, [FromBody] StockAdjustRequest request, CancellationToken cancellationToken)
    {
        var actor = User.FindFirst("sub")?.Value ?? "api";
        await _inventory.AdjustAsync(variantId, request, actor, cancellationToken);
        return NoContent();
    }

    /// <summary>Stock movement history. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpGet("{variantId:int}/movements")]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockMovementResponse>>> Movements(
        int variantId, [FromQuery] int? shopId, [FromQuery] int page, [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(claim, out var id)) return Forbid();
            shopId = id;
        }

        return Ok(await _inventory.MovementsAsync(variantId, shopId, page, pageSize, cancellationToken));
    }
}
