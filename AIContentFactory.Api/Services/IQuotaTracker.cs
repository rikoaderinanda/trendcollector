namespace AIContentFactory.Api.Services;

/// <summary>
/// Tracks daily YouTube Data API v3 quota usage and decides whether
/// search.list is still allowed. When the cap is reached the collector
/// must switch to Tracking Mode (videos.list only).
/// </summary>
public interface IQuotaTracker
{
    /// <summary>Const name of the search.list endpoint used in daily_api_usage.</summary>
    const string SearchEndpoint = "search.list";

    /// <summary>Const name of the videos.list endpoint used in daily_api_usage.</summary>
    const string VideosEndpoint = "videos.list";

    /// <summary>Returns the number of search.list calls made today (UTC).</summary>
    Task<int> GetSearchCallCountTodayAsync(CancellationToken cancellationToken = default);

    /// <summary>Records one search.list call for today (UTC).</summary>
    Task IncrementSearchCallCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Records one videos.list call for today (UTC).</summary>
    Task IncrementVideosCallCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the number of search.list calls made today is
    /// greater than or equal to the configured daily cap, meaning the
    /// collector must run in Tracking Mode and stop calling search.list.
    /// </summary>
    Task<bool> IsSearchQuotaExhaustedAsync(CancellationToken cancellationToken = default);
}