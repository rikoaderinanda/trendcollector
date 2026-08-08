using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Services;
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

        foreach (var item in pendingList)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                // Apply retry with exponential backoff.
                await queueService.MarkAttemptFailedAsync(item.Id, ex.Message, cancellationToken);
                _logger.LogError(
                    ex,
                    "Knowledge extraction queue {QueueId} failed for video {VideoId}. It will be retried with backoff.",
                    item.Id, item.VideoId);
            }
        }
    }
}