namespace AIContentFactory.Api.AI;

/// <summary>
/// Default provider that returns an empty result.
/// Used when no real AI provider is configured - the app still runs
/// and jobs are recorded without crash.
/// </summary>
public sealed class DiscoveryNoOpAIProvider : ITrendDiscoveryAIProvider
{
    private readonly ILogger<DiscoveryNoOpAIProvider> _logger;

    public DiscoveryNoOpAIProvider(ILogger<DiscoveryNoOpAIProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "NoOp";

    public string ModelName => "none";

    public Task<TrendDiscoveryAIResponse> DiscoverTrendsAsync(
        TrendDiscoveryAIRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "DiscoveryNoOpAIProvider is active. Configure a real AI provider (e.g. DeepSeek) to run actual discovery. " +
            "Set 'TrendDiscovery:AIProvider' to 'OpenAICompatible' and provide an API key.");

        return Task.FromResult(new TrendDiscoveryAIResponse
        {
            Prompt = "NoOp provider - no prompt generated.",
            RawJson = "[]",
            Provider = ProviderName,
            Model = ModelName,
            Success = true
        });
    }
}