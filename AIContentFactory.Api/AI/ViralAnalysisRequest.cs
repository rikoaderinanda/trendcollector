namespace AIContentFactory.Api.AI;

/// <summary>
/// Request payload for the Viral Analyzer AI provider.
/// Contains pre-processed candidate summaries, winning patterns, trends and
/// gaps. Raw statistics are computed in application code - never sent raw.
/// </summary>
public sealed class ViralAnalysisRequest
{
    /// <summary>Analysis run id this request belongs to.</summary>
    public long AnalysisRunId { get; set; }

    public string? Niche { get; set; }
    public string? TrendKeyword { get; set; }

    /// <summary>Formatted list of pre-processed candidate summaries.</summary>
    public string CandidateSummaries { get; set; } = string.Empty;

    /// <summary>Formatted list of detected winning patterns.</summary>
    public string WinningPatterns { get; set; } = string.Empty;

    /// <summary>Formatted trend observations.</summary>
    public string TrendSummary { get; set; } = string.Empty;

    /// <summary>Formatted content gap observations.</summary>
    public string ContentGaps { get; set; } = string.Empty;

    /// <summary>Number of opportunities to generate.</summary>
    public int OpportunityCount { get; set; } = 5;
}