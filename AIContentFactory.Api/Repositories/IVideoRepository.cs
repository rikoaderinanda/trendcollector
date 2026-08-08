using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for trending videos and their statistics.
/// </summary>
public interface IVideoRepository
{
    /// <summary>Returns true when a video with the same platform id already exists.</summary>
    Task<bool> ExistsAsync(int platformId, string platformVideoId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new video and returns its id.</summary>
    Task<long> InsertAsync(TrendingVideo video, CancellationToken cancellationToken = default);

    /// <summary>Inserts a statistics snapshot for a video.</summary>
    Task InsertStatisticsAsync(VideoStatistics statistics, CancellationToken cancellationToken = default);

    /// <summary>Inserts a video together with its first statistics snapshot in one transaction.</summary>
    Task<long> InsertWithStatisticsAsync(TrendingVideo video, VideoStatistics statistics, CancellationToken cancellationToken = default);

    /// <summary>Gets a video by its internal id, or null when not found.</summary>
    Task<TrendingVideo?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Gets the most recent statistics snapshot of a video.</summary>
    Task<VideoStatistics?> GetLatestStatisticsAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists videos with optional language filter, optional calendar date filter (started/created day), and pagination.
    /// </summary>
    Task<IEnumerable<TrendingVideo>> ListAsync(string? language, DateTime? date, int limit, int offset, CancellationToken cancellationToken = default);

    /// <summary>Lists videos collected within the last <paramref name="days"/> days (by created_at).</summary>
    Task<IEnumerable<TrendingVideo>> ListRecentAsync(int days, CancellationToken cancellationToken = default);
}