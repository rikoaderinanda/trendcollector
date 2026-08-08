using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Read-only access to video metadata produced by Agent 1 (Trend Collector).
/// </summary>
public interface IVideoMetadataRepository
{
    /// <summary>Gets a video by its internal database id, or null when not found.</summary>
    Task<TrendingVideoMetadata?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Gets the platform video id for a video, or null when not found.</summary>
    Task<string?> GetPlatformVideoIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Gets the latest statistics snapshot of a video, or null when not found.</summary>
    Task<VideoStatisticsSnapshot?> GetLatestStatisticsAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a video exists in trending_videos.
    /// </summary>
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
}