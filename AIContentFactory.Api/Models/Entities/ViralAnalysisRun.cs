namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// A single execution of the Viral Analyzer (Agent 3).
/// Tracks the analysis inputs, candidate counts, and the final recommendation
/// for the next content-generation agent.
/// </summary>
public sealed class ViralAnalysisRun
{
    public long Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Running / Completed / Failed.</summary>
    public string Status { get; set; } = "Running";

    /// <summary>Optional niche filter, e.g. "AI Tools".</summary>
    public string? Niche { get; set; }

    /// <summary>Optional trend keyword filter.</summary>
    public string? TrendKeyword { get; set; }

    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public int TotalCandidates { get; set; }

    public int EligibleCandidates { get; set; }

    public int OpportunitiesGenerated { get; set; }

    /// <summary>FK to viral_analysis_content_opportunities.id (TOP 1).</summary>
    public long? RecommendedOpportunityId { get; set; }

    public string? TrendSummary { get; set; }

    public string? MarketObservation { get; set; }

    /// <summary>Overall confidence of the analysis, 0-100.</summary>
    public decimal? ConfidenceScore { get; set; }

    /// <summary>Prompt/Analysis version, e.g. "v1".</summary>
    public string? AnalysisVersion { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}