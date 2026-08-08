using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Transcript;

/// <summary>
/// Retrieves the transcript/captions of a YouTube video without downloading it.
/// </summary>
public interface ITranscriptProvider
{
    /// <summary>
    /// Gets the transcript for a platform video id.
    /// Returns null when captions are unavailable (disabled or no caption track).
    /// </summary>
    Task<VideoTranscript?> GetTranscriptAsync(
        string platformVideoId,
        string? preferredLanguage = null,
        CancellationToken cancellationToken = default);
}