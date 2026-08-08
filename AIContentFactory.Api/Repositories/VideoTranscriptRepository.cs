using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IVideoTranscriptRepository" />
public sealed class VideoTranscriptRepository : IVideoTranscriptRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public VideoTranscriptRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task UpsertAsync(VideoTranscript transcript, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO video_transcripts (video_id, transcript, language, source)
            VALUES (@VideoId, @Transcript, @Language, @Source)
            ON CONFLICT (video_id) DO UPDATE
            SET transcript = EXCLUDED.transcript,
                language   = EXCLUDED.language,
                source     = EXCLUDED.source;
            """;

        await connection.ExecuteAsync(sql, transcript, commandTimeout: 30);
    }

    public async Task<VideoTranscript?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id          AS Id,
                video_id    AS VideoId,
                transcript  AS Transcript,
                language    AS Language,
                source      AS Source,
                created_at  AS CreatedAt
            FROM video_transcripts
            WHERE video_id = @VideoId;
            """;

        return await connection.QuerySingleOrDefaultAsync<VideoTranscript>(sql,
            new { VideoId = videoId }, commandTimeout: 30);
    }
}