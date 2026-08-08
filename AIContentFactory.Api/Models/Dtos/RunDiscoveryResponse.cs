namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Summary returned after a trend discovery run.
/// </summary>
public sealed class RunDiscoveryResponse
{
    public long JobId { get; set; }

    public string Status { get; set; } = string.Empty;

    /// <summary>Number of keywords upserted in this run.</summary>
    public int TotalKeywords { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public long? DurationMs { get; set; }

    public string? ErrorMessage { get; set; }
}