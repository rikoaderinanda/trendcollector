using System.Data;
using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models;
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

    public async Task<IEnumerable<AIContentFactory.Api.Models.Dtos.VideoListItemDto>> ListWithLatestStatsAsync(
        VideoListQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        // Whitelist of allowed sort columns mapped to SQL expressions.
        // This prevents SQL injection in the ORDER BY clause.
        Dictionary<string, string> sortColumns = new()
        {
            ["published_at"] = "tv.published_at",
            ["views"] = "vs.views",
            ["likes"] = "vs.likes",
            ["comments"] = "vs.comments",
            ["favorites"] = "vs.favorites",
            ["engagement_rate"] = "vs.engagement_rate",
            ["view_per_day"] = "vs.view_per_day",
            ["video_age_days"] = "vs.video_age_days",
            ["captured_at"] = "vs.captured_at",
            ["views_per_hour"] = "vs.views_per_hour",
            ["like_velocity"] = "vs.like_velocity",
            ["comment_velocity"] = "vs.comment_velocity",
            ["growth_score"] = "vs.growth_score",
        };

        var sortColumn = query.SortBy is not null && sortColumns.TryGetValue(query.SortBy, out var mapped)
            ? mapped
            : "vs.captured_at";
        var sortDirection = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "ASC"
            : "DESC";

        // Build conditions dynamically. NULL/0 range params are ignored so an
        // absent min (null) or max (null) simply doesn't constrain the query.
        // All parameters are given an explicit DbType so Npgsql can infer the
        // PostgreSQL type even when the value is null (avoids error 42P08).
        var conditions = new List<string>();
        var parameters = new Dapper.DynamicParameters();
        parameters.Add("Language", query.Language, DbType.String);
        parameters.Add("Date", query.Date, DbType.DateTime);
        parameters.Add("Limit", Math.Clamp(query.Limit, 1, 100), DbType.Int32);
        parameters.Add("Offset", Math.Max(query.Offset, 0), DbType.Int32);

        conditions.Add("(@Language::text IS NULL OR tv.language = @Language::text)");
        conditions.Add("(@Date::date IS NULL OR tv.created_at::date = @Date::date)");

        void AddLongRange(string column, long? min, long? max)
        {
            if (min is not null)
            {
                var p = $"Min{column}";
                parameters.Add(p, min.Value, DbType.Int64);
                conditions.Add($"vs.{column} >= @{p}");
            }

            if (max is not null)
            {
                var p = $"Max{column}";
                parameters.Add(p, max.Value, DbType.Int64);
                conditions.Add($"vs.{column} <= @{p}");
            }
        }

        void AddDecimalRange(string column, decimal? min, decimal? max)
        {
            if (min is not null)
            {
                var p = $"Min{column}";
                parameters.Add(p, min.Value, DbType.Decimal);
                conditions.Add($"vs.{column} >= @{p}");
            }

            if (max is not null)
            {
                var p = $"Max{column}";
                parameters.Add(p, max.Value, DbType.Decimal);
                conditions.Add($"vs.{column} <= @{p}");
            }
        }

        AddLongRange("views", query.MinViews, query.MaxViews);
        AddLongRange("likes", query.MinLikes, query.MaxLikes);
        AddLongRange("comments", query.MinComments, query.MaxComments);
        AddLongRange("favorites", query.MinFavorites, query.MaxFavorites);
        AddDecimalRange("engagement_rate", query.MinEngagementRate, query.MaxEngagementRate);
        AddDecimalRange("view_per_day", query.MinViewPerDay, query.MaxViewPerDay);
        AddDecimalRange("video_age_days", query.MinVideoAgeDays, query.MaxVideoAgeDays);

        // Captured-at time range.
        if (query.CapturedAfter is not null)
        {
            parameters.Add("CapturedAfter", query.CapturedAfter, DbType.DateTimeOffset);
            conditions.Add("vs.captured_at >= @CapturedAfter");
        }

        if (query.CapturedBefore is not null)
        {
            parameters.Add("CapturedBefore", query.CapturedBefore, DbType.DateTimeOffset);
            conditions.Add("vs.captured_at <= @CapturedBefore");
        }

        AddDecimalRange("views_per_hour", query.MinViewsPerHour, query.MaxViewsPerHour);
        AddDecimalRange("like_velocity", query.MinLikeVelocity, query.MaxLikeVelocity);
        AddDecimalRange("comment_velocity", query.MinCommentVelocity, query.MaxCommentVelocity);
        AddDecimalRange("growth_score", query.MinGrowthScore, query.MaxGrowthScore);

        var whereClause = string.Join("\n  AND ", conditions);

        // Single query: each video joined to its latest statistics snapshot
        // (by captured_at). Dynamic ORDER BY supports all statistics metrics.
        var sql = $"""
                   SELECT
                       tv.id                             AS Id,
                       tv.title                          AS Title,
                       tv.description                    AS Description,
                       tv.url                            AS Url,
                       tv.published_at                   AS PublishedAt,
                       tv.duration                       AS Duration,
                       tv.category                       AS Category,
                       tv.tags                           AS Tags,
                       tv.language                       AS Language,
                       tv.thumbnail_high_url             AS ThumbnailHighUrl,
                       tv.processed_at                   AS ProcessedAt,
                       vs.views                          AS Views,
                       vs.likes                          AS Likes,
                       vs.comments                       AS Comments,
                       vs.favorites                      AS Favorites,
                       vs.engagement_rate                AS EngagementRate,
                       vs.like_ratio                     AS LikeRatio,
                       vs.comment_ratio                  AS CommentRatio,
                       vs.view_per_day                   AS ViewPerDay,
                       vs.video_age_days                 AS VideoAgeDays,
                       vs.captured_at                    AS StatisticsCapturedAt,
                       vs.views_per_hour                 AS ViewsPerHour,
                       vs.like_velocity                  AS LikeVelocity,
                       vs.comment_velocity               AS CommentVelocity,
                       vs.growth_score                   AS GrowthScore
                   FROM trending_videos tv
                   LEFT JOIN LATERAL (
                       SELECT views, likes, comments, favorites, engagement_rate, like_ratio, comment_ratio,
                              view_per_day, video_age_days, captured_at,
                              views_per_hour, like_velocity, comment_velocity, growth_score
                       FROM video_statistics
                       WHERE video_id = tv.id
                       ORDER BY captured_at DESC
                       LIMIT 1
                   ) vs ON true
                   WHERE {whereClause}
                   ORDER BY {sortColumn} {sortDirection} NULLS LAST, tv.processed_at DESC
                   LIMIT @Limit OFFSET @Offset;
                   """;

        return await connection.QueryAsync<AIContentFactory.Api.Models.Dtos.VideoListItemDto>(sql,
            parameters, commandTimeout: 30);
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

    public async Task<IEnumerable<VideoStatistics>> GetStatisticsHistoryAsync(long videoId,
        CancellationToken cancellationToken = default)
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
                           ORDER BY captured_at ASC;
                           """;

        return await connection.QueryAsync<VideoStatistics>(sql,
            new { VideoId = videoId }, commandTimeout: 30);
    }
}