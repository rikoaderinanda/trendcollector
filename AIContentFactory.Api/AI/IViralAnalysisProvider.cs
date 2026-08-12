namespace AIContentFactory.Api.AI;

/// <summary>
/// Abstraction over AI providers (OpenAI, DeepSeek, Gemini, Claude, Local LLM, ...)
/// used for the Viral Analyzer. The agent is provider-independent - implementations
/// are selected via configuration.
/// </summary>
public interface IViralAnalysisProvider
{
    /// <summary>Provider display name, e.g. "OpenAICompatible".</summary>
    string ProviderName { get; }

    /// <summary>Model name used, e.g. "deepseek-chat".</summary>
    string ModelName { get; }

    /// <summary>
    /// Sends the viral analysis prompt to the AI provider and returns the raw JSON response.
    /// </summary>
    Task<ViralAnalysisResponse> AnalyzeAsync(
        ViralAnalysisRequest request,
        CancellationToken cancellationToken = default);
}