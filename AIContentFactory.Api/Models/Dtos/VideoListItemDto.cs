using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Video list item that pairs a trending video with its most recent
/// statistics snapshot (views, likes, comments, captured_at, velocity metrics, etc.).
/// Used by the videos listing endpoint so the UI can sort/filter by
/// the latest captured statistics without N+1 queries.
/// </summary>
public sealed class VideoListItemDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? Duration { get; set; }
    public string? Category { get; set; }
    public string[]? Tags { get; set; }
    public string? Language { get; set; }
    public string? ThumbnailHighUrl { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    // Latest statistics snapshot (all numeric metrics from video_statistics).
    public long? Views { get; set; }
    public long? Likes { get; set; }
    public long? Comments { get; set; }
    public long? Favorites { get; set; }
    public decimal? EngagementRate { get; set; }
    public decimal? LikeRatio { get; set; }
    public decimal? CommentRatio { get; set; }
    public decimal? ViewPerDay { get; set; }
    public decimal? VideoAgeDays { get; set; }
    public DateTimeOffset? StatisticsCapturedAt { get; set; }

    // Tracking Mode velocity metrics.
    public decimal? ViewsPerHour { get; set; }
    public decimal? LikeVelocity { get; set; }
    public decimal? CommentVelocity { get; set; }
    public decimal? GrowthScore { get; set; }
}