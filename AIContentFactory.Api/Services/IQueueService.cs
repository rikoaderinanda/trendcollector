using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Business logic over the knowledge extraction queue.
/// </summary>
public interface IQueueService
{
    /// <summary>Enqueues a video for knowledge extraction (no-op if already queued).</summary>
    Task<KnowledgeExtractionQueue> EnqueueAsync(long videoId, int priority = 0,
        CancellationToken cancellationToken = default);

    /// <summary>Gets pending queue items ordered by priority (highest first).</summary>
    Task<IReadOnlyList<KnowledgeExtractionQueue>> GetPendingAsync(int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Lists queued jobs, optionally filtered by status and the calendar date of created_at.</summary>
    Task<IReadOnlyList<KnowledgeExtractionQueue>> ListAsync(string? status, DateTime? date, int limit, int offset,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a queue item by id, or null when not found.</summary>
    Task<KnowledgeExtractionQueue?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Gets the queue item for a video, or null when not found.</summary>
    Task<KnowledgeExtractionQueue?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>Atlocks a queue item as Running.</summary>
    Task MarkRunningAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Marks a queue item as Completed with duration.</summary>
    Task MarkCompletedAsync(long id, long durationMs, CancellationToken cancellationToken = default);

    /// <summary>Marks a queue item as TranscriptUnavailable.</summary>
    Task MarkTranscriptUnavailableAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a failed attempt. If retry count is below the configured maximum,
    /// resets to Pending for exponential backoff retry; otherwise marks Failed permanently.
    /// </summary>
    /// <returns>True when the job was scheduled for retry; false when it was marked Failed permanently.</returns>
    Task<bool> MarkAttemptFailedAsync(long id, string error, CancellationToken cancellationToken = default);

    /// <summary>Resets a failed queue item back to Pending for manual retry.</summary>
    Task ResetForRetryAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all queue items in the terminal TranscriptUnavailable state back
    /// to Pending so the background worker can retry them (e.g. after a
    /// transcript fallback provider was added).
    /// </summary>
    /// <returns>The number of queue items reset.</returns>
    Task<int> ResetAllTranscriptUnavailableAsync(CancellationToken cancellationToken = default);
}
