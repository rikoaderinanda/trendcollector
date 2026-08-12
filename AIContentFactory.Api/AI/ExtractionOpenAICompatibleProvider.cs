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

    public async Task<TranscriptPolishResponse> PolishTranscriptAsync(
        string transcript,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning(
                "No AI API key configured. Set 'KnowledgeExtraction:ApiKey' in appsettings.Local.json.");
            return new TranscriptPolishResponse
            {
                Provider = ProviderName,
                Model = ModelName,
                Success = false,
                ErrorMessage = "AI API key is not configured."
            };
        }

        var prompt = BuildPolishPrompt(transcript, language);
        var body = BuildRequestBody(prompt, maxTokens: _options.PolishMaxTokens);

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
                    "AI polish returned {StatusCode}: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody);

                return new TranscriptPolishResponse
                {
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

            // The provider may wrap the JSON in markdown code fences (```json)
            // even though we asked for json_object. Also the polished transcript
            // may contain quotes/newlines that DeepSeek sometimes fails to
            // escape, producing invalid JSON. Handle both gracefully: on any
            // parse failure we return Success=false (the controller then falls
            // back to the deterministic dedup output instead of crashing).
            var cleanContent = ExtractEmbeddedJson(content);
            if (string.IsNullOrWhiteSpace(cleanContent))
            {
                _logger.LogWarning(
                    "AI polish response did not contain valid JSON. Response was: {Content}",
                    Shorten(content, 300));
                return new TranscriptPolishResponse
                {
                    Provider = ProviderName,
                    Model = ModelName,
                    TokensInput = tokensInput,
                    TokensOutput = tokensOutput,
                    ExecutionTimeMs = executionTimeMs,
                    Success = false,
                    ErrorMessage = "AI response did not contain valid JSON."
                };
            }

            string? polishedText = null;
            var score = 0;

            try
            {
                using var parsed = JsonDocument.Parse(cleanContent);
                var polished = parsed.RootElement;

                polishedText = polished.TryGetProperty("polishedText", out var textEl)
                    ? textEl.GetString()
                    : null;
                if (polished.TryGetProperty("score", out var scoreEl) && scoreEl.TryGetInt32(out var s))
                {
                    score = Math.Clamp(s, 0, 100);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "AI polish returned malformed JSON. Response: {Content}",
                    Shorten(content, 300));
                return new TranscriptPolishResponse
                {
                    Provider = ProviderName,
                    Model = ModelName,
                    TokensInput = tokensInput,
                    TokensOutput = tokensOutput,
                    ExecutionTimeMs = executionTimeMs,
                    Success = false,
                    ErrorMessage = "AI response returned malformed JSON."
                };
            }

            _logger.LogInformation(
                "AI transcript polish completed in {ExecutionTimeMs} ms, score {Score}.",
                executionTimeMs, score);

            return new TranscriptPolishResponse
            {
                PolishedText = polishedText,
                Score = score,
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
            _logger.LogError(ex, "AI transcript polish request failed.");
            return new TranscriptPolishResponse
            {
                Provider = ProviderName,
                Model = ModelName,
                ExecutionTimeMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string BuildPolishPrompt(string transcript, string? language)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "the original language" : language;

        return $$"""
                 You are an expert YouTube transcript editor.

                 Clean and improve the following auto-generated transcript.

                 Rules:
                 - Keep the language in: {{lang}}
                 - Remove filler words (e.g. "um", "uh", "you know", "like" used as filler)
                 - Fix obvious ASR errors where the intended word is clear from context
                 - Add natural paragraph breaks at topic changes
                 - Preserve all factual content — do NOT invent, remove, or reorder information
                 - Do NOT add commentary, timestamps, or speaker labels
                 - Remove repetition

                 TRANSCRIPT:
                 {{transcript}}

                 Return JSON only with exactly these two fields:
                 {
                   "polishedText": "the cleaned transcript",
                   "score": 0-100 integer rating the transcript quality (readability, completeness, coherence)
                 }
                 """;
    }

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

    private string BuildRequestBody(string prompt, int? maxTokens = null)
    {
        var effectiveMaxTokens = maxTokens ?? _options.MaxTokens;

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
            max_tokens = effectiveMaxTokens,
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

    /// <summary>
    /// Extracts the first JSON object/array from a raw string, stripping any
    /// surrounding markdown code fences or explanatory text. Returns empty when
    /// no JSON-like block is found.
    /// </summary>
    private static string ExtractEmbeddedJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();

        // Strip ```json ... ``` fences.
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            return start >= 0 && end > start ? text[start..(end + 1)] : string.Empty;
        }

        if (text.StartsWith('{'))
        {
            var end = text.LastIndexOf('}');
            return end > 0 ? text[..(end + 1)] : text;
        }

        var objStart = text.IndexOf('{');
        var objEnd = text.LastIndexOf('}');
        return objStart >= 0 && objEnd > objStart
            ? text[objStart..(objEnd + 1)]
            : string.Empty;
    }

    /// <summary>Truncates a long string for log safety.</summary>
    private static string Shorten(string text, int maxLength)
    {
        var cleaned = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength] + "...";
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