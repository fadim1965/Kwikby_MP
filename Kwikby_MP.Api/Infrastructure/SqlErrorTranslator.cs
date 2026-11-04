using Microsoft.Data.SqlClient;

namespace kwikby_MP.Api.Infrastructure;

/// <summary>
/// Maps SQL Server errors to safe HTTP outcomes. Custom raise numbers are documented in
/// Database/04_ApiStoredProcedures.sql and docs/API_GUIDE.md.
/// </summary>
public static class SqlErrorTranslator
{
    public static ApiException Translate(SqlException exception) =>
        Translate(exception.Number, exception.Message, exception);

    /// <summary>Number-only overload, used by tests and by callers that already have the pieces.</summary>
    public static ApiException Translate(int number, string message, Exception? inner = null)
    {
        return number switch
        {
            // Validation
            52001 or 52002 => new ApiException(400, "Invalid request", message, "validation", inner),

            // Missing row
            52003 => new ApiException(404, "Not found", message, "not_found", inner),

            // Business conflicts
            52004 or 52005 or 52006 or 52008 =>
                new ApiException(409, "Conflict", message, "conflict", inner),

            // Scope / permission
            52007 => new ApiException(403, "Forbidden", message, "forbidden", inner),

            // Unique / foreign key constraint
            2627 or 2601 => new ApiException(409, "Conflict",
                "A record with the same key already exists.", "duplicate", inner),
            547 => new ApiException(409, "Conflict",
                "The operation would break a related record.", "relation", inner),

            // Transient infrastructure
            1205 => new ApiException(503, "Database busy",
                "The database is busy. Retry the request.", "db.deadlock", inner),
            -2 => new ApiException(504, "Database timeout",
                "The database did not respond in time.", "db.timeout", inner),
            4060 or 18456 or 233 or 10054 or 53 or 64 =>
                new ApiException(503, "Database unavailable",
                    "The database is unavailable. Try again shortly.", "db.unavailable", inner),

            _ => new ApiException(500, "Database error",
                "The database could not complete the request.", "db.error", inner)
        };
    }
}
