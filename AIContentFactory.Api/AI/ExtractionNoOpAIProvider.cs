namespace AIContentFactory.Api.AI;

/// <summary>
/// Default provider that returns an empty result.
/// Used when no real AI provider is configured - the app still runs
/// and jobs are recorded without crash.
/// </summary>
public sealed class ExtractionNoOpAIProvider : IKnowledgeExtractionProvider
{
    private readonly ILogger<ExtractionNoOpAIProvider> _logger;

    public ExtractionNoOpAIProvider(ILogger<ExtractionNoOpAIProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "NoOp";

    public string ModelName => "none";

    public Task<KnowledgeExtractionResponse> ExtractAsync(
        KnowledgeExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "ExtractionNoOpAIProvider is active. Configure a real AI provider (e.g. DeepSeek) to run actual extraction. " +
            "Set 'KnowledgeExtraction:AIProvider' to 'OpenAICompatible' and provide an API key.");

        return Task.FromResult(new KnowledgeExtractionResponse
        {
            Prompt = "NoOp provider - no prompt generated.",
            RawJson = "{}",
            Provider = ProviderName,
            Model = ModelName,
            Success = true
        });
    }
}