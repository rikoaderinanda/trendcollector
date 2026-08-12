namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Request payload to start a Viral Analysis run.
/// All filters are optional; when omitted, the default Daily Analysis mode
/// analyzes all eligible candidates from the configured lookback window.
/// </summary>
public sealed class RunViralAnalysisRequest
{
    /// <summary>Optional niche filter, e.g. "AI Tools".</summary>
    public string? Niche { get; set; }

    /// <summary>Optional trend keyword filter.</summary>
    public string? TrendKeyword { get; set; }

    /// <summary>Optional start date filter (inclusive).</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Optional end date filter (inclusive).</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Minimum candidate score (0-100) a video needs to be included. Default 0.</summary>
    public decimal MinimumCandidateScore { get; set; } = 0;

    /// <summary>Maximum number of videos to analyze. Default 50.</summary>
    public int MaximumVideos { get; set; } = 50;
}