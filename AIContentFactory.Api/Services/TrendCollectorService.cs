using System.Text.Json;
using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Exceptions;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Orchestrates a trend collection: search YouTube, fetch full details,
/// save everything to PostgreSQL, and return an execution summary.
/// When the daily search.list quota is exhausted the collector switches
/// to Tracking Mode and only refreshes statistics of existing videos.
/// </summary>
public sealed class TrendCollectorService
{
    private readonly IYouTubeApiService _youTubeApi;
    private readonly IPlatformRepository _platformRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IVideoRepository _videoRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IQuotaTracker _quotaTracker;
    private readonly TrackingModeOptions _trackingOptions;
    private readonly StatisticsCalculator _statisticsCalculator;
    private readonly IQueueService _queueService;
    private readonly Agent1VideoValidator _videoValidator;
    private readonly IDataProcessingFailureRepository _failureRepo;
    private readonly KnowledgeExtractionOptions _knowledgeExtractionOptions;
    private readonly ILogger<TrendCollectorService> _logger;

    public TrendCollectorService(
        IYouTubeApiService youTubeApi,
        IPlatformRepository platformRepository,
        IChannelRepository channelRepository,
        IVideoRepository videoRepository,
        IJobRepository jobRepository,
        IQuotaTracker quotaTracker,
        IOptions<TrackingModeOptions> trackingOptions,
        StatisticsCalculator statisticsCalculator,
        IQueueService queueService,
        Agent1VideoValidator videoValidator,
        IDataProcessingFailureRepository failureRepo,
        IOptions<KnowledgeExtractionOptions> knowledgeExtractionOptions,
        ILogger<TrendCollectorService> logger)
    {
        _youTubeApi = youTubeApi;
        _platformRepository = platformRepository;
        _channelRepository = channelRepository;
        _videoRepository = videoRepository;
        _jobRepository = jobRepository;
        _quotaTracker = quotaTracker;
        _trackingOptions = trackingOptions.Value;
        _statisticsCalculator = statisticsCalculator;
        _queueService = queueService;
        _videoValidator = videoValidator;
        _failureRepo = failureRepo;
        _knowledgeExtractionOptions = knowledgeExtractionOptions.Value;
        _logger = logger;
    }

    public async Task<CollectSummary> CollectAsync(CollectRequest request, CancellationToken cancellationToken = default)
    {
        // Quota guard: once search.list has been called the configured number of
        // times today, no more search is allowed - run Tracking Mode instead.
        if (await _quotaTracker.IsSearchQuotaExhaustedAsync(cancellationToken))
        {
            _logger.LogInformation(
                "Daily search.list quota exhausted. Switching to Tracking Mode (videos.list refresh only).");
            return await TrackExistingAsync(cancellationToken);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var job = new CollectionJob
        {
            StartedAt = startedAt,
            Keyword = request.Keyword,
            Mode = CollectorMode.Discovery.ToString(),
            Country = request.Country,
            Language = request.Language,
            Status = CollectionJobStatus.Running
        };

        var jobId = await _jobRepository.CreateAsync(job, cancellationToken);
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = $"collector:{jobId}",
            ["Keyword"] = request.Keyword
        });

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
            int validationFailures = 0;

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

                // Validate the mapped video before persisting.
                var validation = _videoValidator.Validate(video);
                if (validation.IsInvalid)
                {
                    validationFailures++;
                    _logger.LogWarning("Skipping invalid video {VideoId}: {Reason}", videoId,
                        string.Join("; ", validation.Reasons));
                    await _failureRepo.RecordAsync(new DataProcessingFailure
                    {
                        AgentName = "TrendCollector",
                        EntityType = "TrendingVideo",
                        EntityId = 0,
                        Operation = "collect",
                        Status = "Invalid",
                        FailureType = "Permanent",
                        FailureReason = $"Validation failed: {string.Join("; ", validation.Reasons)}",
                        FirstAttemptAt = DateTimeOffset.UtcNow,
                        LastAttemptAt = DateTimeOffset.UtcNow,
                        RawReference = $"platform_video_id={videoId}"
                    }, cancellationToken);
                    continue;
                }

                var statistics = _statisticsCalculator.Calculate(
                    videoId: 0, // overridden inside the transactional insert
                    views: GetLong(videoElement, "statistics", "viewCount"),
                    likes: GetLong(videoElement, "statistics", "likeCount"),
                    comments: GetLong(videoElement, "statistics", "commentCount"),
                    favorites: GetLong(videoElement, "statistics", "favoriteCount"),
                    publishedAt: video.PublishedAt,
                    capturedAt: DateTimeOffset.UtcNow);

                long dbVideoId;
                try
                {
                    dbVideoId = await _videoRepository.InsertWithStatisticsAsync(video, statistics, cancellationToken);
                }
                catch (Exception dbEx)
                {
                    // A database failure for one video must not fail the entire
                    // collection run. Record it as retryable for the recovery worker.
                    _logger.LogWarning("Database insert failed for video {VideoId}: {Message}", videoId, dbEx.Message);
                    await _failureRepo.RecordAsync(new DataProcessingFailure
                    {
                        AgentName = "TrendCollector",
                        EntityType = "TrendingVideo",
                        EntityId = 0,
                        Operation = "collect-insert",
                        Status = "Retryable",
                        FailureType = "Transient",
                        FailureReason = dbEx.Message,
                        ExceptionType = dbEx.GetType().FullName,
                        RetryCount = 0,
                        MaxRetryAttempts = 5,
                        FirstAttemptAt = DateTimeOffset.UtcNow,
                        LastAttemptAt = DateTimeOffset.UtcNow,
                        RawReference = $"platform_video_id={videoId}"
                    }, cancellationToken);
                    continue;
                }

                saved++;
                _logger.LogInformation("Saved video {VideoId} ('{Title}') as db id {DbVideoId}", videoId, video.Title,
                    dbVideoId);

                // Integration: automatically enqueue high-quality candidates for
                // knowledge extraction. The early gate filters OUT:
                //  1. Non-Shorts videos (duration > 60s) when ShortsOnly=true
                //  2. Videos older than MaximumVideoAgeDays
                //  3. Videos below MinimumViewsForEnqueue
                // This preserves raw data (video still saved) but avoids wasting
                // Agent 2 AI tokens on videos unlikely to be the viral candidate.
                if (_knowledgeExtractionOptions.AutoEnqueueEnabled)
                {
                    var gateResult = ShouldEnqueue(video, statistics);
                    if (gateResult.IsAccepted)
                    {
                        await EnqueueForKnowledgeExtractionAsync(dbVideoId, cancellationToken);
                    }
                    else
                    {
                        await _failureRepo.RecordAsync(new DataProcessingFailure
                        {
                            AgentName = "TrendCollector",
                            EntityType = "TrendingVideo",
                            EntityId = dbVideoId,
                            Operation = "enqueue-gate",
                            Status = "Skipped",
                            FailureType = "Permanent",
                            FailureReason = gateResult.Reason,
                            FirstAttemptAt = DateTimeOffset.UtcNow,
                            LastAttemptAt = DateTimeOffset.UtcNow,
                            RawReference = $"platform_video_id={videoId}"
                        }, cancellationToken);
                    }
                }
            }

            var finishedAt = DateTimeOffset.UtcNow;
            await _jobRepository.CompleteAsync(jobId, totalCollected, saved, skipped, cancellationToken);

            var searchCallsToday = await _quotaTracker.GetSearchCallCountTodayAsync(cancellationToken);

            return new CollectSummary
            {
                JobId = jobId,
                Keyword = request.Keyword,
                Country = country,
                Language = language,
                Mode = CollectorMode.Discovery,
                TotalCollected = totalCollected,
                TotalSaved = saved,
                TotalSkipped = skipped,
                SearchCallsRemaining = Math.Max(0, _trackingOptions.MaxSearchCallsPerDay - searchCallsToday),
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trend collection failed for keyword '{Keyword}'", request.Keyword);
            await _jobRepository.FailAsync(jobId, ex.Message, cancellationToken);

            if (ex is YouTubeQuotaExceededException)
            {
                _logger.LogWarning(
                    "YouTube search quota exceeded while collecting '{Keyword}'. Remaining keywords will run in Tracking Mode.",
                    request.Keyword);
            }

            return new CollectSummary
            {
                JobId = jobId,
                Keyword = request.Keyword,
                Country = request.Country,
                Language = request.Language,
                Mode = CollectorMode.Discovery,
                TotalCollected = 0,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            };
        }
    }

    /// <summary>
    /// Tracking Mode: refreshes statistics of the videos collected in the last
    /// <see cref="TrackingModeOptions.LookbackDaysForTracking"/> days using
    /// videos.list only (no search.list). Inserts a new snapshot for every
    /// video found and computes velocity metrics against the previous snapshot.
    /// </summary>
    public async Task<CollectSummary> TrackExistingAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var job = new CollectionJob
        {
            StartedAt = startedAt,
            Keyword = "[tracking-mode]",
            Mode = CollectorMode.Tracking.ToString(),
            Status = CollectionJobStatus.Running
        };

        var jobId = await _jobRepository.CreateAsync(job, cancellationToken);
        using var trackingScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = $"tracking:{jobId}"
        });

        try
        {
            var recentVideos = (await _videoRepository.ListRecentAsync(
                Math.Max(1, _trackingOptions.LookbackDaysForTracking), cancellationToken)).ToList();

            _logger.LogInformation("Tracking Mode: found {Count} videos collected in the last {Days} day(s).",
                recentVideos.Count, _trackingOptions.LookbackDaysForTracking);

            if (recentVideos.Count == 0)
            {
                var emptyFinishedAt = DateTimeOffset.UtcNow;
                await _jobRepository.CompleteAsync(jobId, 0, 0, 0, cancellationToken);

                return new CollectSummary
                {
                    JobId = jobId,
                    Keyword = "[tracking-mode]",
                    Mode = CollectorMode.Tracking,
                    TotalCollected = 0,
                    TotalTracked = 0,
                    StartedAt = startedAt,
                    FinishedAt = emptyFinishedAt,
                    DurationMs = (long)(emptyFinishedAt - startedAt).TotalMilliseconds
                };
            }

            int tracked = 0;

            foreach (var batch in recentVideos.Chunk(_trackingOptions.VideoBatchSize))
            {
                using var videosResponse = await _youTubeApi.GetVideosAsync(
                    batch.Select(v => v.PlatformVideoId), cancellationToken);

                var videosById = BuildVideoDictionary(videosResponse.RootElement);

                foreach (var video in batch)
                {
                    if (!videosById.TryGetValue(video.PlatformVideoId, out var videoElement))
                    {
                        continue;
                    }

                    var previous = await _videoRepository.GetLatestStatisticsAsync(video.Id, cancellationToken);
                    var capturedAt = DateTimeOffset.UtcNow;

                    var current = _statisticsCalculator.Calculate(
                        videoId: video.Id,
                        views: GetLong(videoElement, "statistics", "viewCount"),
                        likes: GetLong(videoElement, "statistics", "likeCount"),
                        comments: GetLong(videoElement, "statistics", "commentCount"),
                        favorites: GetLong(videoElement, "statistics", "favoriteCount"),
                        publishedAt: video.PublishedAt,
                        capturedAt: capturedAt);

                    var velocity = _statisticsCalculator.CalculateVelocity(current, previous);

                    current.ViewsPerHour = velocity.ViewsPerHour;
                    current.LikeVelocity = velocity.LikeVelocity;
                    current.CommentVelocity = velocity.CommentVelocity;
                    current.GrowthScore = velocity.GrowthScore;
                    current.PreviousSnapshotId = previous?.Id;

                    await _videoRepository.InsertStatisticsAsync(current, cancellationToken);
                    tracked++;

                    _logger.LogInformation(
                        "Tracked video {VideoId} ('{Title}') - views={Views}, views/h={ViewsPerHour}, growth={GrowthScore}",
                        video.PlatformVideoId, video.Title, current.Views, velocity.ViewsPerHour, velocity.GrowthScore);
                }
            }

            var finishedAt = DateTimeOffset.UtcNow;
            await _jobRepository.CompleteAsync(jobId, recentVideos.Count, 0, recentVideos.Count - tracked,
                cancellationToken);

            return new CollectSummary
            {
                JobId = jobId,
                Keyword = "[tracking-mode]",
                Mode = CollectorMode.Tracking,
                TotalCollected = recentVideos.Count,
                TotalTracked = tracked,
                TotalSkipped = recentVideos.Count - tracked,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tracking Mode failed.");
            await _jobRepository.FailAsync(jobId, ex.Message, cancellationToken);

            return new CollectSummary
            {
                JobId = jobId,
                Keyword = "[tracking-mode]",
                Mode = CollectorMode.Tracking,
                TotalCollected = 0,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            };
        }
    }

    /// <summary>
    /// Result of the early candidate quality gate. Videos that pass are sent
    /// to Agent 2; those that fail are still stored in the DB (raw data
    /// preserved) but recorded as a DataProcessingFailure (Status=Skipped)
    /// with the rejection reason for observability/reporting.
    /// </summary>
    private sealed record EnqueueGateResult(bool IsAccepted, string Reason)
    {
        public static EnqueueGateResult Accepted { get; } = new(true, string.Empty);
    }

    /// <summary>
    /// Applies the early candidate quality gate: Shorts-only, freshness,
    /// and minimum views threshold. Videos failing the gate are still stored
    /// in the DB (raw data preserved) but are not sent to Agent 2.
    /// </summary>
    private EnqueueGateResult ShouldEnqueue(TrendingVideo video, VideoStatistics statistics)
    {
        // 1. Shorts-only filter (duration ≤ 60s).
        if (_trackingOptions.Enabled && _shortsOnlyEnabled)
        {
            if (video.Duration is not null && !IsShortDuration(video.Duration))
            {
                var reason = $"Not Shorts (duration {video.Duration}).";
                _logger.LogDebug("Skipping enqueue for video {VideoId}: {Reason}",
                    video.PlatformVideoId, reason);
                return new EnqueueGateResult(false, reason);
            }
        }

        // 2. Freshness filter.
        if (video.PublishedAt.HasValue)
        {
            var ageDays = (DateTimeOffset.UtcNow - video.PublishedAt.Value).TotalDays;
            if (ageDays > DefaultMaxAgeDays)
            {
                var reason = $"Too old ({ageDays:0.#} days > {DefaultMaxAgeDays} days).";
                _logger.LogDebug("Skipping enqueue for video {VideoId}: {Reason}",
                    video.PlatformVideoId, reason);
                return new EnqueueGateResult(false, reason);
            }
        }

        // 3. Minimum views threshold.
        if ((statistics.Views ?? 0) < MinViewsForEnqueue)
        {
            var reason = $"Views {statistics.Views ?? 0} below min {MinViewsForEnqueue}.";
            _logger.LogDebug("Skipping enqueue for video {VideoId}: {Reason}",
                video.PlatformVideoId, reason);
            return new EnqueueGateResult(false, reason);
        }

        return EnqueueGateResult.Accepted;
    }

    private const int DefaultMaxAgeDays = 7;
    private const long MinViewsForEnqueue = 10_000;
    private readonly bool _shortsOnlyEnabled = true;

    /// <summary>Parses ISO 8601 duration (e.g. "PT1M30S" or "PT45S") and checks if ≤ 60 seconds.</summary>
    internal static bool IsShortDuration(string? duration)
    {
        if (string.IsNullOrEmpty(duration))
        {
            return true; // Unknown duration — be permissive.
        }

        try
        {
            // ISO 8601: PT#H#M#S
            int seconds = 0, minutes = 0, hours = 0;
            var num = string.Empty;
            foreach (var c in duration)
            {
                if (char.IsDigit(c))
                {
                    num += c;
                }
                else if (c == 'H')
                {
                    hours = int.TryParse(num, out var h) ? h : 0;
                    num = string.Empty;
                }
                else if (c == 'M')
                {
                    minutes = int.TryParse(num, out var m) ? m : 0;
                    num = string.Empty;
                }
                else if (c == 'S')
                {
                    seconds = int.TryParse(num, out var s) ? s : 0;
                    num = string.Empty;
                }
            }

            return TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds).TotalSeconds <= 60;
        }
        catch
        {
            return true;
        }
    }

    private async Task EnqueueForKnowledgeExtractionAsync(long dbVideoId, CancellationToken cancellationToken)
    {
        try
        {
            var queueItem = await _queueService.EnqueueAsync(dbVideoId, priority: 0, cancellationToken: cancellationToken);
            _logger.LogInformation(
                "Video {DbVideoId} enqueued for knowledge extraction (queue {QueueId}).",
                dbVideoId, queueItem.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while enqueuing video {DbVideoId} for knowledge extraction.",
                dbVideoId);
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

    private static TrendingVideo MapVideo(int platformId, string videoId, JsonElement item, long? channelDbId,
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
        if (item.TryGetProperty("id", out var id) && id.TryGetProperty("videoId", out var videoIdElement))
            videoId = videoIdElement.GetString() ?? string.Empty;
        return !string.IsNullOrEmpty(videoId);
    }

    private static bool TryGetChannelId(JsonElement item, out string channelId)
    {
        channelId = string.Empty;
        if (item.TryGetProperty("snippet", out var snippet) &&
            snippet.TryGetProperty("channelId", out var channelIdElement))
            channelId = channelIdElement.GetString() ?? string.Empty;
        return !string.IsNullOrEmpty(channelId);
    }

    private static Dictionary<string, JsonElement> BuildVideoDictionary(JsonElement root)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var item in GetItems(root))
        {
            var id = GetString(item, "id");
            if (!string.IsNullOrEmpty(id)) dict[id] = item;
        }
        return dict;
    }

    private static Dictionary<string, JsonElement> BuildChannelDictionary(JsonElement root)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var item in GetItems(root))
        {
            var id = GetString(item, "id");
            if (!string.IsNullOrEmpty(id)) dict[id] = item;
        }
        return dict;
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? value : default;

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value)) return null;
        var text = value.GetString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static long? GetLong(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt64(out var number) ? number
            : current.ValueKind == JsonValueKind.String && long.TryParse(current.GetString(), out var parsed) ? parsed
            : null;
    }

    private static int? GetInt(JsonElement element, params string[] path)
    {
        var v = GetLong(element, path);
        return v is null or > int.MaxValue ? null : (int)v.Value;
    }

    private static bool? ParseBool(string? value) => value is null ? null : bool.TryParse(value, out var p) ? p : null;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var v = GetString(element, propertyName);
        return v is null ? null : DateTimeOffset.TryParse(v, out var p) ? p : null;
    }

    private static string[]? GetStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Array) return null;
        var tags = value.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!).ToArray();
        return tags.Length == 0 ? null : tags;
    }

    private static string? GetThumbnailUrl(JsonElement thumbnails, string size) =>
        GetString(GetProperty(thumbnails, size), "url");

    private static string? MapCategoryName(string? categoryId) => categoryId switch
    {
        "1" => "Film & Animation", "2" => "Autos & Vehicles", "10" => "Music", "15" => "Pets & Animals",
        "17" => "Sports", "20" => "Gaming", "22" => "People & Blogs", "23" => "Comedy",
        "24" => "Entertainment", "25" => "News & Politics", "26" => "Howto & Style", "27" => "Education",
        "28" => "Science & Technology", _ => categoryId
    };
}