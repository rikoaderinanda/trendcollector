using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IChannelRepository" />
public sealed class ChannelRepository : IChannelRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ChannelRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> UpsertAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO channels (
                platform_id, platform_channel_id, name, country,
                subscriber_count, video_count, total_views,
                published_at, custom_url, raw_json, updated_at
            )
            VALUES (
                @PlatformId, @PlatformChannelId, @Name, @Country,
                @SubscriberCount, @VideoCount, @TotalViews,
                @PublishedAt, @CustomUrl, @RawJson::jsonb, now()
            )
            ON CONFLICT (platform_id, platform_channel_id)
            DO UPDATE SET
                name             = EXCLUDED.name,
                country          = EXCLUDED.country,
                subscriber_count = EXCLUDED.subscriber_count,
                video_count      = EXCLUDED.video_count,
                total_views      = EXCLUDED.total_views,
                published_at     = EXCLUDED.published_at,
                custom_url       = EXCLUDED.custom_url,
                raw_json         = EXCLUDED.raw_json,
                updated_at       = now()
            RETURNING id;
            """;

        return await connection.QuerySingleAsync<long>(
            sql,
            new
            {
                channel.PlatformId,
                channel.PlatformChannelId,
                channel.Name,
                channel.Country,
                channel.SubscriberCount,
                channel.VideoCount,
                channel.TotalViews,
                channel.PublishedAt,
                channel.CustomUrl,
                channel.RawJson
            },
            commandTimeout: 30);
    }
}