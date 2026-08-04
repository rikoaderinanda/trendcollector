using System.Text.Json;
using TrendCollector.Api.Models.Dtos;
using TrendCollector.Api.Models.Entities;
using TrendCollector.Api.Repositories;

namespace TrendCollector.Api.Services;

/// <summary>
/// Orchestrates a trend collection: search YouTube, fetch full details,
/// save everything to PostgreSQL, and return an execution summary.
/// </summary>
public sealed class TrendCollectorService
{
    private readonly IYouTubeApiService _youTubeApi;
    private readonly IPlatformRepository _platformRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IVideoRepository _videoRepository;
    private readonly IJobRepository _jobRepository;
    private readonly StatisticsCalculator _statisticsCalculator;
    private readonly ILogger<TrendCollectorService> _logger;

    public TrendCollectorService(
        IYouTubeApiService youTubeApi,
        IPlatformRepository platformRepository,
        IChannelRepository channelRepository,
        IVideoRepository videoRepository,
        IJobRepository jobRepository,
        StatisticsCalculator statisticsCalculator,
        ILogger<TrendCollectorService> logger)
    {
        _youTubeApi = youTubeApi;
        _platformRepository = platformRepository;
        _channelRepository = channelRepository;
        _videoRepository = videoRepository;
        _jobRepository = jobRepository;
        _statisticsCalculator = statisticsCalculator;
        _logger = logger;
    }

    public async Task<CollectSummary> CollectAsync(CollectRequest request, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var job = new CollectionJob
        {
            StartedAt = startedAt,
            Keyword = request.Keyword,
            Country = request.Country,
            Language = request.Language,
            Status = CollectionJobStatus.Running
        };

        var jobId = await _jobRepository.CreateAsync(job, cancellationToken);

        try
        {
            var maxResults = Math.Clamp(request.MaxResults, 1, 50);
            var language = string.IsNullOrWhiteSpace(request.Language) ? "id" : request.Language;
            var country = string.IsNullOrWhiteSpace(request.Country) ? "ID" : request.Country;
            var platformId = await _platformRepository.GetOrCreateAsync("youtube", cancellationToken);

            using var searchResponse = await _youTubeApi.SearchAsync(
                request.Keyword, language, country, maxResults, cancellationToken);

            var searchItems = GetItems(searchResponse.RootElement);
            var totalCollected = searchItems.Count;

            _logger.LogInformation("Search returned {Count} videos for keyword '{Keyword}'", totalCollected, request.Keyword);

            var toProcess = new List<(string VideoId, string ChannelId)>();
            foreach (var item in searchItems)
            {
                if (!TryGetVideoId(item, out var videoId) || !TryGetChannelId(item, out var channelId))
                {
                    continue;
                }

                toProcess.Add((videoId, channelId));
            }

            var uniqueVideoIds = toProcess.Select(x => x.VideoId).Distinct().ToList();
            using var videosResponse = await _youTubeApi.GetVideosAsync(uniqueVideoIds, cancellationToken);
            var videosById = BuildVideoDictionary(videosResponse.RootElement);

            var uniqueChannelIds = toProcess.Select(x => x.ChannelId).Distinct().ToList();
            using var channelsResponse = await _youTubeApi.GetChannelsAsync(uniqueChannelIds, cancellationToken);
            var channelsById = BuildChannelDictionary(channelsResponse.RootElement);

            int saved = 0;
            int skipped = 0;

            foreach (var (videoId, channelId) in toProcess)
            {
                var alreadyExists = await _videoRepository.ExistsAsync(platformId, videoId, cancellationToken);
                if (alreadyExists)
                {
                    skipped++;
                    continue;
                }

                if (!videosById.TryGetValue(videoId, out var videoElement))
                {
                    skipped++;
                    continue;
                }

                var channel = channelsById.TryGetValue(channelId, out var channelElement)
                    ? MapChannel(platformId, channelElement)
                    : null;

                long? channelDbId = null;
                if (channel is not null)
                {
                    channelDbId = await _channelRepository.UpsertAsync(channel, cancellationToken);
                }

                var video = MapVideo(platformId, videoId, videoElement, channelDbId, language);
                var statistics = _statisticsCalculator.Calculate(
                    videoId: 0, // overridden inside the transactional insert
                    views: GetLong(videoElement, "statistics", "viewCount"),
                    likes: GetLong(videoElement, "statistics", "likeCount"),
                    comments: GetLong(videoElement, "statistics", "commentCount"),
                    favorites: GetLong(videoElement, "statistics", "favoriteCount"),
                    publishedAt: video.PublishedAt,
                    capturedAt: DateTimeOffset.UtcNow);

                await _videoRepository.InsertWithStatisticsAsync(video, statistics, cancellationToken);
                saved++;
                _logger.LogInformation("Saved video {VideoId} ('{Title}')", videoId, video.Title);
            }

            var finishedAt = DateTimeOffset.UtcNow;
            await _jobRepository.CompleteAsync(jobId, totalCollected, saved, skipped, cancellationToken);

            return new CollectSummary
            {
                JobId = jobId,
                Keyword = request.Keyword,
                Country = country,
                Language = language,
                TotalCollected = totalCollected,
                TotalSaved = saved,
                TotalSkipped = skipped,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trend collection failed for keyword '{Keyword}'", request.Keyword);
            await _jobRepository.FailAsync(jobId, ex.Message, cancellationToken);

            return new CollectSummary
            {
                JobId = jobId,
                Keyword = request.Keyword,
                Country = request.Country,
                Language = request.Language,
                TotalCollected = 0,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            };
        }
    }

    // ---------- Mapping helpers ----------

    private static Channel? MapChannel(int platformId, JsonElement item)
    {
        var snippet = GetProperty(item, "snippet");
        var statistics = GetProperty(item, "statistics");

        return new Channel
        {
            PlatformId = platformId,
            PlatformChannelId = GetString(item, "id") ?? string.Empty,
            Name = GetString(snippet, "title"),
            Country = GetString(snippet, "country"),
            SubscriberCount = GetLong(statistics, "subscriberCount"),
            VideoCount = GetInt(statistics, "videoCount"),
            TotalViews = GetLong(statistics, "viewCount"),
            PublishedAt = GetDateTimeOffset(snippet, "publishedAt"),
            CustomUrl = GetString(snippet, "customUrl"),
            RawJson = item.GetRawText()
        };
    }

    private static TrendingVideo MapVideo(
        int platformId,
        string videoId,
        JsonElement item,
        long? channelDbId,
        string language)
    {
        var snippet = GetProperty(item, "snippet");
        var contentDetails = GetProperty(item, "contentDetails");
        var thumbnails = GetProperty(snippet, "thumbnails");

        return new TrendingVideo
        {
            PlatformId = platformId,
            PlatformVideoId = videoId,
            ChannelId = channelDbId,
            Title = GetString(snippet, "title"),
            Description = GetString(snippet, "description"),
            Url = $"https://www.youtube.com/watch?v={videoId}",
            PublishedAt = GetDateTimeOffset(snippet, "publishedAt"),
            Duration = GetString(contentDetails, "duration"),
            Category = MapCategoryName(GetString(snippet, "categoryId")),
            Tags = GetStringArray(snippet, "tags"),
            Language = language,
            CaptionAvailable = ParseBool(GetString(contentDetails, "caption")),
            Definition = GetString(contentDetails, "definition"),
            Dimension = GetString(contentDetails, "dimension"),
            Projection = GetString(contentDetails, "projection"),
            ThumbnailDefaultUrl = GetThumbnailUrl(thumbnails, "default"),
            ThumbnailMediumUrl = GetThumbnailUrl(thumbnails, "medium"),
            ThumbnailHighUrl = GetThumbnailUrl(thumbnails, "high"),
            ThumbnailStandardUrl = GetThumbnailUrl(thumbnails, "standard"),
            ThumbnailMaxresUrl = GetThumbnailUrl(thumbnails, "maxres"),
            ProcessedAt = DateTimeOffset.UtcNow,
            RawJson = item.GetRawText()
        };
    }

    // ---------- JSON helpers ----------

    private static List<JsonElement> GetItems(JsonElement root)
        => root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().ToList()
            : new List<JsonElement>();

    private static bool TryGetVideoId(JsonElement item, out string videoId)
    {
        videoId = string.Empty;
        if (item.TryGetProperty("id", out var id) &&
            id.TryGetProperty("videoId", out var videoIdElement))
        {
            videoId = videoIdElement.GetString() ?? string.Empty;
        }
        return !string.IsNullOrEmpty(videoId);
    }

    private static bool TryGetChannelId(JsonElement item, out string channelId)
    {
        channelId = string.Empty;
        if (item.TryGetProperty("snippet", out var snippet) &&
            snippet.TryGetProperty("channelId", out var channelIdElement))
        {
            channelId = channelIdElement.GetString() ?? string.Empty;
        }
        return !string.IsNullOrEmpty(channelId);
    }

    private static Dictionary<string, JsonElement> BuildVideoDictionary(JsonElement root)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var item in GetItems(root))
        {
            var id = GetString(item, "id");
            if (!string.IsNullOrEmpty(id))
            {
                dict[id] = item;
            }
        }
        return dict;
    }

    private static Dictionary<string, JsonElement> BuildChannelDictionary(JsonElement root)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var item in GetItems(root))
        {
            var id = GetString(item, "id");
            if (!string.IsNullOrEmpty(id))
            {
                dict[id] = item;
            }
        }
        return dict;
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? value : default;

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        var text = value.GetString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static long? GetLong(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }
            current = next;
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt64(out var number)
            ? number
            : current.ValueKind == JsonValueKind.String && long.TryParse(current.GetString(), out var parsed)
                ? parsed
                : null;
    }

    private static int? GetInt(JsonElement element, params string[] path)
    {
        var value = GetLong(element, path);
        return value is null or > int.MaxValue ? null : (int)value.Value;
    }

    private static bool? ParseBool(string? value)
        => value is null ? null : bool.TryParse(value, out var parsed) ? parsed : null;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return value is null ? null : DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string[]? GetStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var tags = value.EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!)
            .ToArray();

        return tags.Length == 0 ? null : tags;
    }

    private static string? GetThumbnailUrl(JsonElement thumbnails, string size)
    {
        var thumbnail = GetProperty(thumbnails, size);
        return GetString(thumbnail, "url");
    }

    /// <summary>Maps the standard YouTube category ids to readable names.</summary>
    private static string? MapCategoryName(string? categoryId)
    {
        if (string.IsNullOrEmpty(categoryId))
        {
            return null;
        }

        return categoryId switch
        {
            "1" => "Film & Animation",
            "2" => "Autos & Vehicles",
            "10" => "Music",
            "15" => "Pets & Animals",
            "17" => "Sports",
            "18" => "Short Movies",
            "19" => "Travel & Events",
            "20" => "Gaming",
            "21" => "Videoblogging",
            "22" => "People & Blogs",
            "23" => "Comedy",
            "24" => "Entertainment",
            "25" => "News & Politics",
            "26" => "Howto & Style",
            "27" => "Education",
            "28" => "Science & Technology",
            "29" => "Nonprofits & Activism",
            "30" => "Movies",
            "31" => "Anime/Animation",
            "32" => "Action/Adventure",
            "33" => "Classics",
            "34" => "Comedy",
            "35" => "Documentary",
            "36" => "Drama",
            "37" => "Family",
            "38" => "Foreign",
            "39" => "Horror",
            "40" => "Sci-Fi/Fantasy",
            "41" => "Thriller",
            "42" => "Shorts",
            "43" => "Shows",
            _ => categoryId
        };
    }
}