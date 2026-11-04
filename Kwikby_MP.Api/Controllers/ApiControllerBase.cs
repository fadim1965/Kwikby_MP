using kwikby_MP.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace kwikby_MP.Api.Controllers;

/// <summary>
/// Every API controller requires a JWT by default and is rate limited. Public
/// read endpoints opt out of authorization with <see cref="AllowAnonymousAttribute"/>.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting(ApiRateLimitPolicies.Api)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Builds an RFC 7807 response with an explicit <c>application/problem+json</c>
    /// content type. ControllerBase.Problem() leaves content negotiation to the
    /// formatters, which pick <c>application/json</c> unless told otherwise.
    /// </summary>
    protected ObjectResult ProblemResult(int statusCode, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext?.Request.Path
        };
        problem.Extensions["traceId"] = HttpContext?.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    protected ObjectResult NotFoundProblem(string detail) =>
        ProblemResult(StatusCodes.Status404NotFound, "Not found", detail);

    protected ObjectResult ForbiddenProblem(string detail) =>
        ProblemResult(StatusCodes.Status403Forbidden, "Forbidden", detail);

    protected ObjectResult UnauthorizedProblem(string detail) =>
        ProblemResult(StatusCodes.Status401Unauthorized, "Unauthorized", detail);
}
