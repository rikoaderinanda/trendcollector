using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="ITrendDiscoveryJobRepository" />
public sealed class TrendDiscoveryJobRepository : ITrendDiscoveryJobRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public TrendDiscoveryJobRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> CreateAsync(TrendDiscoveryJob job, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO trend_discovery_jobs (started_at, status, source)
            VALUES (@StartedAt, @Status, @Source)
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql,
            new { job.StartedAt, job.Status, job.Source }, commandTimeout: 30);
    }

    public async Task CompleteAsync(long id, int totalKeywords, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE trend_discovery_jobs
            SET status = @Status,
                finished_at = now(),
                duration_ms = EXTRACT(EPOCH FROM (now() - started_at)) * 1000,
                total_keywords = @TotalKeywords
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql,
            new { Id = id, Status = TrendDiscoveryJobStatus.Completed, TotalKeywords = totalKeywords }, commandTimeout: 30);
    }

    public async Task FailAsync(long id, string error, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE trend_discovery_jobs
            SET status = @Status,
                finished_at = now(),
                duration_ms = EXTRACT(EPOCH FROM (now() - started_at)) * 1000,
                error_message = @Error
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql,
            new { Id = id, Status = TrendDiscoveryJobStatus.Failed, Error = error }, commandTimeout: 30);
    }

    public async Task<IEnumerable<TrendDiscoveryJob>> ListAsync(DateTime? date, int limit, int offset, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id              AS Id,
                started_at      AS StartedAt,
                finished_at     AS FinishedAt,
                duration_ms     AS DurationMs,
                status          AS Status,
                total_keywords  AS TotalKeywords,
                error_message   AS ErrorMessage,
                source          AS Source
            FROM trend_discovery_jobs
            WHERE (@Date::date IS NULL OR started_at::date = @Date::date)
            ORDER BY started_at DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        return await connection.QueryAsync<TrendDiscoveryJob>(sql,
            new { Date = date, Limit = limit, Offset = offset }, commandTimeout: 30);
    }
}