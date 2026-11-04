using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/reviews")]
public sealed class ReviewsController : ApiControllerBase
{
    private readonly IReviewApiRepository _reviews;

    public ReviewsController(IReviewApiRepository reviews) => _reviews = reviews;

    [Authorize(Policy = "ApiAdmin")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ReviewResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ReviewResponse>>> List(
        [FromQuery] int? productId, [FromQuery] int? status, [FromQuery] int page, [FromQuery] int pageSize,
        CancellationToken cancellationToken) =>
        Ok(await _reviews.ListAsync(productId, status, page, pageSize, cancellationToken));

    [Authorize(Policy = "ApiCustomer")]
    [HttpPost]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReviewResponse>> Create(
        [FromBody] ReviewCreateRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        var created = await _reviews.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(List), new { productId = request.ProductId }, created);
    }
    [Authorize(Policy = "ApiAdmin")]
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] int status, CancellationToken cancellationToken)
    {
        await _reviews.SetStatusAsync(id, status, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "ApiAdmin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _reviews.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
