using Dapper;
using TrendCollector.Api.Configuration;

namespace TrendCollector.Api.Data;

/// <summary>
/// Applies the database schema on startup.
/// The schema SQL must be idempotent (CREATE TABLE IF NOT EXISTS).
/// </summary>
public sealed class DbInitializer
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ILogger<DbInitializer> _logger;
    private readonly string _schemaPath;

    public DbInitializer(
        DbConnectionFactory connectionFactory,
        ILogger<DbInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _schemaPath = Path.Combine(AppContext.BaseDirectory, "SQL", "schema.sql");
    }

    /// <summary>
    /// Executes the schema SQL script if present.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_schemaPath))
        {
            _logger.LogWarning("Schema file not found at {SchemaPath}. Skipping schema initialization.", _schemaPath);
            return;
        }

        var sql = await File.ReadAllTextAsync(_schemaPath, cancellationToken);
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql);

        _logger.LogInformation("Database schema applied successfully.");
    }
}
