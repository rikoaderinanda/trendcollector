using Swashbuckle.AspNetCore.Filters;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Swagger example for a knowledge extraction queue item.
/// </summary>
public sealed class KnowledgeExtractionJobDtoExample : IExamplesProvider<KnowledgeExtractionJobDto>
{
    public KnowledgeExtractionJobDto GetExamples()
    {
        return new KnowledgeExtractionJobDto
        {
            Id = 17,
            VideoId = 42,
            Status = "Pending",
            Priority = 5,
            RetryCount = 0,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            FinishedAt = null,
            DurationMs = null,
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}