using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Isolated, configurable scoring model for content opportunities.
/// Future versions may replace this rule-based scoring with a machine-learning
/// model without changing callers.
/// </summary>
public interface IContentOpportunityScorer
{
    /// <summary>
    /// Scores a content opportunity on a 0-100 scale.
    /// </summary>
    decimal Score(ContentOpportunityDraft opportunity);
}

/// <summary>
/// Mutable draft of a content opportunity being scored.
/// The AI provider generates these before they are persisted as
/// <see cref="ContentOpportunity"/> entities with their final scores.
/// </summary>
public sealed class ContentOpportunityDraft
{
    public string Topic { get; set; } = string.Empty;
    public string? Angle { get; set; }
    public string? TargetAudience { get; set; }
    public string? Hook { get; set; }
    public string? Format { get; set; }
    public string[]? Structure { get; set; }
    public string? Emotion { get; set; }
    public string? PsychologicalTrigger { get; set; }
    public string? WhyNow { get; set; }
    public string? ContentGap { get; set; }
    public string? DifferentiationStrategy { get; set; }
    public string? CallToAction { get; set; }
    public long[]? SupportingVideoIds { get; set; }

    /// <summary>
    /// Average momentum score (0-100) of the supporting videos, computed from
    /// real performance data. Used by the scorer as the Trend Momentum component.
    /// </summary>
    public decimal? AverageSupportingMomentum { get; set; }

    public decimal? AiOpportunityScore { get; set; }
    public decimal? AiConfidenceScore { get; set; }
    public string? RiskLevel { get; set; }

    /// <summary>List of evidence strings as returned by the AI.</summary>
    public List<string> Evidence { get; set; } = new();
}