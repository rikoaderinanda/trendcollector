using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Services;
using AIContentFactory.Api.Transcript;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.Workers;

/// <summary>
/// Background service that polls the knowledge extraction queue and
/// processes pending jobs every configurable interval.
/// Applies retry with exponential backoff on failure.
/// </summary>
public sealed class KnowledgeExtractionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KnowledgeExtractionOptions _options;
    private readonly ILogger<KnowledgeExtractionBackgroundService> _logger;

    /// <summary>
    /// Timestamp of the last observed YouTube rate-limit failure (HTTP 429).
    /// Used as a simple circuit breaker: when a rate limit is hit, the worker
    /// stops processing further jobs in the batch for
    /// <see cref="KnowledgeExtractionOptions.RateLimitCooldownSeconds"/> to let
    /// YouTube's rate limiter cool down instead of walking every remaining job
    /// into the same 429 wall.
    /// </summary>
    private DateTimeOffset? _lastRateLimitAt;

    public KnowledgeExtractionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<KnowledgeExtractionOptions> options,
        ILogger<KnowledgeExtractionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Knowledge extraction is disabled. The worker will not start.");
            return;
        }

        _logger.LogInformation(
            "KnowledgeExtractionBackgroundService started. Polling every {Interval}s for up to {BatchSize} pending jobs.",
            _options.WorkerIntervalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Knowledge extraction background cycle failed.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.WorkerIntervalSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("KnowledgeExtractionBackgroundService stopped.");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
        var extractionService = scope.ServiceProvider.GetRequiredService<IKnowledgeExtractionService>();

        var pending = await queueService.GetPendingAsync(_options.BatchSize, cancellationToken);
        var pendingList = pending.ToList();

        if (pendingList.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} pending knowledge extraction jobs.", pendingList.Count);

        var processedCount = 0;
        foreach (var item in pendingList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Circuit breaker: if a rate-limit failure was observed recently,
            // skip the remaining jobs in this batch so they stay Pending and
            // are picked up on a later poll once the cooldown has elapsed.
            if (IsRateLimitCooldownActive())
            {
                _logger.LogWarning(
                    "Rate-limit cooldown active (last 429 at {LastRateLimitAt:O}, cooldown {Cooldown}s). " +
                    "Skipping remaining {Remaining} pending job(s) in this batch; they will be retried on the next poll cycle.",
                    _lastRateLimitAt, _options.RateLimitCooldownSeconds, pendingList.Count - processedCount);
                break;
            }

            _logger.LogInformation(
                "Processing knowledge extraction queue {QueueId} for video {VideoId}.",
                item.Id, item.VideoId);

            try
            {
                // Lock the item as Running before processing.
                await queueService.MarkRunningAsync(item.Id, cancellationToken);

                await extractionService.ProcessQueueItemAsync(item.Id, cancellationToken);

                _logger.LogInformation(
                    "Knowledge extraction queue {QueueId} processed successfully for video {VideoId}.",
                    item.Id, item.VideoId);
            }
            catch (Exception ex)
            {
                // Apply retry with exponential backoff. Transient transcript
                // failures (rate limits / timeouts) are handled by the queue's
                // retry mechanism instead of being treated as terminal.
                var willRetry = await queueService.MarkAttemptFailedAsync(
                    item.Id, ex.Message, cancellationToken);

                if (ex is TranscriptTransientException)
                {
                    // Open the circuit: pause the remaining jobs in this batch
                    // so they do not all walk into the same rate-limit wall.
                    var now = DateTimeOffset.UtcNow;
                    _lastRateLimitAt = now;
                    _logger.LogWarning(
                        ex,
                        "Knowledge extraction queue {QueueId} failed for video {VideoId} with a transient " +
                        "error (rate limit or temporary failure). Remaining batch jobs will be paused for {Cooldown}s.",
                        item.Id, item.VideoId, _options.RateLimitCooldownSeconds);
                }
                else if (willRetry)
                {
                    _logger.LogError(
                        ex,
                        "Knowledge extraction queue {QueueId} failed for video {VideoId}. It will be retried with backoff.",
                        item.Id, item.VideoId);
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Knowledge extraction queue {QueueId} failed permanently after {RetryCount} retries for video {VideoId}.",
                        item.Id, _options.RetryCount, item.VideoId);
                }
            }

            // Space out requests to avoid triggering YouTube rate limiting
            // (HTTP 429) when multiple videos are processed back-to-back.
            processedCount++;
            if (processedCount < pendingList.Count && _options.DelayBetweenJobsSeconds > 0)
            {
                var delay = TimeSpan.FromSeconds(_options.DelayBetweenJobsSeconds);
                _logger.LogDebug(
                    "Waiting {Delay}s before processing the next knowledge extraction job.",
                    _options.DelayBetweenJobsSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Returns true when a rate-limit failure was observed within the
    /// configured cooldown window, indicating the worker should pause
    /// processing further jobs in the current batch.
    /// </summary>
    private bool IsRateLimitCooldownActive()
    {
        if (_lastRateLimitAt is null)
        {
            return false;
        }

        var cooldown = TimeSpan.FromSeconds(Math.Max(1, _options.RateLimitCooldownSeconds));
        return DateTimeOffset.UtcNow - _lastRateLimitAt.Value < cooldown;
    }
}
