using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;

namespace AIContentFactory.Api.AI;

/// <summary>
/// AI provider for any OpenAI-compatible chat completions API
/// (DeepSeek, OpenAI, Groq, OpenRouter, local Ollama, etc.).
/// Configured via the "TrendDiscovery" options section.
/// </summary>
public sealed class DiscoveryOpenAICompatibleProvider : ITrendDiscoveryAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly TrendDiscoveryOptions _options;
    private readonly ILogger<DiscoveryOpenAICompatibleProvider> _logger;

    public DiscoveryOpenAICompatibleProvider(
        HttpClient httpClient,
        IOptions<TrendDiscoveryOptions> options,
        ILogger<DiscoveryOpenAICompatibleProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "OpenAICompatible";

    public string ModelName => _options.Model;

    public async Task<TrendDiscoveryAIResponse> DiscoverTrendsAsync(
        TrendDiscoveryAIRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("No AI API key configured. Set 'TrendDiscovery:ApiKey' in appsettings.Local.json.");
            return new TrendDiscoveryAIResponse
            {
                Provider = ProviderName,
                Model = ModelName,
                Success = false,
                ErrorMessage = "AI API key is not configured."
            };
        }

        var prompt = BuildPrompt(request);
        var body = BuildRequestBody(prompt);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.Endpoint.TrimEnd('/')}/v1/chat/completions");
            httpRequest.Headers.Authorization = new("Bearer", _options.ApiKey);
            httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var executionTimeMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AI provider returned {StatusCode}: {ResponseBody}", (int)response.StatusCode, responseBody);
                return new TrendDiscoveryAIResponse
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

            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "[]";

            var tokensInput = TryGetInt32(root, "usage", "prompt_tokens");
            var tokensOutput = TryGetInt32(root, "usage", "completion_tokens");

            _logger.LogInformation(
                "AI discovery completed in {ExecutionTimeMs} ms, {TokensInput} input tokens, {TokensOutput} output tokens.",
                executionTimeMs, tokensInput, tokensOutput);

            return new TrendDiscoveryAIResponse
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
            _logger.LogError(ex, "AI discovery request failed.");
            return new TrendDiscoveryAIResponse
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
                new { role = "system", content = "You are a YouTube trend researcher. You always respond with valid JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            response_format = new { type = "json_object" }
        };

        return JsonSerializer.Serialize(payload);
    }

    private string BuildPrompt(TrendDiscoveryAIRequest request)
    {
        var niches = string.Join("\n- ", request.Niches);
        var countries = string.Join(", ", request.Countries);
        var languages = string.Join(", ", request.Languages);

        return $$"""
            You are a YouTube trend researcher.

            Your objective is to discover topics that are likely to become viral.

            Focus on:
            - {{niches}}

            Target countries: {{countries}}
            Target languages: {{languages}}

            Generate the best {{request.MaxKeywords}} YouTube search keywords.

            Avoid generic keywords.
            Prefer emerging topics.

            IMPORTANT:
            Focus on topics that are suitable for YouTube SHORTS -
            short-form, vertical videos under 60 seconds.
            Prefer topics that are:
            - visually engaging
            - quick to understand
            - trendy / timely
            - have a clear hook in the first 3 seconds
            - are likely to be searched or viewed in the next 24-48 hours

            Return:
            - Keyword
            - Niche
            - Country
            - Language
            - Priority (1-100)
            - Reason

            Output JSON only, as an array. Example:
            [
              {
                "keyword": "OpenAI Codex",
                "niche": "Artificial Intelligence",
                "country": "Global",
                "language": "en",
                "priority": 96,
                "reason": "Rapid growth among developers."
              }
            ]
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