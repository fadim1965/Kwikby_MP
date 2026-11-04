using Microsoft.Data.SqlClient;

namespace kwikby_MP.Api.Infrastructure;

public interface ISqlConnectionFactory
{
    /// <summary>False when no SQL Server connection string is configured; callers should return 503.</summary>
    bool IsConfigured { get; }

    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Opens a new <see cref="SqlConnection"/> per stored-procedure call. ADO.NET pooling does the
/// expensive part; the API never shares a connection between requests.
/// </summary>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string? _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
        => _connectionString = configuration.GetConnectionString("MultiVendorsStoresConn");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw ApiException.DatabaseNotConfigured();
        }

        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
