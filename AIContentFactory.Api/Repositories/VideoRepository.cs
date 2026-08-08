using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IVideoRepository" />
public sealed class VideoRepository : IVideoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public VideoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ExistsAsync(int platformId, string platformVideoId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM trending_videos
                WHERE platform_id = @PlatformId AND platform_video_id = @PlatformVideoId
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(sql,
            new { PlatformId = platformId, PlatformVideoId = platformVideoId }, commandTimeout: 30);
    }

    public async Task<long> InsertAsync(TrendingVideo video, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO trending_videos (
                platform_id, platform_video_id, channel_id,
                title, description, url, published_at, duration, category,
                tags, language, caption_available, definition, dimension, projection,
                thumbnail_default_url, thumbnail_medium_url, thumbnail_high_url,
                thumbnail_standard_url, thumbnail_maxres_url,
                processed_at, raw_json
            )
            VALUES (
                @PlatformId, @PlatformVideoId, @ChannelId,
                @Title, @Description, @Url, @PublishedAt, @Duration, @Category,
                @Tags, @Language, @CaptionAvailable, @Definition, @Dimension, @Projection,
                @ThumbnailDefaultUrl, @ThumbnailMediumUrl, @ThumbnailHighUrl,
                @ThumbnailStandardUrl, @ThumbnailMaxresUrl,
                @ProcessedAt, @RawJson::jsonb
            )
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql, video, commandTimeout: 30);
    }

    public async Task InsertStatisticsAsync(VideoStatistics statistics, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO video_statistics (
                video_id, views, likes, comments, favorites,
                engagement_rate, like_ratio, comment_ratio,
                view_per_day, video_age_days, captured_at,
                views_per_hour, like_velocity, comment_velocity,
                growth_score, previous_snapshot_id
            )
            VALUES (
                @VideoId, @Views, @Likes, @Comments, @Favorites,
                @EngagementRate, @LikeRatio, @CommentRatio,
                @ViewPerDay, @VideoAgeDays, @CapturedAt,
                @ViewsPerHour, @LikeVelocity, @CommentVelocity,
                @GrowthScore, @PreviousSnapshotId
            );
            """;

        await connection.ExecuteAsync(sql, statistics, commandTimeout: 30);
    }

    public async Task<long> InsertWithStatisticsAsync(TrendingVideo video, VideoStatistics statistics, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertVideoSql = """
            INSERT INTO trending_videos (
                platform_id, platform_video_id, channel_id,
                title, description, url, published_at, duration, category,
                tags, language, caption_available, definition, dimension, projection,
                thumbnail_default_url, thumbnail_medium_url, thumbnail_high_url,
                thumbnail_standard_url, thumbnail_maxres_url,
                processed_at, raw_json
            )
            VALUES (
                @PlatformId, @PlatformVideoId, @ChannelId,
                @Title, @Description, @Url, @PublishedAt, @Duration, @Category,
                @Tags, @Language, @CaptionAvailable, @Definition, @Dimension, @Projection,
                @ThumbnailDefaultUrl, @ThumbnailMediumUrl, @ThumbnailHighUrl,
                @ThumbnailStandardUrl, @ThumbnailMaxresUrl,
                @ProcessedAt, @RawJson::jsonb
            )
            RETURNING id;
            """;

        var videoId = await connection.ExecuteScalarAsync<long>(
            insertVideoSql, video, transaction: transaction, commandTimeout: 30);

        statistics.VideoId = videoId;

        const string insertStatsSql = """
            INSERT INTO video_statistics (
                video_id, views, likes, comments, favorites,
                engagement_rate, like_ratio, comment_ratio,
                view_per_day, video_age_days, captured_at,
                views_per_hour, like_velocity, comment_velocity,
                growth_score, previous_snapshot_id
            )
            VALUES (
                @VideoId, @Views, @Likes, @Comments, @Favorites,
                @EngagementRate, @LikeRatio, @CommentRatio,
                @ViewPerDay, @VideoAgeDays, @CapturedAt,
                @ViewsPerHour, @LikeVelocity, @CommentVelocity,
                @GrowthScore, @PreviousSnapshotId
            );
            """;

        await connection.ExecuteAsync(insertStatsSql, statistics, transaction: transaction, commandTimeout: 30);

        await transaction.CommitAsync(cancellationToken);
        return videoId;
    }

    public async Task<TrendingVideo?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                    AS Id,
                platform_id           AS PlatformId,
                platform_video_id     AS PlatformVideoId,
                channel_id            AS ChannelId,
                title                 AS Title,
                description           AS Description,
                url                   AS Url,
                published_at          AS PublishedAt,
                duration              AS Duration,
                category              AS Category,
                tags                  AS Tags,
                language              AS Language,
                caption_available     AS CaptionAvailable,
                definition            AS Definition,
                dimension             AS Dimension,
                projection            AS Projection,
                thumbnail_default_url AS ThumbnailDefaultUrl,
                thumbnail_medium_url  AS ThumbnailMediumUrl,
                thumbnail_high_url    AS ThumbnailHighUrl,
                thumbnail_standard_url AS ThumbnailStandardUrl,
                thumbnail_maxres_url  AS ThumbnailMaxresUrl,
                processed_at          AS ProcessedAt,
                raw_json              AS RawJson,
                created_at            AS CreatedAt,
                updated_at            AS UpdatedAt
            FROM trending_videos
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<TrendingVideo>(sql,
            new { Id = id }, commandTimeout: 30);
    }

    public async Task<VideoStatistics?> GetLatestStatisticsAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                   AS Id,
                video_id             AS VideoId,
                views                AS Views,
                likes                AS Likes,
                comments             AS Comments,
                favorites            AS Favorites,
                engagement_rate      AS EngagementRate,
                like_ratio           AS LikeRatio,
                comment_ratio        AS CommentRatio,
                view_per_day         AS ViewPerDay,
                video_age_days       AS VideoAgeDays,
                captured_at          AS CapturedAt,
                views_per_hour       AS ViewsPerHour,
                like_velocity        AS LikeVelocity,
                comment_velocity     AS CommentVelocity,
                growth_score         AS GrowthScore,
                previous_snapshot_id AS PreviousSnapshotId
            FROM video_statistics
            WHERE video_id = @VideoId
            ORDER BY captured_at DESC
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<VideoStatistics>(sql,
            new { VideoId = videoId }, commandTimeout: 30);
    }

    public async Task<IEnumerable<TrendingVideo>> ListAsync(string? language, DateTime? date, int limit, int offset, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                    AS Id,
                platform_id           AS PlatformId,
                platform_video_id     AS PlatformVideoId,
                channel_id            AS ChannelId,
                title                 AS Title,
                description           AS Description,
                url                   AS Url,
                published_at          AS PublishedAt,
                duration              AS Duration,
                category              AS Category,
                tags                  AS Tags,
                language              AS Language,
                caption_available     AS CaptionAvailable,
                definition            AS Definition,
                dimension             AS Dimension,
                projection            AS Projection,
                thumbnail_default_url AS ThumbnailDefaultUrl,
                thumbnail_medium_url  AS ThumbnailMediumUrl,
                thumbnail_high_url    AS ThumbnailHighUrl,
                thumbnail_standard_url AS ThumbnailStandardUrl,
                thumbnail_maxres_url  AS ThumbnailMaxresUrl,
                processed_at          AS ProcessedAt,
                raw_json              AS RawJson,
                created_at            AS CreatedAt,
                updated_at            AS UpdatedAt
            FROM trending_videos
            WHERE (@Language IS NULL OR language = @Language)
              AND (@Date::date IS NULL OR created_at::date = @Date::date)
            ORDER BY published_at DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        return await connection.QueryAsync<TrendingVideo>(sql,
            new { Language = language, Date = date, Limit = limit, Offset = offset }, commandTimeout: 30);
    }

    public async Task<IEnumerable<TrendingVideo>> ListRecentAsync(int days,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT
                               id                    AS Id,
                               platform_id           AS PlatformId,
                               platform_video_id     AS PlatformVideoId,
                               channel_id            AS ChannelId,
                               title                 AS Title,
                               description           AS Description,
                               url                   AS Url,
                               published_at          AS PublishedAt,
                               duration              AS Duration,
                               category              AS Category,
                               tags                  AS Tags,
                               language              AS Language,
                               caption_available     AS CaptionAvailable,
                               definition            AS Definition,
                               dimension             AS Dimension,
                               projection            AS Projection,
                               thumbnail_default_url AS ThumbnailDefaultUrl,
                               thumbnail_medium_url  AS ThumbnailMediumUrl,
                               thumbnail_high_url    AS ThumbnailHighUrl,
                               thumbnail_standard_url AS ThumbnailStandardUrl,
                               thumbnail_maxres_url  AS ThumbnailMaxresUrl,
                               processed_at          AS ProcessedAt,
                               raw_json              AS RawJson,
                               created_at            AS CreatedAt,
                               updated_at            AS UpdatedAt
                           FROM trending_videos
                           WHERE created_at >= now() - make_interval(days => @Days)
                           ORDER BY created_at DESC;
                           """;

        return await connection.QueryAsync<TrendingVideo>(sql,
            new { Days = days }, commandTimeout: 30);
    }
}

