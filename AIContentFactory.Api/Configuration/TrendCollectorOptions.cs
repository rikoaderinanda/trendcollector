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
}