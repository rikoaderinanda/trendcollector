using Dapper;
using AIContentFactory.Api.Data;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IQuotaRepository" />
public sealed class QuotaRepository : IQuotaRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public QuotaRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> GetCallCountAsync(DateTime usageDate, string endpoint, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT call_count
            FROM daily_api_usage
            WHERE usage_date = @UsageDate::date AND endpoint = @Endpoint;
            """;

        return await connection.ExecuteScalarAsync<int?>(sql,
            new { UsageDate = usageDate, Endpoint = endpoint }, commandTimeout: 30) ?? 0;
    }

    public async Task IncrementCallCountAsync(DateTime usageDate, string endpoint, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO daily_api_usage (usage_date, endpoint, call_count)
            VALUES (@UsageDate::date, @Endpoint, 1)
            ON CONFLICT (usage_date, endpoint)
            DO UPDATE SET call_count = daily_api_usage.call_count + 1;
            """;

        await connection.ExecuteAsync(sql,
            new { UsageDate = usageDate, Endpoint = endpoint }, commandTimeout: 30);
    }
}