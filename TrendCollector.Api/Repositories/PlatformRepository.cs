using Dapper;
using TrendCollector.Api.Data;
using TrendCollector.Api.Models.Entities;

namespace TrendCollector.Api.Repositories;

/// <inheritdoc cref="IPlatformRepository" />
public sealed class PlatformRepository : IPlatformRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public PlatformRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> GetOrCreateAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO platforms (code, name)
            VALUES (@Code, @Name)
            ON CONFLICT (code) DO NOTHING;

            SELECT id FROM platforms WHERE code = @Code;
            """;

        return await connection.QuerySingleAsync<int>(sql, new { Code = code, Name = code }, commandTimeout: 30);
    }
}