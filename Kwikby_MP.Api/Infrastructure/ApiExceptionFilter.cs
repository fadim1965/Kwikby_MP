using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace kwikby_MP.Api.Infrastructure;

/// <summary>
/// Converts every escape from a controller/repository into RFC 7807. Known
/// <see cref="ApiException"/>s and SQL errors keep their meaning; everything else becomes a
/// generic 500 with the trace id, and the full exception is logged once.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;
        var apiException = exception switch
        {
            ApiException known => known,
            SqlException sql => SqlErrorTranslator.Translate(sql),
            _ => null
        };

        if (apiException is null)
        {
            _logger.LogError(exception, "Unhandled API exception for {Method} {Path}",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            apiException = new ApiException(
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred. Contact support with the trace id.",
                "unexpected",
                exception);
        }
        else if (apiException.StatusCode >= 500)
        {
            _logger.LogError(exception, "API error {Status} for {Method} {Path}",
                apiException.StatusCode, context.HttpContext.Request.Method, context.HttpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation(exception, "API rejected {Status} for {Method} {Path}",
                apiException.StatusCode, context.HttpContext.Request.Method, context.HttpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = apiException.StatusCode,
            Title = apiException.Title,
            Detail = apiException.Message,
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = apiException.Code;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        context.Result = new ObjectResult(problem)
        {
            StatusCode = apiException.StatusCode,
            ContentTypes = { "application/problem+json" }
        };
        context.ExceptionHandled = true;
    }
}
