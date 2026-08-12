using AIContentFactory.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.AI;

/// <summary>
/// No-op fallback provider used when <c>ViralAnalysis:AIProvider = "NoOp"</c>.
/// Returns a minimal empty JSON response so the pipeline can still complete
/// without an external AI dependency.
/// </summary>
public sealed class ViralAnalysisNoOpProvider : IViralAnalysisProvider
{
    private readonly ViralAnalysisOptions _options;
    private readonly ILogger<ViralAnalysisNoOpProvider> _logger;

    public ViralAnalysisNoOpProvider(
        IOptions<ViralAnalysisOptions> options,
        ILogger<ViralAnalysisNoOpProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "NoOp";

    public string ModelName => _options.Model;

    public Task<ViralAnalysisResponse> AnalyzeAsync(
        ViralAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "ViralAnalysis NoOp provider used. No real analysis will be generated for run {RunId}.",
            request.AnalysisRunId);

        var rawJson = """
            {
              "trendSummary": "NoOp provider - no real analysis performed.",
              "marketObservation": "Configure ViralAnalysis:AIProvider to an OpenAI-compatible endpoint.",
              "confidenceScore": 0,
              "opportunities": []
            }
            """;

        return Task.FromResult(new ViralAnalysisResponse
        {
            Success = true,
            RawJson = rawJson,
            Provider = ProviderName,
            Model = ModelName,
            Prompt = string.Empty,
            ExecutionTimeMs = 0
        });
    }
}