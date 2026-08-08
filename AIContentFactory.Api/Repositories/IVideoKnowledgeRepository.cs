using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for structured video knowledge.
/// </summary>
public interface IVideoKnowledgeRepository
{
    /// <summary>Inserts or replaces knowledge for a video (one-to-one).</summary>
    Task UpsertAsync(VideoKnowledge knowledge, CancellationToken cancellationToken = default);

    /// <summary>Gets structured knowledge for a video, or null when not available.</summary>
    Task<VideoKnowledge?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default);
}