using System.Text;
using Dapper;
using Npgsql;

namespace AIContentFactory.Api.Data;

/// <summary>
/// Applies the database schema on startup.
/// The schema SQL must be idempotent (CREATE TABLE IF NOT EXISTS).
///
/// The script is executed statement-by-statement rather than as one big
/// batch. This is important because PostgreSQL aborts the *entire* batch
/// (rolling back earlier statements) when any single statement fails. When a
/// database predates a column that was added later (e.g. the
/// <c>collection_jobs.mode</c> migration), an index referencing the missing
/// column used to abort the batch before the <c>ALTER TABLE ... ADD COLUMN</c>
/// ever ran - leaving the column permanently missing. Executing each
/// statement independently lets every idempotent statement succeed on its own.
/// </summary>
public sealed class DbInitializer
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ILogger<DbInitializer> _logger;
    private readonly string _schemaPath;

    private const int MaxRetries = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// PostgreSQL SQLSTATE codes that mean the server/connection itself is the
    /// problem (as opposed to a real SQL error we should surface).
    /// </summary>
    private static readonly HashSet<string> ConnectionErrorStates = new(StringComparer.Ordinal)
    {
        // connection_exception / connection_does_not_exist / connection_failure
        "08000", "08001", "08003", "08004", "08006", "08007",
        // cannot_connect_now, too_many_connections
        "57P03", "53300"
    };

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
    /// Real SQL errors are logged as errors (not mistaken for an unavailable
    /// database) and, because statements run independently, a single failed
    /// statement does not block the remaining idempotent migrations.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_schemaPath))
        {
            _logger.LogWarning("Schema file not found at {SchemaPath}. Skipping schema initialization.", _schemaPath);
            return;
        }

        var sql = await File.ReadAllTextAsync(_schemaPath, cancellationToken);
        var statements = SplitStatements(sql);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                var failures = 0;
                foreach (var statement in statements)
                {
                    try
                    {
                        await connection.ExecuteAsync(statement, commandTimeout: 60);
                    }
                    catch (PostgresException ex) when (IsConnectionError(ex))
                    {
                        // Server/connection-level problem: abort and let the
                        // outer retry loop decide whether to reconnect.
                        throw;
                    }
                    catch (NpgsqlException ex)
                    {
                        failures++;
                        _logger.LogError(ex,
                            "Schema statement failed (continuing with remaining statements). SQL: {Sql}",
                            ShortenForLog(statement));
                    }
                }

                if (failures == 0)
                {
                    _logger.LogInformation("Database schema applied successfully.");
                }
                else
                {
                    _logger.LogError(
                        "{Failures} schema statement(s) failed to apply. " +
                        "Database-backed endpoints may not work correctly until the schema is repaired.",
                        failures);
                }
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

    private static bool IsConnectionError(PostgresException ex) =>
        ConnectionErrorStates.Contains(ex.SqlState);

    /// <summary>
    /// Splits a SQL script into individual statements, honouring single-quoted
    /// strings, double-quoted identifiers, dollar-quoted bodies and comments.
    /// </summary>
    internal static IReadOnlyList<string> SplitStatements(string sql)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var i = 0;
        var length = sql.Length;

        while (i < length)
        {
            var c = sql[i];

            // Line comment: -- ...
            if (c == '-' && i + 1 < length && sql[i + 1] == '-')
            {
                while (i < length && sql[i] != '\n') i++;
                continue;
            }

            // Block comment: /* ... */
            if (c == '/' && i + 1 < length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i = Math.Min(i + 2, length);
                continue;
            }

            // Single-quoted string literal.
            if (c == '\'')
            {
                current.Append(c);
                i++;
                while (i < length)
                {
                    current.Append(sql[i]);
                    if (sql[i] == '\'')
                    {
                        // Handle escaped quote '' inside the literal.
                        if (i + 1 < length && sql[i + 1] == '\'')
                        {
                            current.Append(sql[i + 1]);
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            // Double-quoted identifier.
            if (c == '"')
            {
                current.Append(c);
                i++;
                while (i < length)
                {
                    current.Append(sql[i]);
                    if (sql[i] == '"')
                    {
                        if (i + 1 < length && sql[i + 1] == '"')
                        {
                            current.Append(sql[i + 1]);
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            // Dollar-quoted body, e.g. $$ ... $$ or $tag$ ... $tag$.
            if (c == '$')
            {
                var tag = ReadDollarTag(sql, i);
                if (tag is not null)
                {
                    current.Append(tag);
                    i += tag.Length;
                    var end = sql.IndexOf(tag, i, StringComparison.Ordinal);
                    var endPos = end < 0 ? length : end + tag.Length;
                    var body = sql[i..endPos];
                    current.Append(body);
                    i = endPos;
                    continue;
                }
            }

            // Statement terminator.
            if (c == ';')
            {
                var statement = current.ToString().Trim();
                if (statement.Length > 0)
                {
                    statements.Add(statement);
                }
                current.Clear();
                i++;
                continue;
            }

            current.Append(c);
            i++;
        }

        var trailing = current.ToString().Trim();
        if (trailing.Length > 0)
        {
            statements.Add(trailing);
        }

        return statements;
    }

    private static string? ReadDollarTag(string sql, int start)
    {
        if (sql[start] != '$') return null;

        var i = start + 1;
        while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_')) i++;

        if (i < sql.Length && sql[i] == '$')
        {
            return sql[start..(i + 1)];
        }

        return null;
    }

    private static string ShortenForLog(string statement)
    {
        const int maxLength = 500;
        return statement.Length <= maxLength
            ? statement
            : statement[..maxLength] + "...(truncated)";
    }
}