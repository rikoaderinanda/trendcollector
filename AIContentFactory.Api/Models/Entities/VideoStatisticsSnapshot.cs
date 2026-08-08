namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Read-only mirror of the latest statistics snapshot for a video,
/// produced by Agent 1 (Trend Collector). Used as AI input.
/// </summary>
public sealed class VideoStatisticsSnapshot
{
    public long VideoId { get; set; }
    public long? Views { get; set; }
    public long? Likes { get; set; }
    public long? Comments { get; set; }
    public long? Favorites { get; set; }

    /// <summary>Engagement rate as a fraction, e.g. 0.0342 for 3.42%.</summary>
    public decimal? EngagementRate { get; set; }

    public decimal? LikeRatio { get; set; }
    public decimal? CommentRatio { get; set; }
    public decimal? ViewPerDay { get; set; }
    public int? VideoAgeDays { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}