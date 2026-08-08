namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Channel information collected from a platform.
/// </summary>
public sealed class Channel
{
    public long Id { get; set; }

    /// <summary>FK to platforms.id.</summary>
    public int PlatformId { get; set; }

    /// <summary>Native channel id on the platform, e.g. "UC...".</summary>
    public string PlatformChannelId { get; set; } = string.Empty;

    public string? Name { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "ID".</summary>
    public string? Country { get; set; }

    public long? SubscriberCount { get; set; }
    public int? VideoCount { get; set; }
    public long? TotalViews { get; set; }

    /// <summary>When the channel was created on the platform.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Channel handle / custom url, e.g. "@username".</summary>
    public string? CustomUrl { get; set; }

    /// <summary>Full platform API response as JSON.</summary>
    public string? RawJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}