namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// A single trend discovery execution run.
/// </summary>
public sealed class TrendDiscoveryJob
{
    public long Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Execution duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>running / completed / failed.</summary>
    public string Status { get; set; } = TrendDiscoveryJobStatus.Running;

    /// <summary>Number of keywords upserted in this job.</summary>
    public int TotalKeywords { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Origin of this job, e.g. "AI". Future: "GoogleTrends".</summary>
    public string Source { get; set; } = DiscoverySource.AI;
}