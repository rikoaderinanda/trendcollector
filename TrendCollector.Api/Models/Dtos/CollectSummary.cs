namespace TrendCollector.Api.Models.Dtos;

/// <summary>
/// Execution summary returned when a collection job finishes.
/// </summary>
public sealed class CollectSummary
{
    public long JobId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Language { get; set; }

    /// <summary>Videos returned by the search.</summary>
    public int TotalCollected { get; set; }

    /// <summary>New videos saved to the database.</summary>
    public int TotalSaved { get; set; }

    /// <summary>Duplicate / invalid videos ignored.</summary>
    public int TotalSkipped { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
}