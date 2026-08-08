namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Never-discarded raw AI response. Keeps the full prompt and response
/// for audit, debugging, and future re-processing.
/// </summary>
public sealed class VideoKnowledgeRaw
{
    public long Id { get; set; }

    /// <summary>FK to trending_videos.id.</summary>
    public long VideoId { get; set; }

    /// <summary>Provider display name, e.g. "DeepSeek".</summary>
    public string? Provider { get; set; }

    /// <summary>Model name used, e.g. "deepseek-chat".</summary>
    public string? Model { get; set; }

    /// <summary>Full prompt sent to the provider.</summary>
    public string? Prompt { get; set; }

    /// <summary>Raw response content returned by the provider.</summary>
    public string? Response { get; set; }

    public long? ExecutionTimeMs { get; set; }

    public int? TokensInput { get; set; }

    public int? TokensOutput { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}