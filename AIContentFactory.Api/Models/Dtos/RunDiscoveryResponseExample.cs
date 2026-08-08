using Swashbuckle.AspNetCore.Filters;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Swagger example for a completed trend discovery run.
/// </summary>
public sealed class RunDiscoveryResponseExample : IExamplesProvider<RunDiscoveryResponse>
{
    public RunDiscoveryResponse GetExamples()
    {
        return new RunDiscoveryResponse
        {
            JobId = 12,
            Status = TrendDiscoveryJobStatus.Completed,
            TotalKeywords = 18,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            FinishedAt = DateTimeOffset.UtcNow,
            DurationMs = 182450,
            ErrorMessage = null
        };
    }
}