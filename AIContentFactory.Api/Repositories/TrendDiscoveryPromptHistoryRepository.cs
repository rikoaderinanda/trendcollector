using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="ITrendDiscoveryPromptHistoryRepository" />
public sealed class TrendDiscoveryPromptHistoryRepository : ITrendDiscoveryPromptHistoryRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public TrendDiscoveryPromptHistoryRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> CreateAsync(TrendDiscoveryPromptHistory history, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO trend_discovery_prompt_history (
                job_id, prompt, ai_response, provider, model,
                tokens_input, tokens_output, execution_time_ms)
            VALUES (
                @JobId, @Prompt, @AiResponse, @Provider, @Model,
                @TokensInput, @TokensOutput, @ExecutionTimeMs)
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql,
            new
            {
                history.JobId,
                history.Prompt,
                history.AiResponse,
                history.Provider,
                history.Model,
                history.TokensInput,
                history.TokensOutput,
                history.ExecutionTimeMs
            }, commandTimeout: 30);
    }
}