using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

public sealed class DataProcessingFailureRepository : IDataProcessingFailureRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public DataProcessingFailureRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> RecordAsync(DataProcessingFailure failure, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            INSERT INTO data_processing_failures (
                agent_name, entity_type, entity_id, operation,
                status, failure_type, failure_reason, exception_type,
                max_retry_attempts, first_attempt_at, last_attempt_at,
                next_retry_at, raw_reference, metadata_json
            )
            VALUES (
                @AgentName, @EntityType, @EntityId, @Operation,
                @Status, @FailureType, @FailureReason, @ExceptionType,
                @MaxRetryAttempts, @FirstAttemptAt, @LastAttemptAt,
                @NextRetryAt, @RawReference, @MetadataJson::jsonb
            )
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql, failure, commandTimeout: 30);
    }

    public async Task<IEnumerable<DataProcessingFailure>> GetRetryableAsync(int limit, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            SELECT
                id, agent_name, entity_type, entity_id, operation,
                status, failure_type, failure_reason, exception_type,
                retry_count, max_retry_attempts, first_attempt_at, last_attempt_at,
                next_retry_at, resolved_at, resolution_type, raw_reference,
                metadata_json, created_at, updated_at
            FROM data_processing_failures
            WHERE status = 'Retryable'
              AND (next_retry_at IS NULL OR next_retry_at <= now())
            ORDER BY last_attempt_at ASC
            LIMIT @Limit;
            """;

        return await connection.QueryAsync<DataProcessingFailure>(sql, new { Limit = limit }, commandTimeout: 30);
    }

    public async Task<DataProcessingFailure?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            SELECT
                id, agent_name, entity_type, entity_id, operation,
                status, failure_type, failure_reason, exception_type,
                retry_count, max_retry_attempts, first_attempt_at, last_attempt_at,
                next_retry_at, resolved_at, resolution_type, raw_reference,
                metadata_json, created_at, updated_at
            FROM data_processing_failures
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<DataProcessingFailure>(sql, new { Id = id }, commandTimeout: 30);
    }

    public async Task MarkRetryAttemptFailedAsync(long id, string error, DateTimeOffset nextRetryAt, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            UPDATE data_processing_failures
            SET retry_count = retry_count + 1,
                failure_reason = @Error,
                last_attempt_at = now(),
                next_retry_at = @NextRetryAt,
                updated_at = now()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, Error = error, NextRetryAt = nextRetryAt }, commandTimeout: 30);
    }

    public async Task MarkPermanentFailedAsync(long id, string error, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            UPDATE data_processing_failures
            SET status = 'PermanentFailed',
                failure_reason = @Error,
                last_attempt_at = now(),
                next_retry_at = NULL,
                updated_at = now()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, Error = error }, commandTimeout: 30);
    }

    public async Task MarkRecoveredAsync(long id, string resolutionType, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            UPDATE data_processing_failures
            SET status = 'Recovered',
                resolved_at = now(),
                resolution_type = @ResolutionType,
                updated_at = now()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, ResolutionType = resolutionType }, commandTimeout: 30);
    }

    public async Task MarkQuarantinedAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            UPDATE data_processing_failures
            SET status = 'Quarantined',
                next_retry_at = NULL,
                updated_at = now()
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id }, commandTimeout: 30);
    }
}