using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Exceptions;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IYouTubeApiService" />
public sealed class YouTubeApiService : IYouTubeApiService
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeOptions _options;
    private readonly IQuotaTracker _quotaTracker;
    private readonly ILogger<YouTubeApiService> _logger;
    private readonly TrendCollectorOptions _collectorOptions;

    private const int BatchSize = 50;

    private readonly RetryCalculator _retryCalculator;

    public YouTubeApiService(
        HttpClient httpClient,
        IOptions<YouTubeOptions> options,
        IOptions<TrendCollectorOptions> collectorOptions,
        IQuotaTracker quotaTracker,
        RetryCalculator retryCalculator,
        ILogger<YouTubeApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _collectorOptions = collectorOptions.Value;
        _quotaTracker = quotaTracker;
        _retryCalculator = retryCalculator;
        _logger = logger;
    }

    public async Task<JsonDocument> SearchAsync(
        string keyword,
        string language,
        string country,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        // Increment the search.list daily counter BEFORE the actual call.
        // This counter is the source of truth for switching to Tracking Mode.
        await _quotaTracker.IncrementSearchCallCountAsync(cancellationToken);

        var queryParams = new List<(string Key, string? Value)>
        {
            ("part", "snippet"),
            ("type", "video"),
            ("q", keyword),
            ("relevanceLanguage", language),
            ("regionCode", country),
            ("maxResults", maxResults.ToString()),
            ("key", _options.ApiKey)
        };

        // Recent-shorts discovery: order by date to bias toward fresh videos.
        if (!string.IsNullOrWhiteSpace(_collectorOptions.SearchOrder))
        {
            queryParams.Add(("order", _collectorOptions.SearchOrder));
        }

        // Biase toward short-form videos (YouTube's "short" = ≤4 min).
        if (!string.IsNullOrWhiteSpace(_collectorOptions.SearchVideoDuration))
        {
            queryParams.Add(("videoDuration", _collectorOptions.SearchVideoDuration));
        }

        // Only include videos published within the configured freshness window.
        if (_collectorOptions.SearchWindowDays > 0)
        {
            var publishedAfter = DateTime.UtcNow
                .AddDays(-_collectorOptions.SearchWindowDays)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");
            queryParams.Add(("publishedAfter", publishedAfter));
        }

        var query = BuildQuery(queryParams.ToArray());

        _logger.LogInformation(
            "Trend search: keyword={Keyword}, order={Order}, videoDuration={Duration}, publishedAfter={PublishedAfter}, maxResults={Max}",
            keyword,
            _collectorOptions.SearchOrder,
            _collectorOptions.SearchVideoDuration,
            _collectorOptions.SearchWindowDays > 0
                ? DateTime.UtcNow.AddDays(-_collectorOptions.SearchWindowDays).ToString("yyyy-MM-ddTHH:mm:ssZ")
                : "(none)",
            maxResults);

        return await GetAsync(_options.SearchEndpoint, query, cancellationToken);
    }

    public async Task<JsonDocument> GetVideosAsync(
        IEnumerable<string> videoIds,
        CancellationToken cancellationToken = default)
    {
        var ids = videoIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return JsonDocument.Parse("{\"items\":[]}");
        }

        var batches = ids.Chunk(BatchSize).ToList();
        _logger.LogInformation("Fetching details for {Count} videos ({Batches} batches)", ids.Count, batches.Count);

        if (batches.Count == 1)
        {
            // Track videos.list usage so the background service can honour
            // the overall daily quota too.
            await _quotaTracker.IncrementVideosCallCountAsync(cancellationToken);

            var query = BuildQuery(
                ("part", "snippet,contentDetails,statistics,status,topicDetails,recordingDetails,localizations"),
                ("id", string.Join(",", batches[0])),
                ("key", _options.ApiKey));

            return await GetAsync(_options.VideosEndpoint, query, cancellationToken);
        }

        return await GetMergedBatchesAsync(
            _options.VideosEndpoint,
            ("part", "snippet,contentDetails,statistics,status,topicDetails,recordingDetails,localizations"),
            ("key", _options.ApiKey),
            batches,
            trackQuota: true,
            cancellationToken);
    }

    public async Task<JsonDocument> GetChannelsAsync(
        IEnumerable<string> channelIds,
        CancellationToken cancellationToken = default)
    {
        var ids = channelIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return JsonDocument.Parse("{\"items\":[]}");
        }

        var batches = ids.Chunk(BatchSize).ToList();
        _logger.LogInformation("Fetching details for {Count} channels ({Batches} batches)", ids.Count, batches.Count);

        if (batches.Count == 1)
        {
            var query = BuildQuery(
                ("part", "snippet,statistics,contentDetails,topicDetails"),
                ("id", string.Join(",", batches[0])),
                ("key", _options.ApiKey));

            return await GetAsync(_options.ChannelsEndpoint, query, cancellationToken);
        }

        return await GetMergedBatchesAsync(
            _options.ChannelsEndpoint,
            ("part", "snippet,statistics,contentDetails,topicDetails"),
            ("key", _options.ApiKey),
            batches,
            trackQuota: false,
            cancellationToken);
    }

    private async Task<JsonDocument> GetMergedBatchesAsync(
        string endpoint,
        (string Key, string Value) fixedPart,
        (string Key, string Value) apiKey,
        List<string[]> batches,
        bool trackQuota,
        CancellationToken cancellationToken)
    {
        var results = new List<JsonDocument>(batches.Count);
        try
        {
            foreach (var batch in batches)
            {
                var query = BuildQuery(
                    fixedPart,
                    ("id", string.Join(",", batch)),
                    apiKey);

                // Each batch is an independent HTTP request and consumes quota.
                if (trackQuota)
                {
                    await _quotaTracker.IncrementVideosCallCountAsync(cancellationToken);
                }

                results.Add(await GetAsync(endpoint, query, cancellationToken));
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteStartArray("items");
                foreach (var doc in results)
                {
                    if (doc.RootElement.TryGetProperty("items", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            item.WriteTo(writer);
                        }
                    }
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            stream.Position = 0;
            return JsonDocument.Parse(stream);
        }
        finally
        {
            foreach (var doc in results)
            {
                doc.Dispose();
            }
        }
    }

    private async Task<JsonDocument> GetAsync(string endpoint, string query, CancellationToken cancellationToken)
    {
        var url = $"{endpoint}?{query}";
        Exception? lastException = null;

        for (var attempt = 0; _retryCalculator.ShouldRetry(attempt - 1) || attempt == 0; attempt++)
        {
            if (attempt > 0)
            {
                var delay = _retryCalculator.Calculate(attempt - 1);
                _logger.LogWarning(
                    "Retrying YouTube API call after {Seconds:0}s (attempt {Attempt}). Endpoint: {Endpoint}",
                    delay.TotalSeconds, attempt, endpoint);
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    var error = MapHttpError(response.StatusCode, endpoint, body);

                    // Only retry transient errors.
                    if (error is YouTubeTransientException && _retryCalculator.ShouldRetry(attempt))
                    {
                        lastException = error;
                        continue;
                    }

                    throw error;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                lastException = new YouTubeTransientException($"Network error calling {endpoint}: {ex.Message}", ex);

                if (_retryCalculator.ShouldRetry(attempt))
                {
                    continue;
                }

                throw lastException;
            }
        }

        throw lastException ?? new YouTubeTransientException($"YouTube API call failed: {endpoint}");
    }

    /// <summary>
    /// Maps a YouTube API HTTP error to a typed, actionable exception so the
    /// caller can decide whether to retry, switch mode, or fail permanently.
    /// </summary>
    private static Exception MapHttpError(System.Net.HttpStatusCode statusCode, string endpoint, string body)
    {
        var message = $"YouTube API returned {(int)statusCode} for {endpoint}: {body}";

        // Quota exceeded (429 happens when total quota units are consumed,
        // 403 with "dailyLimitExceeded" for quota per method).
        if (statusCode == System.Net.HttpStatusCode.TooManyRequests ||
            body.Contains("dailyLimitExceeded", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("quota", StringComparison.OrdinalIgnoreCase) && statusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return new YouTubeQuotaExceededException(message);
        }

        // Invalid or forbidden API key.
        if (statusCode == System.Net.HttpStatusCode.Unauthorized ||
            statusCode == System.Net.HttpStatusCode.Forbidden ||
            body.Contains("keyInvalid", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("apiKeyNotValid", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("badRequest", StringComparison.OrdinalIgnoreCase))
        {
            return new YouTubeApiKeyInvalidException(message);
        }

        // Everything else (5xx, network blips…) is potentially transient.
        return new YouTubeTransientException(message);
    }

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        return string.Join("&", parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
    }
}