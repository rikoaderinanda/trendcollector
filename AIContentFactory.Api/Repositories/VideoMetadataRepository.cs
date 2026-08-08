using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IVideoMetadataRepository" />
public sealed class VideoMetadataRepository : IVideoMetadataRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public VideoMetadataRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<TrendingVideoMetadata?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                  AS Id,
                platform_id         AS PlatformId,
                platform_video_id   AS PlatformVideoId,
                channel_id          AS ChannelId,
                title               AS Title,
                description         AS Description,
                url                 AS Url,
                published_at        AS PublishedAt,
                duration            AS Duration,
                category            AS Category,
                tags                AS Tags,
                language            AS Language,
                caption_available   AS CaptionAvailable
            FROM trending_videos
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<TrendingVideoMetadata>(sql,
            new { Id = id }, commandTimeout: 30);
    }

    public async Task<string?> GetPlatformVideoIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT platform_video_id
            FROM trending_videos
            WHERE id = @Id;
            """;

        return await connection.ExecuteScalarAsync<string?>(sql, new { Id = id }, commandTimeout: 30);
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM trending_videos WHERE id = @Id
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = id }, commandTimeout: 30);
    }

    public async Task<VideoStatisticsSnapshot?> GetLatestStatisticsAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                video_id        AS VideoId,
                views           AS Views,
                likes           AS Likes,
                comments        AS Comments,
                favorites       AS Favorites,
                engagement_rate AS EngagementRate,
                like_ratio      AS LikeRatio,
                comment_ratio   AS CommentRatio,
                view_per_day    AS ViewPerDay,
                video_age_days  AS VideoAgeDays,
                captured_at     AS CapturedAt
            FROM video_statistics
            WHERE video_id = @VideoId
            ORDER BY captured_at DESC
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<VideoStatisticsSnapshot>(sql,
            new { VideoId = videoId }, commandTimeout: 30);
    }
}