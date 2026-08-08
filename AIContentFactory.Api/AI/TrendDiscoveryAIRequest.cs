namespace AIContentFactory.Api.AI;

/// <summary>
/// Input for a trend discovery AI call.
/// </summary>
public sealed class TrendDiscoveryAIRequest
{
    /// <summary>Niches/topics the AI should focus on.</summary>
    public IReadOnlyList<string> Niches { get; init; } = Array.Empty<string>();

    /// <summary>Target countries (e.g. "Global", "US", "ID").</summary>
    public IReadOnlyList<string> Countries { get; init; } = Array.Empty<string>();

    /// <summary>Target languages (e.g. "en", "id").</summary>
    public IReadOnlyList<string> Languages { get; init; } = Array.Empty<string>();

    /// <summary>Maximum number of keywords to return.</summary>
    public int MaxKeywords { get; init; } = 20;
}