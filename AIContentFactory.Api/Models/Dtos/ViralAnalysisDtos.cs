using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>Full Viral Analysis result returned by GET /viral-analysis/{id}.</summary>
public sealed class ViralAnalysisResultDto
{
    public long Id { get; set; }
    public long AnalysisRunId { get; set; }
    public DateTimeOffset AnalyzedAt { get; set; }

    public string? TrendSummary { get; set; }
    public string? MarketObservation { get; set; }

    public IReadOnlyList<WinningPattern> WinningPatterns { get; set; } = Array.Empty<WinningPattern>();

    public IReadOnlyList<ContentOpportunity> ContentOpportunities { get; set; } = Array.Empty<ContentOpportunity>();

    public ContentOpportunity? RecommendedOpportunity { get; set; }

    public decimal? ConfidenceScore { get; set; }
    public string? AnalysisVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Ranked winning patterns returned by GET /viral-analysis/{id}/patterns.</summary>
public sealed class WinningPatternDto
{
    public long Id { get; set; }
    public string PatternType { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public int SupportingVideoCount { get; set; }
    public decimal AverageMomentumScore { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

/// <summary>Ranked content opportunities returned by GET /viral-analysis/{id}/opportunities.</summary>
public sealed class ContentOpportunityDto
{
    public long Id { get; set; }
    public int Rank { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Angle { get; set; }
    public string? TargetAudience { get; set; }
    public string Hook { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string[]? Structure { get; set; }
    public string? Emotion { get; set; }
    public string? PsychologicalTrigger { get; set; }
    public string WhyNow { get; set; } = string.Empty;
    public string? ContentGap { get; set; }
    public string? DifferentiationStrategy { get; set; }
    public string? CallToAction { get; set; }
    public decimal OpportunityScore { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string RiskLevel { get; set; } = "Medium";
    public long[]? SupportingVideoIds { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

/// <summary>
/// TOP 1 recommendation returned by GET /viral-analysis/{id}/recommendation.
/// This is the strategic content blueprint for the next content-generation agent.
/// </summary>
public sealed class ViralAnalysisRecommendationDto
{
    public ContentOpportunityDto Opportunity { get; set; } = new();

    /// <summary>Overall confidence of the analysis (0-100).</summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>Why this opportunity is recommended right now.</summary>
    public string WhyThisOpportunity { get; set; } = string.Empty;

    /// <summary>Traceable evidence supporting the recommendation.</summary>
    public IReadOnlyList<string> Evidence { get; set; } = Array.Empty<string>();

    /// <summary>Identified risks.</summary>
    public IReadOnlyList<string> Risks { get; set; } = Array.Empty<string>();

    /// <summary>How to differentiate from existing videos.</summary>
    public string DifferentiationStrategy { get; set; } = string.Empty;
}

/// <summary>Response returned by POST /viral-analysis/run containing the new run id.</summary>
public sealed class RunViralAnalysisResponse
{
    public long AnalysisRunId { get; set; }
    public string Status { get; set; } = "Running";
    public int TotalCandidates { get; set; }
    public int EligibleCandidates { get; set; }
}