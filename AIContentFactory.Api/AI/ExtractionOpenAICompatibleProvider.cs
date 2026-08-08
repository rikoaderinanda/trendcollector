using System.Text;
using System.Text.Json;
using AIContentFactory.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.AI;

/// <summary>
/// AI provider for any OpenAI-compatible chat completions API
/// (DeepSeek, OpenAI, Groq, OpenRouter, local Ollama, etc.).
/// Configured via the "KnowledgeExtraction" options section.
/// </summary>
public sealed class ExtractionOpenAICompatibleProvider : IKnowledgeExtractionProvider
{
    private readonly HttpClient _httpClient;
    private readonly KnowledgeExtractionOptions _options;
    private readonly ILogger<ExtractionOpenAICompatibleProvider> _logger;

    public ExtractionOpenAICompatibleProvider(
        HttpClient httpClient,
        IOptions<KnowledgeExtractionOptions> options,
        ILogger<ExtractionOpenAICompatibleProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "OpenAICompatible";

    public string ModelName => _options.Model;

    public async Task<KnowledgeExtractionResponse> ExtractAsync(
        KnowledgeExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning(
                "No AI API key configured. Set 'KnowledgeExtraction:ApiKey' in appsettings.Local.json.");
            return new KnowledgeExtractionResponse
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
                    "AI provider returned {StatusCode}: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody);

                return new KnowledgeExtractionResponse
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
                "AI knowledge extraction completed in {ExecutionTimeMs} ms, {TokensInput} input tokens, {TokensOutput} output tokens.",
                executionTimeMs, tokensInput, tokensOutput);

            return new KnowledgeExtractionResponse
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
            _logger.LogError(ex, "AI knowledge extraction request failed.");
            return new KnowledgeExtractionResponse
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
                        "You are a YouTube knowledge extraction engine. " +
                        "You analyze video metadata, statistics, and transcripts, " +
                        "and you ALWAYS respond with valid JSON only."
                },
                new { role = "user", content = prompt }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            response_format = new { type = "json_object" }
        };

        return JsonSerializer.Serialize(payload);
    }

    private string BuildPrompt(KnowledgeExtractionRequest request)
    {
        var tags = request.Tags is { Length: > 0 }
            ? string.Join(", ", request.Tags)
            : "(none)";

        var statistics = string.IsNullOrWhiteSpace(request.Statistics)
            ? "(none)"
            : request.Statistics;

        var transcript = string.IsNullOrWhiteSpace(request.Transcript)
            ? "(no transcript available)"
            : request.Transcript;

        return $$"""
            You are a YouTube knowledge extraction engine.

            Extract structured knowledge from the following YouTube video content.

            Prompt version: {{_options.PromptVersion}}

            --- VIDEO METADATA ---
            Title: {{request.Title ?? "(none)"}}
            Description: {{request.Description ?? "(none)"}}
            Tags: {{tags}}
            Language: {{request.Language ?? "(unknown)"}}

            --- STATISTICS ---
            {{statistics}}

            --- TRANSCRIPT ---
            {{transcript}}

            Generate the following JSON fields:

            - summary: string, 2-4 sentence summary of the video
            - mainTopic: string, the main topic of the video
            - keywords: string[] of 5-10 SEO keywords
            - hook: string, the opening hook of the video
            - tone: string, e.g. "casual", "educational", "inspiring"
            - contentStructure: string[] describing the video's section flow
            - callToAction: string, the CTA used in the video
            - importantPoints: string[] of the key takeaways
            - learningNotes: string[] of educational lessons
            - interestingFacts: string[] of notable facts
            - psychologicalTriggers: string[] e.g. "Curiosity", "FOMO"
            - storyPattern: string, e.g. "Problem → Solution"
            - contentType: string, e.g. "tutorial", "vlog", "review"
            - difficultyLevel: string, e.g. "Beginner", "Intermediate", "Advanced"
            - language: string of the video language
            - emotion: string, dominant emotion
            - curiosityScore: int 1-100
            - retentionStrategy: string, how the video keeps viewers watching
            - suggestedImprovements: string[] of improvement ideas
            - educationalValue: int 1-100
            - entertainmentValue: int 1-100
            - engagementTechniques: string[] of engagement techniques used
            - targetAudience: string, who the video is for

            Return JSON only. No markdown. No extra text.
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