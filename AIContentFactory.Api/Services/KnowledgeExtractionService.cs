using System.Text.Json;
using AIContentFactory.Api.AI;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;
using AIContentFactory.Api.Transcript;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IKnowledgeExtractionService" />
public sealed class KnowledgeExtractionService : IKnowledgeExtractionService
{
    private readonly IQueueService _queueService;
    private readonly IVideoMetadataRepository _videoMetadataRepository;
    private readonly IVideoTranscriptRepository _transcriptRepository;
    private readonly IVideoKnowledgeRepository _knowledgeRepository;
    private readonly IVideoKnowledgeRawRepository _rawRepository;
    private readonly ITranscriptProvider _transcriptProvider;
    private readonly IKnowledgeExtractionProvider _aiProvider;
    private readonly ILogger<KnowledgeExtractionService> _logger;

    public KnowledgeExtractionService(
        IQueueService queueService,
        IVideoMetadataRepository videoMetadataRepository,
        IVideoTranscriptRepository transcriptRepository,
        IVideoKnowledgeRepository knowledgeRepository,
        IVideoKnowledgeRawRepository rawRepository,
        ITranscriptProvider transcriptProvider,
        IKnowledgeExtractionProvider aiProvider,
        ILogger<KnowledgeExtractionService> logger)
    {
        _queueService = queueService;
        _videoMetadataRepository = videoMetadataRepository;
        _transcriptRepository = transcriptRepository;
        _knowledgeRepository = knowledgeRepository;
        _rawRepository = rawRepository;
        _transcriptProvider = transcriptProvider;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task ProcessQueueItemAsync(long queueId, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var queueItem = await _queueService.GetByIdAsync(queueId, cancellationToken);
        if (queueItem is null)
        {
            _logger.LogWarning("Knowledge extraction queue item {QueueId} not found.", queueId);
            return;
        }

        _logger.LogInformation("Knowledge extraction queue {QueueId} started for video {VideoId}.", queueId, queueItem.VideoId);

        // 1. Load video metadata
        var video = await _videoMetadataRepository.GetByIdAsync(queueItem.VideoId, cancellationToken);
        if (video is null)
        {
            throw new InvalidOperationException($"Video {queueItem.VideoId} not found in trending_videos.");
        }

        // 2. Persist transcript
        var transcript = await EnsureTranscriptAsync(video, cancellationToken);
        if (transcript is null)
        {
            await _queueService.MarkTranscriptUnavailableAsync(queueId, cancellationToken);
            _logger.LogWarning("Transcript unavailable for video {VideoId}. Queue {QueueId} marked TranscriptUnavailable.",
                video.Id, queueId);
            return;
        }

        // 3. Build AI request
        var statistics = await _videoMetadataRepository.GetLatestStatisticsAsync(video.Id, cancellationToken);
        var request = new KnowledgeExtractionRequest
        {
            VideoId = video.Id,
            Title = video.Title,
            Description = video.Description,
            Tags = video.Tags,
            Language = video.Language,
            Statistics = FormatStatistics(statistics),
            Transcript = transcript.Transcript
        };

        _logger.LogInformation("Prompt generated for video {VideoId} ({Title}).", video.Id, video.Title);

        // 4. Call AI provider
        var aiResponse = await _aiProvider.ExtractAsync(request, cancellationToken);

        // 5. Persist raw AI response - never discard
        await _rawRepository.InsertAsync(new VideoKnowledgeRaw
        {
            VideoId = video.Id,
            Provider = aiResponse.Provider,
            Model = aiResponse.Model,
            Prompt = aiResponse.Prompt,
            Response = aiResponse.RawJson,
            ExecutionTimeMs = aiResponse.ExecutionTimeMs,
            TokensInput = aiResponse.TokensInput,
            TokensOutput = aiResponse.TokensOutput
        }, cancellationToken);

        if (!aiResponse.Success)
        {
            throw new InvalidOperationException(
                $"AI provider failed for video {video.Id}: {aiResponse.ErrorMessage ?? "Unknown error"}");
        }

        // 6. Parse AI JSON into structured knowledge
        var knowledge = string.IsNullOrWhiteSpace(aiResponse.RawJson)
            ? null
            : ParseKnowledge(aiResponse.RawJson);

        if (knowledge is null)
        {
            throw new InvalidOperationException($"Failed to parse AI response for video {video.Id}.");
        }

        knowledge.VideoId = video.Id;

        // 7. Persist knowledge
        await _knowledgeRepository.UpsertAsync(knowledge, cancellationToken);

        // 8. Mark completed
        var durationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        await _queueService.MarkCompletedAsync(queueId, durationMs, cancellationToken);

        _logger.LogInformation(
            "Knowledge saved for video {VideoId} in {DurationMs} ms. Queue {QueueId} completed.",
            video.Id, durationMs, queueId);
    }

    // ---------- Transcript ----------

    private async Task<VideoTranscript?> EnsureTranscriptAsync(
        TrendingVideoMetadata video,
        CancellationToken cancellationToken)
    {
        // Prefer the persisted transcript when already available.
        var existing = await _transcriptRepository.GetByVideoIdAsync(video.Id, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Transcript already persisted for video {VideoId}.", video.Id);
            return existing;
        }

        _logger.LogInformation("Retrieving transcript for video {VideoId} from platform.", video.Id);

        var fetched = await _transcriptProvider.GetTranscriptAsync(
            video.PlatformVideoId,
            video.Language,
            cancellationToken);

        if (fetched is null)
        {
            return null;
        }

        fetched.VideoId = video.Id;
        await _transcriptRepository.UpsertAsync(fetched, cancellationToken);

        _logger.LogInformation("Transcript loaded and persisted for video {VideoId}.", video.Id);
        return fetched;
    }

    // ---------- Statistics formatting ----------

    private static string FormatStatistics(VideoStatisticsSnapshot? statistics)
    {
        if (statistics is null)
        {
            return "No statistics available.";
        }

        return $"views: {FormatNumber(statistics.Views)}, " +
               $"likes: {FormatNumber(statistics.Likes)}, " +
               $"comments: {FormatNumber(statistics.Comments)}, " +
               $"favorites: {FormatNumber(statistics.Favorites)}, " +
               $"engagement rate: {FormatPercent(statistics.EngagementRate)}, " +
               $"like ratio: {FormatPercent(statistics.LikeRatio)}, " +
               $"comment ratio: {FormatPercent(statistics.CommentRatio)}, " +
               $"views/day: {FormatNumber(statistics.ViewPerDay)}, " +
               $"video age: {statistics.VideoAgeDays ?? 0} days, " +
               $"captured at: {statistics.CapturedAt:O}";
    }

    private static string FormatNumber(long? value)
    {
        if (value is null)
        {
            return "unknown";
        }

        return value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000.0:0.#}B",
            >= 1_000_000 => $"{value / 1_000_000.0:0.#}M",
            >= 1_000 => $"{value / 1_000.0:0.#}K",
            _ => value.ToString() ?? "unknown"
        };
    }

    private static string FormatNumber(decimal? value)
        => value is null ? "unknown" : FormatNumber((long)value.Value);

    private static string FormatPercent(decimal? value)
        => value is null ? "unknown" : $"{value * 100m:0.##}%";

    // ---------- AI JSON parsing ----------

    private static VideoKnowledge? ParseKnowledge(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        var clean = ExtractJsonObject(rawJson);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return null;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<VideoKnowledge>(clean, options);
    }

    /// <summary>
    /// Strips markdown code fences and any surrounding text so only the JSON object remains.
    /// </summary>
    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();

        // Remove markdown code fences if present.
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            return start >= 0 && end > start ? text[start..(end + 1)] : string.Empty;
        }

        // If it's a JSON object directly, return it.
        if (text.StartsWith('{'))
        {
            var end = text.LastIndexOf('}');
            return end > 0 ? text[..(end + 1)] : text;
        }

        // Some providers wrap the object in an array or additional text.
        var objectStart = text.IndexOf('{');
        var objectEnd = text.LastIndexOf('}');
        return objectStart >= 0 && objectEnd > objectStart
            ? text[objectStart..(objectEnd + 1)]
            : string.Empty;
    }
}