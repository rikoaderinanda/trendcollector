using System.Text.Json;
using Microsoft.Extensions.Options;
using AIContentFactory.Api.AI;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Orchestrates a trend discovery run:
/// job → prompt → AI → history → parse → upsert → complete/fail.
/// </summary>
public sealed class TrendDiscoveryService
{
    private readonly ITrendDiscoveryAIProvider _aiProvider;
    private readonly ITrendKeywordRepository _keywordRepository;
    private readonly ITrendDiscoveryJobRepository _jobRepository;
    private readonly ITrendDiscoveryPromptHistoryRepository _historyRepository;
    private readonly TrendDiscoveryOptions _options;
    private readonly ILogger<TrendDiscoveryService> _logger;

    public TrendDiscoveryService(
        ITrendDiscoveryAIProvider aiProvider,
        ITrendKeywordRepository keywordRepository,
        ITrendDiscoveryJobRepository jobRepository,
        ITrendDiscoveryPromptHistoryRepository historyRepository,
        IOptions<TrendDiscoveryOptions> options,
        ILogger<TrendDiscoveryService> logger)
    {
        _aiProvider = aiProvider;
        _keywordRepository = keywordRepository;
        _jobRepository = jobRepository;
        _historyRepository = historyRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Runs a discovery job immediately.
    /// </summary>
    public async Task<RunDiscoveryResponse> RunAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var job = new TrendDiscoveryJob
        {
            StartedAt = startedAt,
            Status = TrendDiscoveryJobStatus.Running,
            Source = DiscoverySource.AI
        };

        var jobId = await _jobRepository.CreateAsync(job, cancellationToken);
        _logger.LogInformation("Trend discovery job {JobId} started.", jobId);

        try
        {
            // 1. Build AI request from configured options
            var request = new TrendDiscoveryAIRequest
            {
                Niches = _options.Niches,
                Countries = _options.Countries,
                Languages = _options.Languages,
                MaxKeywords = _options.MaxKeywordsPerRun
            };

            // 2. Call AI provider
            var aiResponse = await _aiProvider.DiscoverTrendsAsync(request, cancellationToken);

            // 3. Save prompt history (full audit trail - never discards prompts)
            await _historyRepository.CreateAsync(new TrendDiscoveryPromptHistory
            {
                JobId = jobId,
                Prompt = aiResponse.Prompt,
                AiResponse = aiResponse.RawJson,
                Provider = aiResponse.Provider,
                Model = aiResponse.Model,
                TokensInput = aiResponse.TokensInput,
                TokensOutput = aiResponse.TokensOutput,
                ExecutionTimeMs = aiResponse.ExecutionTimeMs
            }, cancellationToken);

            if (!aiResponse.Success)
            {
                var message = aiResponse.ErrorMessage ?? "AI provider returned an unknown error.";
                _logger.LogError("Trend discovery job {JobId} failed: {Message}", jobId, message);
                await _jobRepository.FailAsync(jobId, message, cancellationToken);
                return BuildResponse(jobId, startedAt, TrendDiscoveryJobStatus.Failed, 0, message);
            }

            // 4. Parse JSON → keywords
            var discovered = ParseKeywords(aiResponse.RawJson);
            if (discovered.Count == 0)
            {
                _logger.LogWarning("Trend discovery job {JobId} received no keywords from AI.", jobId);
            }

            // 5. Upsert keywords (duplicate-safe, updates priority)
            foreach (var item in discovered)
            {
                await _keywordRepository.UpsertAsync(new TrendKeyword
                {
                    Keyword = item.Keyword,
                    Niche = item.Niche,
                    Country = item.Country,
                    Language = item.Language,
                    Priority = item.Priority,
                    DiscoveryReason = item.Reason,
                    Source = DiscoverySource.AI,
                    Status = KeywordStatus.Active
                }, cancellationToken);
            }

            // 6. Complete job
            await _jobRepository.CompleteAsync(jobId, discovered.Count, cancellationToken);
            _logger.LogInformation("Trend discovery job {JobId} completed with {Count} keywords.", jobId, discovered.Count);

            return BuildResponse(jobId, startedAt, TrendDiscoveryJobStatus.Completed, discovered.Count, null);
        }
        catch (OperationCanceledException)
        {
            await _jobRepository.FailAsync(jobId, "Operation cancelled.", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trend discovery job {JobId} failed unexpectedly.", jobId);
            await _jobRepository.FailAsync(jobId, ex.Message, CancellationToken.None);
            return BuildResponse(jobId, startedAt, TrendDiscoveryJobStatus.Failed, 0, ex.Message);
        }
    }

    private static List<AIDiscoveredKeyword> ParseKeywords(string rawJson)
    {
        var clean = ExtractJsonArray(rawJson);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return new List<AIDiscoveredKeyword>();
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var result = JsonSerializer.Deserialize<List<AIDiscoveredKeyword>>(clean, options);
        return result ?? new List<AIDiscoveredKeyword>();
    }

    /// <summary>
    /// Strips markdown code fences and any surrounding text so only the JSON array remains.
    /// Handles providers that wrap JSON in ```json ... ``` or return a single JSON object.
    /// </summary>
    private static string ExtractJsonArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();

        // Remove markdown code fences if present
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            return start >= 0 && end > start ? text[start..(end + 1)] : string.Empty;
        }

        // If it's an array directly, return it
        if (text.StartsWith('['))
        {
            var end = text.LastIndexOf(']');
            return end > 0 ? text[..(end + 1)] : text;
        }

        // Some providers wrap the array in a JSON object like { "keywords": [...] }
        var arrayStart = text.IndexOf('[');
        var arrayEnd = text.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            return text[arrayStart..(arrayEnd + 1)];
        }

        return string.Empty;
    }

    private static RunDiscoveryResponse BuildResponse(
        long jobId,
        DateTimeOffset startedAt,
        string status,
        int totalKeywords,
        string? errorMessage)
    {
        return new RunDiscoveryResponse
        {
            JobId = jobId,
            Status = status,
            TotalKeywords = totalKeywords,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            DurationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Parsed keyword item from the AI JSON response.
    /// </summary>
    private sealed class AIDiscoveredKeyword
    {
        public string Keyword { get; set; } = string.Empty;
        public string? Niche { get; set; }
        public string Country { get; set; } = "Global";
        public string Language { get; set; } = "en";
        public int Priority { get; set; } = 50;
        public string? Reason { get; set; }
    }
}