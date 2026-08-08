using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IJobRepository" />
public sealed class JobRepository : IJobRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public JobRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> CreateAsync(CollectionJob job, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO collection_jobs (started_at, keyword, mode, country, language, status)
            VALUES (@StartedAt, @Keyword, @Mode, @Country, @Language, @Status)
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql,
            new { job.StartedAt, job.Keyword, job.Mode, job.Country, job.Language, job.Status }, commandTimeout: 30);
    }

    public async Task CompleteAsync(long id, int totalCollected, int totalSaved, int totalSkipped, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE collection_jobs
            SET status = @Status,
                finished_at = now(),
                duration_ms = EXTRACT(EPOCH FROM (now() - started_at)) * 1000,
                total_collected = @TotalCollected,
                total_saved = @TotalSaved,
                total_skipped = @TotalSkipped
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql,
            new
            {
                Id = id,
                Status = CollectionJobStatus.Completed,
                TotalCollected = totalCollected,
                TotalSaved = totalSaved,
                TotalSkipped = totalSkipped
            }, commandTimeout: 30);
    }

    public async Task FailAsync(long id, string error, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE collection_jobs
            SET status = @Status,
                finished_at = now(),
                duration_ms = EXTRACT(EPOCH FROM (now() - started_at)) * 1000,
                error = @Error
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql,
            new { Id = id, Status = CollectionJobStatus.Failed, Error = error }, commandTimeout: 30);
    }

    public async Task<IEnumerable<CollectionJob>> ListAsync(DateTime? date, int limit, int offset,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id               AS Id,
                started_at       AS StartedAt,
                finished_at      AS FinishedAt,
                duration_ms      AS DurationMs,
                keyword          AS Keyword,
                mode             AS Mode,
                country          AS Country,
                language         AS Language,
                status           AS Status,
                total_collected  AS TotalCollected,
                total_saved      AS TotalSaved,
                total_skipped    AS TotalSkipped,
                error            AS Error
            FROM collection_jobs
            WHERE (@Date::date IS NULL OR started_at::date = @Date::date)
            ORDER BY started_at DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        return await connection.QueryAsync<CollectionJob>(sql,
            new { Date = date, Limit = limit, Offset = offset }, commandTimeout: 30);
    }
}