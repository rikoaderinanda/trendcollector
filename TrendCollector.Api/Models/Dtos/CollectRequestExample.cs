using Swashbuckle.AspNetCore.Filters;

namespace TrendCollector.Api.Models.Dtos;

/// <summary>
/// Swagger example for the collection request body.
/// </summary>
public sealed class CollectRequestExample : IExamplesProvider<CollectRequest>
{
    public CollectRequest GetExamples()
    {
        return new CollectRequest
        {
            Keyword = "AI",
            Language = "id",
            Country = "ID",
            MaxResults = 20
        };
    }
}