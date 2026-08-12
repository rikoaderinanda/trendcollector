namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// A recurring content pattern detected across multiple high-performing videos.
/// Produced by the Cross-Video Pattern Analysis step of Agent 3.
/// </summary>
public sealed class WinningPattern
{
    public long Id { get; set; }

    /// <summary>FK to viral_analysis_runs.id.</summary>
    public long AnalysisRunId { get; set; }

    /// <summary>Pattern category, e.g. "Hook", "Structure", "Emotion", "PsychologicalTrigger".</summary>
    public string PatternType { get; set; } = string.Empty;

    /// <summary>Pattern name, e.g. "Curiosity Gap Hook".</summary>
    public string PatternName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>How many times the pattern appears among the analyzed videos.</summary>
    public int Frequency { get; set; }

    /// <summary>How many of the analyzed videos exhibit this pattern.</summary>
    public int SupportingVideoCount { get; set; }

    /// <summary>Average momentum/growth score of videos exhibiting this pattern.</summary>
    public decimal AverageMomentumScore { get; set; }

    /// <summary>Evidence text describing why this pattern is considered winning.</summary>
    public string Evidence { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}