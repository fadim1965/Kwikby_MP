using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/shops")]
public sealed class ShopsController : ApiControllerBase
{
    private readonly IShopApiRepository _shops;

    public ShopsController(IShopApiRepository shops) => _shops = shops;

    /// <summary>Approved shops. Public.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ShopResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShopResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _shops.ListAsync(cancellationToken));

    /// <summary>One approved shop. Public.</summary>
    [AllowAnonymous]
    [HttpGet("{shopId:int}")]
    [ProducesResponseType(typeof(ShopResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopResponse>> Get(int shopId, CancellationToken cancellationToken)
    {
        var shop = await _shops.GetAsync(shopId, cancellationToken);
        return shop is null ? NotFoundProblem("Shop not found or not approved.") : Ok(shop);
    }

    /// <summary>Create a shop. Customer or admin.</summary>
    [Authorize(Policy = "ApiCustomer")]
    [HttpPost]
    [ProducesResponseType(typeof(ShopResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ShopResponse>> Create(
        [FromBody] ShopCreateRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var userId = User.FindFirst("sub")?.Value;
            if (userId is null || userId != request.OwnerUserId) return Forbid();
        }

        var created = await _shops.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { shopId = created.Id }, created);
    }

    /// <summary>Update a shop. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPut("{shopId:int}")]
    [ProducesResponseType(typeof(ShopResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShopResponse>> Update(
        int shopId, [FromBody] ShopUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(claim, out var id) || id != shopId) return Forbid();
        }

        return Ok(await _shops.UpdateAsync(shopId, request, cancellationToken));
    }

    /// <summary>Approve/reject/suspend a shop. Admin only.</summary>
    [Authorize(Policy = "ApiAdmin")]
    [HttpPost("{shopId:int}/status")]
    [ProducesResponseType(typeof(ShopResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShopResponse>> SetStatus(
        int shopId, [FromBody] ShopStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await _shops.SetStatusAsync(shopId, request, cancellationToken));

    /// <summary>Shop team members. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpGet("{shopId:int}/team")]
    [ProducesResponseType(typeof(IReadOnlyList<ShopTeamMemberResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShopTeamMemberResponse>>> ListTeam(
        int shopId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(claim, out var id) || id != shopId) return Forbid();
        }

        return Ok(await _shops.ListTeamAsync(shopId, cancellationToken));
    }

    /// <summary>Add a team member. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPost("{shopId:int}/team")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddTeamMember(
        int shopId, [FromBody] ShopTeamAddRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(claim, out var id) || id != shopId) return Forbid();
        }

        await _shops.AddTeamMemberAsync(shopId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Remove a team member. Vendor (own shop) or admin.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpDelete("{shopId:int}/team/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTeamMember(
        int shopId, string userId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var claim = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(claim, out var id) || id != shopId) return Forbid();
        }

        await _shops.RemoveTeamMemberAsync(shopId, userId, cancellationToken);
        return NoContent();
    }
}
