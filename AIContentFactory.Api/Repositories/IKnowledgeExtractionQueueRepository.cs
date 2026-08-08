using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for the knowledge extraction queue.
/// </summary>
public interface IKnowledgeExtractionQueueRepository
{
    /// <summary>Creates a pending queue item. No-op when a queue item already exists for the video.</summary>
    Task CreateIfNotExistsAsync(long videoId, int priority = 0, CancellationToken cancellationToken = default);

    /// <summary>Gets pending queue items ordered by priority (highest first).</summary>
    Task<IReadOnlyList<KnowledgeExtractionQueue>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Gets a queue item by id, or null when not found.</summary>
    Task<KnowledgeExtractionQueue?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Gets the queue item for a video, or null when not found.</summary>
    Task<KnowledgeExtractionQueue?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>Lists queued jobs, optionally filtered by status and the calendar date of created_at.</summary>
    Task<IReadOnlyList<KnowledgeExtractionQueue>> ListAsync(string? status, DateTime? date, int limit, int offset, CancellationToken cancellationToken = default);

    /// <summary>Transitions a queue item to Running and stamps StartedAt.</summary>
    Task MarkRunningAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Transitions a queue item to Completed and stamps FinishedAt + duration.</summary>
    Task MarkCompletedAsync(long id, long durationMs, CancellationToken cancellationToken = default);

    /// <summary>Transitions a queue item to TranscriptUnavailable.</summary>
    Task MarkTranscriptUnavailableAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Increments retry_count, resets the item back to Pending and schedules the next retry time.</summary>
    Task MarkRetryAsync(long id, string error, DateTimeOffset nextRetryAt, CancellationToken cancellationToken = default);

    /// <summary>Transitions a queue item to Failed (permanent).</summary>
    Task MarkFailedAsync(long id, string error, CancellationToken cancellationToken = default);

    /// <summary>Resets a failed queue item back to Pending with retry count cleared.</summary>
    Task ResetForRetryAsync(long id, CancellationToken cancellationToken = default);
}