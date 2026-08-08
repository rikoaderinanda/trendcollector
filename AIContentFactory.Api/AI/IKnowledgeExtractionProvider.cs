namespace AIContentFactory.Api.AI;

/// <summary>
/// Abstraction over AI providers (OpenAI, DeepSeek, Gemini, Claude, Local LLM, ...).
/// The agent is provider-independent - implementations are selected via configuration.
/// </summary>
public interface IKnowledgeExtractionProvider
{
    /// <summary>Provider display name, e.g. "DeepSeek".</summary>
    string ProviderName { get; }

    /// <summary>Model name used, e.g. "deepseek-chat".</summary>
    string ModelName { get; }

    /// <summary>
    /// Sends the knowledge extraction prompt to the AI provider and returns the raw JSON response.
    /// </summary>
    Task<KnowledgeExtractionResponse> ExtractAsync(
        KnowledgeExtractionRequest request,
        CancellationToken cancellationToken = default);
}