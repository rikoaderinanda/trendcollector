namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IContentOpportunityScorer" />
/// <summary>
/// Configurable rule-based scorer with the following default weights:
/// 30% Trend Momentum, 20% Audience Relevance, 15% Engagement Evidence,
/// 15% Content Gap, 10% Novelty, 10% Production Feasibility.
/// The weights are overridable via constructor injection so a future
/// machine-learning model can replace this implementation without changing callers.
/// </summary>
public sealed class ContentOpportunityScorer : IContentOpportunityScorer
{
    private readonly OpportunityScoreWeights _weights;

    public ContentOpportunityScorer(OpportunityScoreWeights? weights = null)
    {
        _weights = weights ?? OpportunityScoreWeights.Default;
    }

    public decimal Score(ContentOpportunityDraft opportunity)
    {
        var trendMomentum = ScoreTrendMomentum(opportunity);
        var audienceRelevance = ScoreAudienceRelevance(opportunity);
        var engagementEvidence = ScoreEngagementEvidence(opportunity);
        var contentGap = ScoreContentGap(opportunity);
        var novelty = ScoreNovelty(opportunity);
        var feasibility = ScoreFeasibility(opportunity);

        var score = trendMomentum * _weights.TrendMomentum
                    + audienceRelevance * _weights.AudienceRelevance
                    + engagementEvidence * _weights.EngagementEvidence
                    + contentGap * _weights.ContentGap
                    + novelty * _weights.Novelty
                    + feasibility * _weights.ProductionFeasibility;

        return Math.Round(Math.Clamp(score, 0m, 100m), 2);
    }

    // ---------- Scoring dimensions ----------

    /// <summary>30% - How strong is the underlying trend momentum?</summary>
    private static decimal ScoreTrendMomentum(ContentOpportunityDraft opportunity)
    {
        // Use the REAL average momentum of the supporting videos (computed from
        // actual performance data), not the AI's perceived opportunity score.
        // This keeps the scorer independent from AI output.
        if (opportunity.AverageSupportingMomentum is not null)
        {
            return Math.Clamp(opportunity.AverageSupportingMomentum.Value, 0m, 100m);
        }

        // No supporting videos = no evidence of momentum; neutral baseline.
        return 50m;
    }

    /// <summary>20% - Does the opportunity clearly define a target audience?</summary>
    private static decimal ScoreAudienceRelevance(ContentOpportunityDraft opportunity)
    {
        if (string.IsNullOrWhiteSpace(opportunity.TargetAudience))
        {
            return 30m;
        }

        var audience = opportunity.TargetAudience;
        var score = 50m;

        if (audience.Contains("beginner", StringComparison.OrdinalIgnoreCase)
            || audience.Contains("new", StringComparison.OrdinalIgnoreCase))
        {
            score += 15m;
        }

        if (audience.Contains("creator", StringComparison.OrdinalIgnoreCase)
            || audience.Contains("marketer", StringComparison.OrdinalIgnoreCase)
            || audience.Contains("business", StringComparison.OrdinalIgnoreCase)
            || audience.Contains("developer", StringComparison.OrdinalIgnoreCase))
        {
            score += 15m;
        }

        if (audience.Length > 30)
        {
            score += 10m; // detailed audience definition indicates deeper relevance
        }

        // Supporting evidence raises relevance
        if (opportunity.SupportingVideoIds is { Length: >= 2 })
        {
            score += 10m;
        }

        return Math.Min(score, 100m);
    }

    /// <summary>15% - Is there concrete engagement evidence supporting this opportunity?</summary>
    private static decimal ScoreEngagementEvidence(ContentOpportunityDraft opportunity)
    {
        if (opportunity.SupportingVideoIds is null || opportunity.SupportingVideoIds.Length == 0)
        {
            return 20m;
        }

        var score = 50m;
        var count = opportunity.SupportingVideoIds.Length;

        if (count >= 2)
        {
            score += 15m;
        }

        if (count >= 4)
        {
            score += 15m;
        }

        if (opportunity.Evidence.Count >= 2)
        {
            score += 10m;
        }

        return Math.Min(score, 100m);
    }

    /// <summary>15% - Does the opportunity fill a real content gap?</summary>
    private static decimal ScoreContentGap(ContentOpportunityDraft opportunity)
    {
        if (string.IsNullOrWhiteSpace(opportunity.ContentGap))
        {
            return 30m;
        }

        var gap = opportunity.ContentGap;
        var score = 60m;

        if (gap.Contains("no", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("lack", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("under", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("not covered", StringComparison.OrdinalIgnoreCase))
        {
            score += 20m;
        }

        if (gap.Contains("simpl", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("concise", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("quick", StringComparison.OrdinalIgnoreCase))
        {
            score += 10m;
        }

        if (gap.Contains("workflow", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("step", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("tutorial", StringComparison.OrdinalIgnoreCase))
        {
            score += 10m;
        }

        return Math.Min(score, 100m);
    }

    /// <summary>10% - How novel/different is the angle from existing videos?</summary>
    private static decimal ScoreNovelty(ContentOpportunityDraft opportunity)
    {
        if (string.IsNullOrWhiteSpace(opportunity.DifferentiationStrategy)
            && string.IsNullOrWhiteSpace(opportunity.Angle))
        {
            return 40m;
        }

        var score = 60m;

        if (!string.IsNullOrWhiteSpace(opportunity.DifferentiationStrategy))
        {
            score += 20m;
        }

        if (opportunity.Angle is not null
            && opportunity.Angle.Contains("contrar", StringComparison.OrdinalIgnoreCase)
            || opportunity.Angle?.Contains("vs", StringComparison.OrdinalIgnoreCase) == true
            || opportunity.Angle?.Contains("underrated", StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 20m;
        }

        return Math.Min(score, 100m);
    }

    /// <summary>10% - Can this opportunity realistically be produced?</summary>
    private static decimal ScoreFeasibility(ContentOpportunityDraft opportunity)
    {
        if (string.IsNullOrWhiteSpace(opportunity.Format))
        {
            return 50m;
        }

        var format = opportunity.Format;
        var score = 70m;

        if (format.Contains("short", StringComparison.OrdinalIgnoreCase)
            || format.Contains("reel", StringComparison.OrdinalIgnoreCase)
            || format.Contains("30-60", StringComparison.OrdinalIgnoreCase))
        {
            score += 20m; // short-form is cheap to produce
        }

        if (format.Contains("tutorial", StringComparison.OrdinalIgnoreCase)
            || format.Contains("list", StringComparison.OrdinalIgnoreCase))
        {
            score += 10m; // structured formats are easy to plan
        }

        if (format.Contains("documentary", StringComparison.OrdinalIgnoreCase)
            || format.Contains("case study", StringComparison.OrdinalIgnoreCase))
        {
            score -= 20m; // heavy research/production
        }

        return Math.Clamp(score, 0m, 100m);
    }
}

/// <summary>
/// Configurable weights for the content opportunity scorer.
/// Default weights per the specification: 30/20/15/15/10/10.
/// </summary>
public sealed class OpportunityScoreWeights
{
    public decimal TrendMomentum { get; set; } = 0.30m;
    public decimal AudienceRelevance { get; set; } = 0.20m;
    public decimal EngagementEvidence { get; set; } = 0.15m;
    public decimal ContentGap { get; set; } = 0.15m;
    public decimal Novelty { get; set; } = 0.10m;
    public decimal ProductionFeasibility { get; set; } = 0.10m;

    public static OpportunityScoreWeights Default => new();
}