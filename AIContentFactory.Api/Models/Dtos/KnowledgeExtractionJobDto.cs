namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Knowledge extraction queue item exposed by the API.
/// </summary>
public sealed class KnowledgeExtractionJobDto
{
    public long Id { get; set; }

    /// <summary>FK to trending_videos.id.</summary>
    public long VideoId { get; set; }

    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int RetryCount { get; set; }

    /// <summary>Earliest time the item may be retried (exponential backoff). Null when immediately retryable.</summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}