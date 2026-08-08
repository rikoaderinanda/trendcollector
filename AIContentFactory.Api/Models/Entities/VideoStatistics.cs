namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// A statistics snapshot of a video at a point in time.
/// </summary>
public sealed class VideoStatistics
{
    public long Id { get; set; }

    /// <summary>FK to trending_videos.id.</summary>
    public long VideoId { get; set; }

    /// <summary>Null when the platform does not expose the value.</summary>
    public long? Views { get; set; }

    public long? Likes { get; set; }
    public long? Comments { get; set; }
    public long? Favorites { get; set; }

    /// <summary>(likes + comments) / views * 100.</summary>
    public decimal? EngagementRate { get; set; }

    /// <summary>likes / views * 100.</summary>
    public decimal? LikeRatio { get; set; }

    /// <summary>comments / views * 100.</summary>
    public decimal? CommentRatio { get; set; }

    /// <summary>views / video_age_days.</summary>
    public decimal? ViewPerDay { get; set; }

    /// <summary>Views gained per hour since the previous snapshot.</summary>
    public decimal? ViewsPerHour { get; set; }

    /// <summary>Likes gained per hour since the previous snapshot.</summary>
    public decimal? LikeVelocity { get; set; }

    /// <summary>Comments gained per hour since the previous snapshot.</summary>
    public decimal? CommentVelocity { get; set; }

    /// <summary>Composite 0-100 growth score (see VideoVelocity).</summary>
    public decimal? GrowthScore { get; set; }

    /// <summary>Id of the previous snapshot used to compute velocity metrics.</summary>
    public long? PreviousSnapshotId { get; set; }

    /// <summary>Days between published_at and captured_at (minimum 1).</summary>
    public int? VideoAgeDays { get; set; }

    /// <summary>When this snapshot was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; }
}

