namespace AIContentFactory.Api.Models.Analysis;

/// <summary>
/// Pre-computed performance metrics for a candidate video.
/// Calculated in application code - never sent raw to the LLM.
/// </summary>
public sealed class VideoPerformanceSummary
{
    public long VideoId { get; set; }

    public long? Views { get; set; }
    public long? Likes { get; set; }
    public long? Comments { get; set; }

    public int? VideoAgeDays { get; set; }

    public decimal? ViewsPerHour { get; set; }
    public decimal? LikesPerHour { get; set; }
    public decimal? CommentsPerHour { get; set; }

    /// <summary>Engagement rate as a fraction, e.g. 0.0342 = 3.42%.</summary>
    public decimal? EngagementRate { get; set; }

    /// <summary>Composite momentum/growth score 0-100.</summary>
    public decimal MomentumScore { get; set; }

    /// <summary>Candidate score 0-100 (weighted momentum + engagement).</summary>
    public decimal CandidateScore { get; set; }

    /// <summary>How many statistics snapshots exist for this video.</summary>
    public int StatisticsSnapshotCount { get; set; }
}