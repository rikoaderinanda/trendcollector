namespace TrendCollector.Api.Models.Entities;

/// <summary>
/// A single trend collection execution.
/// </summary>
public sealed class CollectionJob
{
    public long Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }

    /// <summary>Search keyword, e.g. "AI".</summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "ID".</summary>
    public string? Country { get; set; }

    /// <summary>Language code, e.g. "id".</summary>
    public string? Language { get; set; }

    /// <summary>running / completed / failed.</summary>
    public string Status { get; set; } = CollectionJobStatus.Running;

    public int TotalCollected { get; set; }

    /// <summary>New videos saved in this job.</summary>
    public int TotalSaved { get; set; }

    /// <summary>Duplicate / invalid videos ignored in this job.</summary>
    public int TotalSkipped { get; set; }

    public string? Error { get; set; }
}