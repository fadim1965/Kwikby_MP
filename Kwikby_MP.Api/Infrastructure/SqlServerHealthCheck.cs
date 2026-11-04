using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace kwikby_MP.Api.Infrastructure;

/// <summary>
/// Readiness check for the API's only dependency: the SQL Server database the
/// stored procedures live in. A missing connection string is unhealthy here, but it
/// does not stop the host from starting (Swagger and the 503 responses still work).
/// </summary>
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly ISqlConnectionFactory _connections;

    public SqlServerHealthCheck(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return HealthCheckResult.Unhealthy(
                "ConnectionStrings:MultiVendorsStoresConn is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("SQL Server answered SELECT 1.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server is unreachable.", exception);
        }
    }
}
