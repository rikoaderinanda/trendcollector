namespace AIContentFactory.Api.AI;

/// <summary>
/// Request payload for AI knowledge extraction.
/// Contains all the raw material the AI needs to understand a video.
/// </summary>
public sealed class KnowledgeExtractionRequest
{
    public long VideoId { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string[]? Tags { get; set; }
    public string? Language { get; set; }

    /// <summary>Formatted statistics summary, e.g. "views: 1.2M, likes: 45K".</summary>
    public string? Statistics { get; set; }

    /// <summary>Transcript text of the video.</summary>
    public string? Transcript { get; set; }
}