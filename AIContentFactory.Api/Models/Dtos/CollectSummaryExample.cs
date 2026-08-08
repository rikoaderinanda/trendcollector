using Swashbuckle.AspNetCore.Filters;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Swagger example for a completed collection job summary.
/// </summary>
public sealed class CollectSummaryExample : IExamplesProvider<CollectSummary>
{
    public CollectSummary GetExamples()
    {
        return new CollectSummary
        {
            JobId = 1,
            Keyword = "AI",
            Country = "ID",
            Language = "id",
            TotalCollected = 20,
            TotalSaved = 18,
            TotalSkipped = 2,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            FinishedAt = DateTimeOffset.UtcNow,
            DurationMs = 120340
        };
    }
}