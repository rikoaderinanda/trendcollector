namespace AIContentFactory.Api.Models;

/// <summary>
/// Query parameters for listing videos with optional statistics filters
/// and sorting. All filter ranges are optional — when a value is null the
/// corresponding filter is not applied.
/// </summary>
public sealed class VideoListQuery
{
    // Basic filters.
    public string? Language { get; set; }
    public DateTime? Date { get; set; }
    public int Limit { get; set; } = 20;
    public int Offset { get; set; }

    // Sorting.
    /// <summary>Sort column. Validated against an explicit whitelist in the repository.</summary>
    public string? SortBy { get; set; }
    /// <summary>"asc" or "desc". Defaults to "desc" when not provided.</summary>
    public string? SortDirection { get; set; }

    // Statistics filter ranges (latest snapshot).
    public long? MinViews { get; set; }
    public long? MaxViews { get; set; }
    public long? MinLikes { get; set; }
    public long? MaxLikes { get; set; }
    public long? MinComments { get; set; }
    public long? MaxComments { get; set; }
    public long? MinFavorites { get; set; }
    public long? MaxFavorites { get; set; }
    public decimal? MinEngagementRate { get; set; }
    public decimal? MaxEngagementRate { get; set; }
    public decimal? MinViewPerDay { get; set; }
    public decimal? MaxViewPerDay { get; set; }
    public decimal? MinVideoAgeDays { get; set; }
    public decimal? MaxVideoAgeDays { get; set; }
    public DateTimeOffset? CapturedAfter { get; set; }
    public DateTimeOffset? CapturedBefore { get; set; }

    // Tracking Mode velocity metrics.
    public decimal? MinViewsPerHour { get; set; }
    public decimal? MaxViewsPerHour { get; set; }
    public decimal? MinLikeVelocity { get; set; }
    public decimal? MaxLikeVelocity { get; set; }
    public decimal? MinCommentVelocity { get; set; }
    public decimal? MaxCommentVelocity { get; set; }
    public decimal? MinGrowthScore { get; set; }
    public decimal? MaxGrowthScore { get; set; }
}