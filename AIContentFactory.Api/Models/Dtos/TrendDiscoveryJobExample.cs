using Swashbuckle.AspNetCore.Filters;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Swagger example for a list of trend discovery jobs.
/// </summary>
public sealed class TrendDiscoveryJobExample : IExamplesProvider<IEnumerable<TrendDiscoveryJob>>
{
    public IEnumerable<TrendDiscoveryJob> GetExamples()
    {
        return new[]
        {
            new TrendDiscoveryJob
            {
                Id = 12,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
                FinishedAt = DateTimeOffset.UtcNow,
                DurationMs = 182450,
                Status = TrendDiscoveryJobStatus.Completed,
                TotalKeywords = 18,
                ErrorMessage = null,
                Source = DiscoverySource.AI
            },
            new TrendDiscoveryJob
            {
                Id = 11,
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
                FinishedAt = DateTimeOffset.UtcNow.AddMinutes(-57),
                DurationMs = 175000,
                Status = TrendDiscoveryJobStatus.Completed,
                TotalKeywords = 15,
                ErrorMessage = null,
                Source = DiscoverySource.AI
            }
        };
    }
}

