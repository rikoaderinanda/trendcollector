using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IVideoKnowledgeRawRepository" />
public sealed class VideoKnowledgeRawRepository : IVideoKnowledgeRawRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public VideoKnowledgeRawRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InsertAsync(VideoKnowledgeRaw raw, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO video_knowledge_raw (
                video_id, provider, model, prompt, response,
                execution_time_ms, tokens_input, tokens_output
            )
            VALUES (
                @VideoId, @Provider, @Model, @Prompt, @Response,
                @ExecutionTimeMs, @TokensInput, @TokensOutput
            );
            """;

        await connection.ExecuteAsync(sql, raw, commandTimeout: 30);
    }
}