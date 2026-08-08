namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// A search target (keyword) discovered by AI or another future source.
/// Consumed later by the Trend Collector Agent.
/// </summary>
public sealed class TrendKeyword
{
    public long Id { get; set; }

    /// <summary>YouTube search keyword, e.g. "OpenAI Codex".</summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>Niche / topic category, e.g. "Artificial Intelligence".</summary>
    public string? Niche { get; set; }

    /// <summary>Country target, e.g. "Global", "US", "ID".</summary>
    public string Country { get; set; } = "Global";

    /// <summary>Language code, e.g. "en", "id".</summary>
    public string Language { get; set; } = "en";

    /// <summary>Priority 1-100; higher means more important.</summary>
    public int Priority { get; set; } = 50;

    /// <summary>AI reasoning / justification for this keyword.</summary>
    public string? DiscoveryReason { get; set; }

    /// <summary>Origin of this keyword, e.g. "AI", "GoogleTrends".</summary>
    public string Source { get; set; } = DiscoverySource.AI;

    /// <summary>Lifecycle status, e.g. "active", "collected".</summary>
    public string Status { get; set; } = KeywordStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}