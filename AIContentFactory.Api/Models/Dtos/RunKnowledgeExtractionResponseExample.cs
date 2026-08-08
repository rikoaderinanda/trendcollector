using Swashbuckle.AspNetCore.Filters;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Swagger example for a manual knowledge extraction run/retry response.
/// </summary>
public sealed class RunKnowledgeExtractionResponseExample : IExamplesProvider<RunKnowledgeExtractionResponse>
{
    public RunKnowledgeExtractionResponse GetExamples()
    {
        return new RunKnowledgeExtractionResponse
        {
            QueueId = 17,
            VideoId = 42,
            Status = "Completed",
            RetryCount = 0,
            ErrorMessage = null,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            FinishedAt = DateTimeOffset.UtcNow
        };
    }
}