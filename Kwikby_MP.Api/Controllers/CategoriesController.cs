using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/categories")]
public sealed class CategoriesController : ApiControllerBase
{
    private readonly ICategoryApiRepository _categories;

    public CategoriesController(ICategoryApiRepository categories) => _categories = categories;

    /// <summary>Active categories, ordered for display. Public.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var canSeeInactive = includeInactive && User.IsInRole("Admin");
        return Ok(await _categories.ListAsync(canSeeInactive, cancellationToken));
    }

    /// <summary>One category, including inactive ones for admins.</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryResponse>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await _categories.GetAsync(id, cancellationToken));

    /// <summary>Create a global category. Admin only.</summary>
    [Authorize(Policy = "ApiAdmin")]
    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryResponse>> Create(
        [FromBody] CategoryCreateRequest request, CancellationToken cancellationToken)
    {
        var created = await _categories.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Update a category. Admin only.</summary>
    [Authorize(Policy = "ApiAdmin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryResponse>> Update(
        int id, [FromBody] CategoryUpdateRequest request, CancellationToken cancellationToken) =>
        Ok(await _categories.UpdateAsync(id, request, cancellationToken));

    /// <summary>Delete a category that is not in use. Admin only.</summary>
    [Authorize(Policy = "ApiAdmin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _categories.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
