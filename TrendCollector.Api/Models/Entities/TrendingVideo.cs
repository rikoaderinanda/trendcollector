namespace TrendCollector.Api.Models.Entities;

/// <summary>
/// Complete metadata of a trending video collected from a platform.
/// </summary>
public sealed class TrendingVideo
{
    public long Id { get; set; }

    /// <summary>FK to platforms.id.</summary>
    public int PlatformId { get; set; }

    /// <summary>Native video id on the platform, e.g. "dQw4w9WgXcQ".</summary>
    public string PlatformVideoId { get; set; } = string.Empty;

    /// <summary>FK to channels.id. Null until the channel is saved.</summary>
    public long? ChannelId { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Canonical url of the video on the platform.</summary>
    public string? Url { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>ISO 8601 duration, e.g. "PT12M34S".</summary>
    public string? Duration { get; set; }

    /// <summary>Human readable category name.</summary>
    public string? Category { get; set; }

    public string[]? Tags { get; set; }

    /// <summary>Video language, e.g. "id", "en".</summary>
    public string? Language { get; set; }

    public bool? CaptionAvailable { get; set; }

    /// <summary>hd / sd / high / ...</summary>
    public string? Definition { get; set; }

    /// <summary>2d / 3d</summary>
    public string? Dimension { get; set; }

    /// <summary>rectangular / 360 / ...</summary>
    public string? Projection { get; set; }

    public string? ThumbnailDefaultUrl { get; set; }
    public string? ThumbnailMediumUrl { get; set; }
    public string? ThumbnailHighUrl { get; set; }
    public string? ThumbnailStandardUrl { get; set; }
    public string? ThumbnailMaxresUrl { get; set; }

    /// <summary>When the video was first collected.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Full platform API response as JSON.</summary>
    public string? RawJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}