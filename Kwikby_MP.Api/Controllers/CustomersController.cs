using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/customers")]
public sealed class CustomersController : ApiControllerBase
{
    private readonly ICustomerApiRepository _customers;

    public CustomersController(ICustomerApiRepository customers) => _customers = customers;

    [Authorize(Policy = "ApiAdmin")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> List(
        [FromQuery] string? query, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken) =>
        Ok(await _customers.ListAsync(query, page, pageSize, cancellationToken));

    [Authorize(Policy = "ApiAdmin")]
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> Get(string userId, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAsync(userId, cancellationToken);
        return customer is null ? NotFoundProblem("Customer not found.") : Ok(customer);
    }
}
