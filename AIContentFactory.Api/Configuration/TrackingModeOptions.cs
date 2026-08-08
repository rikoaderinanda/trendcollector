namespace AIContentFactory.Api.Configuration;

/// <summary>
/// Options for the quota-based Tracking Mode feature.
/// Bound from the "TrackingMode" configuration section.
/// </summary>
public sealed class TrackingModeOptions
{
    public const string SectionName = "TrackingMode";

    /// <summary>Whether the tracking mode/background service is enabled globally.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum number of search.list calls allowed per UTC day.</summary>
    public int MaxSearchCallsPerDay { get; set; } = 10;

    /// <summary>How often the tracking background service runs (in minutes).</summary>
    public int TrackingIntervalMinutes { get; set; } = 300;

    /// <summary>Number of days to look back when selecting videos to track.</summary>
    public int LookbackDaysForTracking { get; set; } = 3;

    /// <summary>Maximum video ids per videos.list call.</summary>
    public int VideoBatchSize { get; set; } = 50;

    /// <summary>Weights used when computing the composite GrowthScore.</summary>
    public decimal ViewsVelocityWeight { get; set; } = 0.5m;

    /// <summary>Weights used when computing the composite GrowthScore.</summary>
    public decimal LikeVelocityWeight { get; set; } = 0.3m;

    /// <summary>Weights used when computing the composite GrowthScore.</summary>
    public decimal CommentVelocityWeight { get; set; } = 0.2m;
}