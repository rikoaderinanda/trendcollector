namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Centralized per-item failure tracking for all AI Content Factory agents.
/// Records transient and permanent processing failures with retry history,
/// enabling the centralized Data Recovery worker to reprocess eligible items.
/// </summary>
public sealed class DataProcessingFailure
{
    public long Id { get; set; }

    /// <summary>"TrendDiscovery", "TrendCollector", "KnowledgeExtraction", "ViralAnalyzer"</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>"TrendKeyword", "TrendingVideo", "QueueItem", "AnalysisRun", etc.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>FK to the specific entity's table (e.g. trending_videos.id).</summary>
    public long EntityId { get; set; }

    /// <summary>"discover", "collect", "extract", "analyze"</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>"Retryable", "Failed", "PermanentFailed", "Quarantined", "Recovered"</summary>
    public string Status { get; set; } = "Retryable";

    /// <summary>"Transient" or "Permanent"</summary>
    public string FailureType { get; set; } = "Transient";

    public string? FailureReason { get; set; }

    /// <summary>Fully-qualified exception type name.</summary>
    public string? ExceptionType { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryAttempts { get; set; } = 5;

    public DateTimeOffset FirstAttemptAt { get; set; }

    public DateTimeOffset LastAttemptAt { get; set; }

    /// <summary>Null when the item is not currently scheduled for retry.</summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>Null when the failure has not been resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>"AutoRecovered", "ManualRecovered", "Quarantined", null</summary>
    public string? ResolutionType { get; set; }

    /// <summary>Reference to raw source data (e.g. "video_statistics.id=42").</summary>
    public string? RawReference { get; set; }

    /// <summary>Agent-specific metadata stored as JSON string.</summary>
    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}