using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for persisted video transcripts.
/// </summary>
public interface IVideoTranscriptRepository
{
    /// <summary>Inserts a transcript for a video. Replaces any existing transcript (upsert).</summary>
    Task UpsertAsync(VideoTranscript transcript, CancellationToken cancellationToken = default);

    /// <summary>Gets the transcript of a video, or null when not available.</summary>
    Task<VideoTranscript?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default);
}