namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Queue item for knowledge extraction. One per video.
/// </summary>
public sealed class KnowledgeExtractionQueue
{
    public long Id { get; set; }

    /// <summary>FK to trending_videos.id.</summary>
    public long VideoId { get; set; }

    public QueueStatus Status { get; set; }

    /// <summary>Higher priority items are processed first.</summary>
    public int Priority { get; set; }

    public int RetryCount { get; set; }

    /// <summary>Earliest time the item may be retried (exponential backoff). Null when immediately retryable.</summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public long? DurationMs { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>AI-assessed quality score (0-100) of the polished transcript, if one exists.</summary>
    public int? TranscriptScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}