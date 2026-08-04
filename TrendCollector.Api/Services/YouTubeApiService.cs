using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrendCollector.Api.Configuration;

namespace TrendCollector.Api.Services;

/// <inheritdoc cref="IYouTubeApiService" />
public sealed class YouTubeApiService : IYouTubeApiService
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeOptions _options;
    private readonly ILogger<YouTubeApiService> _logger;

    private const int BatchSize = 50;

    public YouTubeApiService(
        HttpClient httpClient,
        IOptions<YouTubeOptions> options,
        ILogger<YouTubeApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JsonDocument> SearchAsync(
        string keyword,
        string language,
        string country,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("part", "snippet"),
            ("type", "video"),
            ("q", keyword),
            ("relevanceLanguage", language),
            ("regionCode", country),
            ("maxResults", maxResults.ToString()),
            ("key", _options.ApiKey));

        _logger.LogInformation("Searching YouTube videos for keyword '{Keyword}' (language={Language}, country={Country}, max={Max})",
            keyword, language, country, maxResults);

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
            cancellationToken);
    }

    private async Task<JsonDocument> GetMergedBatchesAsync(
        string endpoint,
        (string Key, string Value) fixedPart,
        (string Key, string Value) apiKey,
        List<string[]> batches,
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
        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"YouTube API returned {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        return string.Join("&", parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
    }
}