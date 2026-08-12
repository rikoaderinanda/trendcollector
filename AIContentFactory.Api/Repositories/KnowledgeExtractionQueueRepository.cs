using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IKnowledgeExtractionQueueRepository" />
public sealed class KnowledgeExtractionQueueRepository : IKnowledgeExtractionQueueRepository
{
    private const string SelectColumns = """
        id             AS Id,
        video_id       AS VideoId,
        status         AS Status,
        priority       AS Priority,
        retry_count    AS RetryCount,
        next_retry_at  AS NextRetryAt,
        started_at     AS StartedAt,
        finished_at    AS FinishedAt,
        duration_ms    AS DurationMs,
        error_message  AS ErrorMessage,
        created_at     AS CreatedAt,
        updated_at     AS UpdatedAt
        """;

    private readonly DbConnectionFactory _connectionFactory;

    public KnowledgeExtractionQueueRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task CreateIfNotExistsAsync(long videoId, int priority = 0, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO knowledge_extraction_queue (video_id, status, priority)
            VALUES (@VideoId, 'Pending', @Priority)
            ON CONFLICT (video_id) DO NOTHING;
            """;

        await connection.ExecuteAsync(sql, new { VideoId = videoId, Priority = priority }, commandTimeout: 30);
    }

    public async Task<IReadOnlyList<KnowledgeExtractionQueue>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = $$"""
            SELECT {{SelectColumns}}
            FROM knowledge_extraction_queue
            WHERE status = 'Pending'
              AND (next_retry_at IS NULL OR next_retry_at <= now())
            ORDER BY priority DESC, created_at ASC
            LIMIT @Limit;
            """;

        var results = await connection.QueryAsync<KnowledgeExtractionQueue>(sql,
            new { Limit = limit }, commandTimeout: 30);

        return results.ToList();
    }

    public async Task<KnowledgeExtractionQueue?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = $$"""
            SELECT {{SelectColumns}}
            FROM knowledge_extraction_queue
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<KnowledgeExtractionQueue>(sql,
            new { Id = id }, commandTimeout: 30);
    }

    public async Task<KnowledgeExtractionQueue?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = $$"""
            SELECT {{SelectColumns}}
            FROM knowledge_extraction_queue
            WHERE video_id = @VideoId;
            """;

        return await connection.QuerySingleOrDefaultAsync<KnowledgeExtractionQueue>(sql,
            new { VideoId = videoId }, commandTimeout: 30);
    }

    /// <summary>
    /// Lists queued jobs, optionally filtered by status and the calendar date of
    /// created_at. Left-joins video_transcripts so each row exposes the AI
    /// transcript quality score when one exists.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeExtractionQueue>> ListAsync(string? status, DateTime? date, int limit, int offset, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string listSelectColumns = """
                                         q.id               AS Id,
                                         q.video_id         AS VideoId,
                                         q.status           AS Status,
                                         q.priority         AS Priority,
                                         q.retry_count      AS RetryCount,
                                         q.next_retry_at    AS NextRetryAt,
                                         q.started_at       AS StartedAt,
                                         q.finished_at      AS FinishedAt,
                                         q.duration_ms      AS DurationMs,
                                         q.error_message    AS ErrorMessage,
                                         q.created_at       AS CreatedAt,
                                         q.updated_at       AS UpdatedAt,
                                         vt.transcript_score AS TranscriptScore
                                         """;

        const string sql = $$"""
                             SELECT {{listSelectColumns}}
                             FROM knowledge_extraction_queue q
                             LEFT JOIN video_transcripts vt ON vt.video_id = q.video_id
                             WHERE (@Status IS NULL OR q.status = @Status)
                               AND (@Date::date IS NULL OR q.created_at::date = @Date::date)
                             ORDER BY q.created_at DESC
                             LIMIT @Limit OFFSET @Offset;
                             """;

        var results = await connection.QueryAsync<KnowledgeExtractionQueue>(sql,
            new { Status = status, Date = date, Limit = limit, Offset = offset }, commandTimeout: 30);

        return results.ToList();
    }

    public async Task MarkRunningAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           UPDATE knowledge_extraction_queue
                           SET status = 'Running',
                               started_at = now(),
                               updated_at = now()
                           WHERE id = @Id;
                           """;

        await connection.ExecuteAsync(sql, new { Id = id }, commandTimeout: 30);
    }

    public async Task MarkCompletedAsync(long id, long durationMs, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           UPDATE knowledge_extraction_queue
                           SET status = 'Completed',
                               finished_at = now(),
                               duration_ms = @DurationMs,
                               error_message = NULL,
                               updated_at = now()
                           WHERE id = @Id;
                           """;

        await connection.ExecuteAsync(sql, new { Id = id, DurationMs = durationMs }, commandTimeout: 30);
    }

    public async Task MarkTranscriptUnavailableAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           UPDATE knowledge_extraction_queue
                           SET status = 'TranscriptUnavailable',
                               finished_at = now(),
                               updated_at = now()
                           WHERE id = @Id;
                           """;

        await connection.ExecuteAsync(sql, new { Id = id }, commandTimeout: 30);
    }

    public async Task MarkRetryAsync(long id, string error, DateTimeOffset nextRetryAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           UPDATE knowledge_extraction_queue
                           SET status = 'Pending',
                               retry_count = retry_count + 1,
                               error_message = @Error,
                               next_retry_at = @NextRetryAt,
                               updated_at = now()
                           WHERE id = @Id;
                           """;

        await connection.ExecuteAsync(sql, new { Id = id, Error = error, NextRetryAt = nextRetryAt },
            commandTimeout: 30);
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           UPDATE knowledge_extraction_queue
                           SET status = 'Failed',
                               finished_at = now(),
                               error_message = @Error,
                               updated_at = now()
                           WHERE id = @Id;
                           """;

        await connection.ExecuteAsync(sql, new { Id = id, Error = error }, commandTimeout: 30);
    }

    public async Task ResetForRetryAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           UPDATE knowledge_extraction_queue
                           SET status = 'Pending',
                               retry_count = 0,
                               error_message = NULL,
                               next_retry_at = NULL,
                               started_at = NULL,
                               finished_at = NULL,
                               duration_ms = NULL,
                               updated_at = now()
                           WHERE id = @Id;
                           """;

        await connection.ExecuteAsync(sql, new { Id = id }, commandTimeout: 30);
    }

    public async Task<int> ResetAllTranscriptUnavailableAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           UPDATE knowledge_extraction_queue
                           SET status = 'Pending',
                               retry_count = 0,
                               error_message = NULL,
                               next_retry_at = NULL,
                               started_at = NULL,
                               finished_at = NULL,
                               duration_ms = NULL,
                               updated_at = now()
                           WHERE status = 'TranscriptUnavailable';
                           """;

        var affected = await connection.ExecuteAsync(sql, commandTimeout: 30);
        return affected;
    }
}
