using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/audit")]
public sealed class AuditController : ApiControllerBase
{
    private readonly IAuditApiRepository _audit;

    public AuditController(IAuditApiRepository audit) => _audit = audit;

    [Authorize(Policy = "ApiAdmin")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> List(
        [FromQuery] string? actor, [FromQuery] string? action, [FromQuery] string? entityType,
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken) =>
        Ok(await _audit.ListAsync(actor, action, entityType, page, pageSize, cancellationToken));
}
