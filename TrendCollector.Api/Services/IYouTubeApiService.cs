using System.Text.Json;

namespace TrendCollector.Api.Services;

/// <summary>
/// Client for the YouTube Data API v3.
/// All methods return the raw JSON document so no API field is ever lost.
/// </summary>
public interface IYouTubeApiService
{
    /// <summary>Searches videos for a keyword and returns the raw search response.</summary>
    Task<JsonDocument> SearchAsync(
        string keyword,
        string language,
        string country,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>Returns full video details (snippet, contentDetails, statistics, status, ...) as raw JSON.</summary>
    Task<JsonDocument> GetVideosAsync(
        IEnumerable<string> videoIds,
        CancellationToken cancellationToken = default);

    /// <summary>Returns full channel details (snippet, statistics, ...) as raw JSON.</summary>
    Task<JsonDocument> GetChannelsAsync(
        IEnumerable<string> channelIds,
        CancellationToken cancellationToken = default);
}