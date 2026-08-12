namespace AIContentFactory.Api.Configuration;

/// <summary>
/// Options for connecting to the Trend Collector API.
/// Bound from the "TrendCollector" configuration section.
/// </summary>
public sealed class TrendCollectorOptions
{
    public const string SectionName = "TrendCollector";

    /// <summary>Base URL of the Trend Collector API, e.g. "http://localhost:5075".</summary>
    public string BaseUrl { get; set; } = "http://localhost:5075";

    /// <summary>Maximum number of search results per keyword (1-50).</summary>
    public int MaxResultsPerKeyword { get; set; } = 20;

    /// <summary>How often the background service polls for active keywords (seconds).</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// When true, only videos with duration max 60 seconds are considered
    /// Shorts-eligible and will be collected/enqueued to Agent 2.
    /// Long-form videos are still stored but marked for skip.
    /// </summary>
    public bool ShortsOnly { get; set; } = true;

    /// <summary>
    /// Maximum video age in days for a video to be considered a fresh
    /// candidate for downstream trending analysis. Older videos are still
    /// stored but are not enqueued to Agent 2.
    /// </summary>
    public int MaximumVideoAgeDays { get; set; } = 7;

    /// <summary>
    /// Minimum total views a video must have to be enqueued to Agent 2
    /// (early quality gate — saves AI extraction cost on junk).
    /// </summary>
    public long MinimumViewsForEnqueue { get; set; } = 10_000;

    /// <summary>
    /// Search result ordering. "date" biases discovery toward newly
    /// published videos instead of evergreen/most-popular content.
    /// </summary>
    public string SearchOrder { get; set; } = "date";

    /// <summary>
    /// YouTube search videoDuration filter. "short" biases toward
    /// short-form videos (≤4 minutes; Shorts typically ≤60s).
    /// The existing IsShortDuration() check still applies as the
    /// authoritative Shorts gate.
    /// </summary>
    public string SearchVideoDuration { get; set; } = "short";

    /// <summary>
    /// Search freshness window in days. When > 0, the search includes
    /// publishedAfter = now - SearchWindowDays so only recently published
    /// videos are returned.
    /// </summary>
    public int SearchWindowDays { get; set; } = 7;
}
