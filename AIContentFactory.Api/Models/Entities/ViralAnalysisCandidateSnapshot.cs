namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Snapshot of a candidate video considered during a Viral Analysis run.
/// Persists eligibility, skip reason, and the pre-computed performance/pattern
/// data so the analysis is fully auditable.
/// </summary>
public sealed class ViralAnalysisCandidateSnapshot
{
    public long Id { get; set; }

    /// <summary>FK to viral_analysis_runs.id.</summary>
    public long AnalysisRunId { get; set; }

    /// <summary>FK to trending_videos.id.</summary>
    public long VideoId { get; set; }

    /// <summary>True when the video passed all eligibility checks.</summary>
    public bool IsEligible { get; set; }

    /// <summary>Why the video was skipped when not eligible, e.g. "Missing knowledge".</summary>
    public string? SkipReason { get; set; }

    /// <summary>Pre-computed performance metrics as JSON (views/hour, growth score, etc.).</summary>
    public string? PerformanceSummaryJson { get; set; }

    /// <summary>Extracted content patterns as JSON (hook, structure, emotion, triggers).</summary>
    public string? PatternSummaryJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}