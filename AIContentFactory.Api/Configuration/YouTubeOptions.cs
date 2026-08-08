namespace AIContentFactory.Api.Configuration;

/// <summary>
/// Options for the YouTube Data API v3.
/// Bound from the "YouTube" configuration section.
/// </summary>
public sealed class YouTubeOptions
{
    public const string SectionName = "YouTube";

    /// <summary>YouTube Data API v3 key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL of the search endpoint.</summary>
    public string SearchEndpoint { get; set; } = string.Empty;

    /// <summary>Base URL of the videos endpoint.</summary>
    public string VideosEndpoint { get; set; } = string.Empty;

    /// <summary>Base URL of the channels endpoint.</summary>
    public string ChannelsEndpoint { get; set; } = string.Empty;
}