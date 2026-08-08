using System.Text.Json.Serialization;

namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Determines how TrendCollector interacts with the YouTube Data API.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectorMode
{
    /// <summary>
    /// Default mode: uses search.list to discover new videos for keywords.
    /// </summary>
    Discovery = 0,

    /// <summary>
    /// Quota-preserving mode: disables search.list and only refreshes
    /// statistics of already-collected videos via videos.list.
    /// </summary>
    Tracking = 1
}