namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Read-only mirror of a trending video produced by Agent 1 (Trend Collector).
/// Used as AI input for knowledge extraction.
/// </summary>
public sealed class TrendingVideoMetadata
{
    public long Id { get; set; }

    public int PlatformId { get; set; }

    /// <summary>Native video id on the platform, e.g. "dQw4w9WgXcQ".</summary>
    public string PlatformVideoId { get; set; } = string.Empty;

    public long? ChannelId { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? Duration { get; set; }
    public string? Category { get; set; }
    public string[]? Tags { get; set; }
    public string? Language { get; set; }
    public bool? CaptionAvailable { get; set; }
}