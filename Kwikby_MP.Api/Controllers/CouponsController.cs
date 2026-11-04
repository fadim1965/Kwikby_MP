using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/coupons")]
public sealed class CouponsController : ApiControllerBase
{
    private readonly ICouponApiRepository _coupons;

    public CouponsController(ICouponApiRepository coupons) => _coupons = coupons;

    [Authorize(Policy = "ApiAdmin")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CouponResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CouponResponse>>> List(
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken) =>
        Ok(await _coupons.ListAsync(page, pageSize, cancellationToken));

    [Authorize(Policy = "ApiAdmin")]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CouponResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CouponResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var coupon = await _coupons.GetAsync(id, cancellationToken);
        return coupon is null ? NotFoundProblem("Coupon not found.") : Ok(coupon);
    }

    [Authorize(Policy = "ApiAdmin")]
    [HttpPost]
    [ProducesResponseType(typeof(CouponResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CouponResponse>> Create(
        [FromBody] CouponCreateRequest request, CancellationToken cancellationToken)
    {
        var created = await _coupons.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [Authorize(Policy = "ApiAdmin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CouponResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CouponResponse>> Update(
        int id, [FromBody] CouponUpdateRequest request, CancellationToken cancellationToken) =>
        Ok(await _coupons.UpdateAsync(id, request, cancellationToken));

    [Authorize(Policy = "ApiAdmin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _coupons.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
