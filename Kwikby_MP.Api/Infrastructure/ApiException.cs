namespace kwikby_MP.Api.Infrastructure;

/// <summary>
/// A deliberate, mapped failure. The exception filter turns this into an RFC 7807
/// ProblemDetails response; the message is safe to show to the caller.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string title, string detail, string? code = null, Exception? inner = null)
        : base(detail, inner)
    {
        StatusCode = statusCode;
        Title = title;
        Code = code;
    }

    public int StatusCode { get; }

    public string Title { get; }

    /// <summary>Short machine-readable code, e.g. <c>not_found</c> or <c>db.not_configured</c>.</summary>
    public string? Code { get; }

    public static ApiException NotFound(string detail) =>
        new(StatusCodes.Status404NotFound, "Not found", detail, "not_found");

    public static ApiException Forbidden(string detail) =>
        new(StatusCodes.Status403Forbidden, "Forbidden", detail, "forbidden");

    public static ApiException BadRequest(string detail) =>
        new(StatusCodes.Status400BadRequest, "Invalid request", detail, "validation");

    public static ApiException Conflict(string detail) =>
        new(StatusCodes.Status409Conflict, "Conflict", detail, "conflict");

    public static ApiException DatabaseNotConfigured() =>
        new(StatusCodes.Status503ServiceUnavailable,
            "Database not configured",
            "ConnectionStrings:MultiVendorsStoresConn is not configured on this API host.",
            "db.not_configured");
}
