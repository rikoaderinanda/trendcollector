using System.Text;
using System.Text.Json;
using AIContentFactory.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.AI;

/// <summary>
/// AI provider for any OpenAI-compatible chat completions API
/// (DeepSeek, OpenAI, Groq, OpenRouter, local Ollama, etc.).
/// Configured via the "ViralAnalysis" options section.
/// </summary>
public sealed class ViralAnalysisOpenAICompatibleProvider : IViralAnalysisProvider
{
    private readonly HttpClient _httpClient;
    private readonly ViralAnalysisOptions _options;
    private readonly ILogger<ViralAnalysisOpenAICompatibleProvider> _logger;

    public ViralAnalysisOpenAICompatibleProvider(
        HttpClient httpClient,
        IOptions<ViralAnalysisOptions> options,
        ILogger<ViralAnalysisOpenAICompatibleProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "OpenAICompatible";

    public string ModelName => _options.Model;

    public async Task<ViralAnalysisResponse> AnalyzeAsync(
        ViralAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning(
                "No ViralAnalysis AI API key configured. Set 'ViralAnalysis:ApiKey' in appsettings.Local.json.");
            return new ViralAnalysisResponse
            {
                Provider = ProviderName,
                Model = ModelName,
                Success = false,
                ErrorMessage = "ViralAnalysis AI API key is not configured."
            };
        }

        var prompt = BuildPrompt(request);
        var body = BuildRequestBody(prompt);

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.Endpoint.TrimEnd('/')}/v1/chat/completions");
            httpRequest.Headers.Authorization = new("Bearer", _options.ApiKey);
            httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var executionTimeMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "ViralAnalysis AI provider returned {StatusCode}: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody);

                return new ViralAnalysisResponse
                {
                    Prompt = prompt,
                    Provider = ProviderName,
                    Model = ModelName,
                    ExecutionTimeMs = executionTimeMs,
                    Success = false,
                    ErrorMessage = $"AI provider returned HTTP {(int)response.StatusCode}: {responseBody}"
                };
            }

            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;

            var content = root.GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";

            var tokensInput = TryGetInt32(root, "usage", "prompt_tokens");
            var tokensOutput = TryGetInt32(root, "usage", "completion_tokens");

            _logger.LogInformation(
                "Viral analysis AI completed in {ExecutionTimeMs} ms, {TokensInput} input tokens, {TokensOutput} output tokens.",
                executionTimeMs, tokensInput, tokensOutput);

            return new ViralAnalysisResponse
            {
                Prompt = prompt,
                RawJson = content,
                Provider = ProviderName,
                Model = ModelName,
                TokensInput = tokensInput,
                TokensOutput = tokensOutput,
                ExecutionTimeMs = executionTimeMs,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viral analysis AI request failed.");
            return new ViralAnalysisResponse
            {
                Prompt = prompt,
                Provider = ProviderName,
                Model = ModelName,
                ExecutionTimeMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string BuildRequestBody(string prompt)
    {
        var payload = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "You are a viral content opportunity analyzer. " +
                        "You analyze evidence from trending videos and " +
                        "you ALWAYS respond with valid JSON only."
                },
                new { role = "user", content = prompt }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            response_format = new { type = "json_object" }
        };

        return JsonSerializer.Serialize(payload);
    }

    private string BuildPrompt(ViralAnalysisRequest request)
    {
        var niche = string.IsNullOrWhiteSpace(request.Niche) ? "(all)" : request.Niche;
        var keyword = string.IsNullOrWhiteSpace(request.TrendKeyword) ? "(all)" : request.TrendKeyword;

        return $$"""
                 You are the Viral Analyzer for a content factory.

                 Analyze the following candidate videos, winning patterns, trend signals
                 and content gaps. Generate {{request.OpportunityCount}} ranked content opportunities.

                 Prompt version: {{_options.PromptVersion}}

                 --- ANALYSIS CONTEXT ---
                 Niche: {{niche}}
                 Trend keyword: {{keyword}}

                 --- CANDIDATE VIDEO SUMMARIES ---
                 {{request.CandidateSummaries}}

                 --- WINNING PATTERNS ---
                 {{request.WinningPatterns}}

                 --- TREND SUMMARY ---
                 {{request.TrendSummary}}

                 --- CONTENT GAPS ---
                 {{request.ContentGaps}}

                 Generate the following JSON:

                 {
                   "trendSummary": string, "2-4 sentences describing the trend",
                   "marketObservation": string, "1-2 sentences about the market",
                   "confidenceScore": number 0-100, "how strong is the overall evidence",
                   "opportunities": [
                     {
                       "topic": string,
                       "angle": string,
                       "targetAudience": string,
                       "hook": string,
                       "format": string, e.g. "Short-form video", "Tutorial", "Listicle",
                       "structure": string[], e.g. ["Hook", "Problem", "Solution"],
                       "emotion": string,
                       "psychologicalTrigger": string,
                       "whyNow": string, "why this opportunity is strong right now",
                       "contentGap": string,
                       "differentiationStrategy": string,
                       "callToAction": string,
                       "opportunityScore": number 0-100,
                       "confidenceScore": number 0-100,
                       "riskLevel": "Low" | "Medium" | "High",
                       "supportingVideoIds": number[],
                       "evidence": string[]
                     }
                   ]
                 }

                 Rules:
                 - Use probabilistic language. Never claim a video "will go viral".
                 - Every opportunity must cite supporting video ids from the candidates above.
                 - Never generate a final script. Only a strategic blueprint.
                 - Return JSON only. No markdown. No extra text.
                 """;
    }

    private static int? TryGetInt32(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.TryGetInt32(out var value) ? value : null;
    }
}