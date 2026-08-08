using Swashbuckle.AspNetCore.Filters;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Swagger example for a list of discovered trend keywords.
/// </summary>
public sealed class TrendKeywordExample : IExamplesProvider<IEnumerable<TrendKeyword>>
{
    public IEnumerable<TrendKeyword> GetExamples()
    {
        return new[]
        {
            new TrendKeyword
            {
                Id = 1,
                Keyword = "OpenAI Codex",
                Niche = "Artificial Intelligence",
                Country = "Global",
                Language = "en",
                Priority = 96,
                DiscoveryReason = "Rapid growth among developers.",
                Source = DiscoverySource.AI,
                Status = KeywordStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new TrendKeyword
            {
                Id = 2,
                Keyword = "DeepSeek R1",
                Niche = "Artificial Intelligence",
                Country = "Global",
                Language = "en",
                Priority = 91,
                DiscoveryReason = "Viral open-source reasoning model.",
                Source = DiscoverySource.AI,
                Status = KeywordStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };
    }
}

