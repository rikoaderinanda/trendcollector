using Npgsql;

namespace TrendCollector.Api.Data;

/// <summary>
/// Creates Npgsql connections from the configured PostgreSQL connection string.
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Opens and returns a new PostgreSQL connection.
    /// </summary>
    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
