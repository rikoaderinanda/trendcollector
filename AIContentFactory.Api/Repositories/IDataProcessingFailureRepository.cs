using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Centralized failure tracking for all agents.
/// </summary>
public interface IDataProcessingFailureRepository
{
    /// <summary>Records a new failure or upserts if one exists for the same agent+entity.</summary>
    Task<long> RecordAsync(DataProcessingFailure failure, CancellationToken ct = default);

    /// <summary>Lists retryable failures whose next_retry_at has elapsed.</summary>
    Task<IEnumerable<DataProcessingFailure>> GetRetryableAsync(int limit, CancellationToken ct = default);

    /// <summary>Gets a failure by id.</summary>
    Task<DataProcessingFailure?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Updates retry count, next retry time, and error info after a failed retry.</summary>
    Task MarkRetryAttemptFailedAsync(long id, string error, DateTimeOffset nextRetryAt, CancellationToken ct = default);

    /// <summary>Marks a failure as permanently failed (max retries exhausted).</summary>
    Task MarkPermanentFailedAsync(long id, string error, CancellationToken ct = default);

    /// <summary>Marks a failure as resolved (recovery succeeded).</summary>
    Task MarkRecoveredAsync(long id, string resolutionType, CancellationToken ct = default);

    /// <summary>Marks a failure as quarantined for manual review.</summary>
    Task MarkQuarantinedAsync(long id, CancellationToken ct = default);
}