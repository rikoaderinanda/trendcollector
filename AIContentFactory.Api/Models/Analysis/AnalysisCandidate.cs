namespace AIContentFactory.Api.Models.Analysis;

/// <summary>
/// A candidate video with all data required for eligibility checks and
/// downstream analysis. Assembled by the orchestrator from existing
/// Agent 1 + Agent 2 repositories.
/// </summary>
public sealed class AnalysisCandidate
{
    public long VideoId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[]? Tags { get; set; }
    public string? Language { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Latest statistics snapshot, null when not yet collected.</summary>
    public AIContentFactory.Api.Models.Entities.VideoStatistics? Statistics { get; set; }

    /// <summary>Extracted knowledge, null when Agent 2 has not completed.</summary>
    public AIContentFactory.Api.Models.Entities.VideoKnowledge? Knowledge { get; set; }

    /// <summary>Transcript text, null when unavailable.</summary>
    public string? Transcript { get; set; }

    /// <summary>Pre-computed performance metrics.</summary>
    public VideoPerformanceSummary? Performance { get; set; }

    /// <summary>True when this video passes all eligibility checks.</summary>
    public bool IsEligible { get; set; }

    /// <summary>Why the video was skipped when not eligible.</summary>
    public string? SkipReason { get; set; }
}