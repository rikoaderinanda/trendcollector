using Dapper;
using Npgsql;

namespace AIContentFactory.Api.Data;

/// <summary>
/// Applies the database schema on startup.
/// The schema SQL must be idempotent (CREATE TABLE IF NOT EXISTS).
/// If PostgreSQL is not reachable yet, retries a few times before logging a
/// warning and letting the app start anyway (database features become
/// available once the database is up).
/// </summary>
public sealed class DbInitializer
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ILogger<DbInitializer> _logger;
    private readonly string _schemaPath;

    private const int MaxRetries = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public DbInitializer(
        DbConnectionFactory connectionFactory,
        ILogger<DbInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _schemaPath = Path.Combine(AppContext.BaseDirectory, "SQL", "schema.sql");
    }

    /// <summary>
    /// Executes the schema SQL script if present. Never throws when the
    /// database is unavailable - logs a warning instead so the app can still
    /// start and serve endpoints that do not depend on PostgreSQL.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_schemaPath))
        {
            _logger.LogWarning("Schema file not found at {SchemaPath}. Skipping schema initialization.", _schemaPath);
            return;
        }

        var sql = await File.ReadAllTextAsync(_schemaPath, cancellationToken);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                await connection.ExecuteAsync(sql, commandTimeout: 60);
                _logger.LogInformation("Database schema applied successfully.");
                return;
            }
            catch (NpgsqlException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "PostgreSQL connection attempt {Attempt}/{MaxRetries} failed: {Message}. Retrying in {Delay}s...",
                    attempt, MaxRetries, ex.Message, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (NpgsqlException ex)
            {
                _logger.LogWarning(
                    ex,
                    "PostgreSQL is not available after {MaxRetries} attempts. Schema migration skipped. " +
                    "The app will start, but database-backed endpoints will fail until PostgreSQL is running.",
                    MaxRetries);
                return;
            }
        }
    }
}