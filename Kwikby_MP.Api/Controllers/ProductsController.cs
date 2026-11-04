using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/products")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly IProductApiRepository _products;

    public ProductsController(IProductApiRepository products) => _products = products;

    /// <summary>Search published products. Public.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProductSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProductSummaryResponse>>> Search(
        [FromQuery] ProductQuery query, CancellationToken cancellationToken) =>
        Ok(await _products.SearchAsync(query, cancellationToken));

    /// <summary>One published product with variants, images and rating. Public.</summary>
    [AllowAnonymous]
    [HttpGet("{productId:int}")]
    [ProducesResponseType(typeof(ProductDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailResponse>> Get(
        int productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetAsync(productId, cancellationToken);
        return product is null
            ? NotFoundProblem("Product not found or not published.")
            : Ok(product);
    }

    /// <summary>Admin/vendor product list, including unpublished and deleted rows.</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpGet("admin")]
    [ProducesResponseType(typeof(PagedResponse<ProductListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProductListItemResponse>>> ListAdmin(
        [FromQuery] ProductListQuery query, CancellationToken cancellationToken)
    {
        // Vendors are scoped to their own shops; admins see everything.
        if (!User.IsInRole("Admin"))
        {
            var shopId = User.FindFirst("shop_id")?.Value;
            if (int.TryParse(shopId, out var id)) query.ShopId = id;
            else return Forbid();
        }

        return Ok(await _products.ListAdminAsync(query, cancellationToken));
    }

    /// <summary>Create a product. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPost]
    [ProducesResponseType(typeof(ProductDetailResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductDetailResponse>> Create(
        [FromBody] ProductCreateRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var shopId = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(shopId, out var id) || id != request.ShopId) return Forbid();
        }

        var created = await _products.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { productId = created.ProductId }, created);
    }

    /// <summary>Update a product. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPut("{productId:int}")]
    [ProducesResponseType(typeof(ProductDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductDetailResponse>> Update(
        int productId, [FromBody] ProductUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var existing = await _products.GetAsync(productId, cancellationToken);
            if (existing is null) return NotFoundProblem("Product not found.");
            var shopId = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(shopId, out var id) || id != existing.ShopId) return Forbid();
        }

        return Ok(await _products.UpdateAsync(productId, request, cancellationToken));
    }

    /// <summary>Soft-delete a product. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpDelete("{productId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int productId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var existing = await _products.GetAsync(productId, cancellationToken);
            if (existing is null) return NotFoundProblem("Product not found.");
            var shopId = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(shopId, out var id) || id != existing.ShopId) return Forbid();
        }

        await _products.SoftDeleteAsync(productId, cancellationToken);
        return NoContent();
    }

    /// <summary>Publish/unpublish a product. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPatch("{productId:int}/publish")]
    [ProducesResponseType(typeof(ProductDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductDetailResponse>> SetPublished(
        int productId, [FromBody] ProductPublishRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var existing = await _products.GetAsync(productId, cancellationToken);
            if (existing is null) return NotFoundProblem("Product not found.");
            var shopId = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(shopId, out var id) || id != existing.ShopId) return Forbid();
        }

        await _products.SetPublishedAsync(productId, request.IsPublished, cancellationToken);
        var updated = await _products.GetAsync(productId, cancellationToken);
        return updated is null ? NotFoundProblem("Product not found.") : Ok(updated);
    }

    /// <summary>Set moderation status. Admin only.</summary>
    [Authorize(Policy = "ApiAdmin")]
    [HttpPatch("{productId:int}/moderation")]
    [ProducesResponseType(typeof(ProductDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductDetailResponse>> SetModeration(
        int productId, [FromBody] ProductModerationRequest request, CancellationToken cancellationToken)
    {
        await _products.SetModerationAsync(productId, request.ModerationStatus, request.Reason, cancellationToken);
        var updated = await _products.GetAsync(productId, cancellationToken);
        return updated is null ? NotFoundProblem("Product not found.") : Ok(updated);
    }

    /// <summary>List variants for a product. Public.</summary>
    [AllowAnonymous]
    [HttpGet("{productId:int}/variants")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductVariantResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductVariantResponse>>> ListVariants(
        int productId, CancellationToken cancellationToken) =>
        Ok(await _products.ListVariantsAsync(productId, cancellationToken));

    /// <summary>Add a variant. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPost("{productId:int}/variants")]
    [ProducesResponseType(typeof(ProductVariantResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductVariantResponse>> CreateVariant(
        int productId, [FromBody] VariantCreateRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var existing = await _products.GetAsync(productId, cancellationToken);
            if (existing is null) return NotFoundProblem("Product not found.");
            var shopId = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(shopId, out var id) || id != existing.ShopId) return Forbid();
        }

        var created = await _products.CreateVariantAsync(productId, request, cancellationToken);
        return CreatedAtAction(nameof(ListVariants), new { productId }, created);
    }

    /// <summary>Update a variant. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPut("variants/{variantId:int}")]
    [ProducesResponseType(typeof(ProductVariantResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductVariantResponse>> UpdateVariant(
        int variantId, [FromBody] VariantUpdateRequest request, CancellationToken cancellationToken) =>
        Ok(await _products.UpdateVariantAsync(variantId, request, cancellationToken));

    /// <summary>Delete a variant not referenced by orders. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpDelete("variants/{variantId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteVariant(int variantId, CancellationToken cancellationToken)
    {
        await _products.DeleteVariantAsync(variantId, cancellationToken);
        return NoContent();
    }

    /// <summary>List images for a product. Public.</summary>
    [AllowAnonymous]
    [HttpGet("{productId:int}/images")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductImageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductImageResponse>>> ListImages(
        int productId, CancellationToken cancellationToken) =>
        Ok(await _products.ListImagesAsync(productId, cancellationToken));

    /// <summary>Add an image. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpPost("{productId:int}/images")]
    [ProducesResponseType(typeof(ProductImageResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductImageResponse>> AddImage(
        int productId, [FromBody] ImageAddRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            var existing = await _products.GetAsync(productId, cancellationToken);
            if (existing is null) return NotFoundProblem("Product not found.");
            var shopId = User.FindFirst("shop_id")?.Value;
            if (!int.TryParse(shopId, out var id) || id != existing.ShopId) return Forbid();
        }

        var created = await _products.AddImageAsync(productId, request, cancellationToken);
        return CreatedAtAction(nameof(ListImages), new { productId }, created);
    }

    /// <summary>Delete an image. Admin or vendor (own shop).</summary>
    [Authorize(Policy = "ApiVendor")]
    [HttpDelete("images/{imageId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteImage(int imageId, CancellationToken cancellationToken)
    {
        await _products.DeleteImageAsync(imageId, cancellationToken);
        return NoContent();
    }
}
