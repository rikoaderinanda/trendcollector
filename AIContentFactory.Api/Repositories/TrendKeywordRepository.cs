using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="ITrendKeywordRepository" />
public sealed class TrendKeywordRepository : ITrendKeywordRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public TrendKeywordRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> UpsertAsync(TrendKeyword keyword, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO trend_keywords (keyword, niche, country, language, priority, discovery_reason, source, status)
            VALUES (@Keyword, @Niche, @Country, @Language, @Priority, @DiscoveryReason, @Source, @Status)
            ON CONFLICT (keyword, country, language)
            DO UPDATE SET
                niche            = EXCLUDED.niche,
                priority         = EXCLUDED.priority,
                discovery_reason = EXCLUDED.discovery_reason,
                source           = EXCLUDED.source,
                status           = EXCLUDED.status,
                updated_at       = now()
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql,
            new
            {
                keyword.Keyword,
                keyword.Niche,
                keyword.Country,
                keyword.Language,
                keyword.Priority,
                keyword.DiscoveryReason,
                keyword.Source,
                keyword.Status
            }, commandTimeout: 30);
    }

    public async Task<IEnumerable<TrendKeyword>> ListAsync(
        string? country,
        string? language,
        string? niche,
        int? minPriority,
        string? status,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id               AS Id,
                keyword          AS Keyword,
                niche            AS Niche,
                country          AS Country,
                language         AS Language,
                priority         AS Priority,
                discovery_reason AS DiscoveryReason,
                source           AS Source,
                status           AS Status,
                created_at       AS CreatedAt,
                updated_at       AS UpdatedAt
            FROM trend_keywords
            WHERE (@Country IS NULL OR country = @Country)
              AND (@Language IS NULL OR language = @Language)
              AND (@Niche IS NULL OR niche = @Niche)
              AND (@MinPriority IS NULL OR priority >= @MinPriority)
              AND (@Status IS NULL OR status = @Status)
            ORDER BY priority DESC, created_at DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        return await connection.QueryAsync<TrendKeyword>(sql,
            new { Country = country, Language = language, Niche = niche, MinPriority = minPriority, Status = status, Limit = limit, Offset = offset },
            commandTimeout: 30);
    }

    public async Task<bool> ExistsAsync(string keyword, string country, string language, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM trend_keywords
                WHERE keyword = @Keyword AND country = @Country AND language = @Language
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql,
            new { Keyword = keyword, Country = country, Language = language }, commandTimeout: 30);
    }

    public async Task UpdateStatusAsync(long id, string status, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE trend_keywords
            SET status = @Status,
                updated_at = now()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql,
            new { Id = id, Status = status }, commandTimeout: 30);
    }
}

