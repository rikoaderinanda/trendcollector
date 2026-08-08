namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Daily API call counters for YouTube Data API v3 endpoints.
/// Used to enforce the search.list quota cap and switch the collector
/// from Discovery mode to Tracking mode.
/// </summary>
public sealed class DailyApiUsage
{
    public long Id { get; set; }

    /// <summary>Calendar date (UTC) the counters apply to.</summary>
    public DateTime UsageDate { get; set; }

    /// <summary>YouTube API endpoint, e.g. "search.list", "videos.list".</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Number of API calls made to this endpoint on this date.</summary>
    public int CallCount { get; set; }
}