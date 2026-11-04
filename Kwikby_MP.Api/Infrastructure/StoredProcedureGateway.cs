using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace kwikby_MP.Api.Infrastructure;

public interface IStoredProcedureGateway
{
    Task<IReadOnlyList<T>> QueryAsync<T>(string procedure, object? parameters, CancellationToken cancellationToken);

    Task<T?> QuerySingleOrDefaultAsync<T>(string procedure, object? parameters, CancellationToken cancellationToken);

    Task<T?> ExecuteScalarAsync<T>(string procedure, object? parameters, CancellationToken cancellationToken);

    Task<int> ExecuteAsync(string procedure, object? parameters, CancellationToken cancellationToken);

    /// <summary>
    /// Calls a procedure that returns several result sets. The caller owns the returned
    /// object and must dispose it (which closes the connection).
    /// </summary>
    Task<IMultiResult> QueryMultipleAsync(string procedure, object? parameters, CancellationToken cancellationToken);
}

public interface IMultiResult : IAsyncDisposable
{
    Task<IReadOnlyList<T>> ReadAsync<T>();

    Task<T?> ReadSingleOrDefaultAsync<T>();

    Task<T> ReadSingleAsync<T>();
}

public sealed class StoredProcedureGateway : IStoredProcedureGateway
{
    private readonly ISqlConnectionFactory _connections;
    private readonly ApiOptions _options;
    private readonly ILogger<StoredProcedureGateway> _logger;

    public StoredProcedureGateway(
        ISqlConnectionFactory connections,
        IOptions<ApiOptions> options,
        ILogger<StoredProcedureGateway> logger)
    {
        _connections = connections;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string procedure, object? parameters, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = Command(procedure, parameters, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var rows = await connection.QueryAsync<T>(command).ConfigureAwait(false);
        stopwatch.Stop();
        LogIfSlow(procedure, stopwatch.ElapsedMilliseconds);
        return rows.AsList();
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string procedure, object? parameters, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = Command(procedure, parameters, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var row = await connection.QuerySingleOrDefaultAsync<T>(command).ConfigureAwait(false);
        stopwatch.Stop();
        LogIfSlow(procedure, stopwatch.ElapsedMilliseconds);
        return row;
    }

    public async Task<T?> ExecuteScalarAsync<T>(string procedure, object? parameters, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = Command(procedure, parameters, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var value = await connection.ExecuteScalarAsync<T>(command).ConfigureAwait(false);
        stopwatch.Stop();
        LogIfSlow(procedure, stopwatch.ElapsedMilliseconds);
        return value;
    }

    public async Task<int> ExecuteAsync(string procedure, object? parameters, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = Command(procedure, parameters, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);
        stopwatch.Stop();
        LogIfSlow(procedure, stopwatch.ElapsedMilliseconds);
        return affected;
    }

    public async Task<IMultiResult> QueryMultipleAsync(string procedure, object? parameters, CancellationToken cancellationToken)
    {
        var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var command = Command(procedure, parameters, cancellationToken);
            var stopwatch = Stopwatch.StartNew();
            var reader = await connection.QueryMultipleAsync(command).ConfigureAwait(false);
            stopwatch.Stop();
            LogIfSlow(procedure, stopwatch.ElapsedMilliseconds);
            return new MultiResult(connection, reader, _logger, procedure);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void LogIfSlow(string procedure, long elapsedMilliseconds)
    {
        if (elapsedMilliseconds >= _options.SlowCommandMs)
        {
            _logger.LogWarning(
                "Stored procedure {Procedure} took {ElapsedMilliseconds} ms (threshold {ThresholdMs} ms).",
                procedure, elapsedMilliseconds, _options.SlowCommandMs);
        }
    }

    private CommandDefinition Command(string procedure, object? parameters, CancellationToken cancellationToken)
    {
        return new CommandDefinition(
            procedure,
            parameters,
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: _options.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);
    }

    private sealed class MultiResult : IMultiResult
    {
        private readonly SqlConnection _connection;
        private readonly SqlMapper.GridReader _reader;
        private readonly ILogger _logger;
        private readonly string _procedure;

        public MultiResult(SqlConnection connection, SqlMapper.GridReader reader, ILogger logger, string procedure)
        {
            _connection = connection;
            _reader = reader;
            _logger = logger;
            _procedure = procedure;
        }

        public async Task<IReadOnlyList<T>> ReadAsync<T>()
        {
            var rows = await _reader.ReadAsync<T>().ConfigureAwait(false);
            return rows.AsList();
        }

        public Task<T?> ReadSingleOrDefaultAsync<T>() => _reader.ReadSingleOrDefaultAsync<T>();

        public Task<T> ReadSingleAsync<T>() => _reader.ReadSingleAsync<T>();

        public async ValueTask DisposeAsync()
        {
            try
            {
                _reader.Dispose();
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Disposing the result reader for {Procedure} failed.", _procedure);
            }

            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
