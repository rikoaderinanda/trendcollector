namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Velocity metrics that describe how fast a video's engagement is growing
/// between two statistics snapshots. All values are computed in the
/// <see cref="Services.StatisticsCalculator"/> and stored with the new
/// snapshot in <c>video_statistics</c>.
/// </summary>
public sealed class VideoVelocity
{
    /// <summary>True when this velocity was computed from the very first snapshot (no previous data).</summary>
    public bool IsFirstSnapshot { get; set; }

    /// <summary>Views gained per hour between the previous and current snapshot.</summary>
    public decimal? ViewsPerHour { get; set; }

    /// <summary>Likes gained per hour between the previous and current snapshot.</summary>
    public decimal? LikeVelocity { get; set; }

    /// <summary>Comments gained per hour between the previous and current snapshot.</summary>
    public decimal? CommentVelocity { get; set; }

    /// <summary>
    /// Composite 0-100 score representing how "hot" a video is right now.
    /// Combines normalized views, like and comment velocity.
    /// </summary>
    public decimal? GrowthScore { get; set; }
}