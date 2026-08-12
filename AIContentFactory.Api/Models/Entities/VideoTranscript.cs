namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Transcript/captions of a video, used as primary AI input.
/// </summary>
public sealed class VideoTranscript
{
    public long Id { get; set; }

    /// <summary>FK to trending_videos.id.</summary>
    public long VideoId { get; set; }

    public string Transcript { get; set; } = string.Empty;

    /// <summary>Transcript language, e.g. "en", "id".</summary>
    public string? Language { get; set; }

    /// <summary>Source of the transcript, e.g. "youtube_captions".</summary>
    public string? Source { get; set; }

    /// <summary>
    /// AI-assessed quality score (0-100) of the polished transcript.
    /// Set by the transcript polishing flow (Reconstruct + AI).
    /// </summary>
    public int? TranscriptScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
