using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IQueueService" />
public sealed class QueueService : IQueueService
{
    private readonly IKnowledgeExtractionQueueRepository _queueRepository;
    private readonly IVideoMetadataRepository _videoMetadataRepository;
    private readonly KnowledgeExtractionOptions _options;
    private readonly ILogger<QueueService> _logger;

    public QueueService(
        IKnowledgeExtractionQueueRepository queueRepository,
        IVideoMetadataRepository videoMetadataRepository,
        IOptions<KnowledgeExtractionOptions> options,
        ILogger<QueueService> logger)
    {
        _queueRepository = queueRepository;
        _videoMetadataRepository = videoMetadataRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<KnowledgeExtractionQueue> EnqueueAsync(long videoId, int priority = 0, CancellationToken cancellationToken = default)
    {
        if (!await _videoMetadataRepository.ExistsAsync(videoId, cancellationToken))
        {
            throw new InvalidOperationException($"Video {videoId} does not exist in trending_videos.");
        }

        await _queueRepository.CreateIfNotExistsAsync(videoId, priority, cancellationToken);

        var queueItem = await _queueRepository.GetByVideoIdAsync(videoId, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to create queue item for video {videoId}.");

        _logger.LogInformation(
            "Knowledge extraction queue item {QueueId} created for video {VideoId} with status {Status}.",
            queueItem.Id, videoId, queueItem.Status);

        return queueItem;
    }

    public Task<IReadOnlyList<KnowledgeExtractionQueue>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
        => _queueRepository.GetPendingAsync(limit, cancellationToken);

    public Task<IReadOnlyList<KnowledgeExtractionQueue>> ListAsync(string? status, DateTime? date, int limit, int offset, CancellationToken cancellationToken = default)
        => _queueRepository.ListAsync(status, date, limit, offset, cancellationToken);

    public Task<KnowledgeExtractionQueue?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => _queueRepository.GetByIdAsync(id, cancellationToken);

    public Task<KnowledgeExtractionQueue?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default)
        => _queueRepository.GetByVideoIdAsync(videoId, cancellationToken);

    public Task MarkRunningAsync(long id, CancellationToken cancellationToken = default)
        => _queueRepository.MarkRunningAsync(id, cancellationToken);

    public Task MarkCompletedAsync(long id, long durationMs, CancellationToken cancellationToken = default)
        => _queueRepository.MarkCompletedAsync(id, durationMs, cancellationToken);

    public Task MarkTranscriptUnavailableAsync(long id, CancellationToken cancellationToken = default)
        => _queueRepository.MarkTranscriptUnavailableAsync(id, cancellationToken);

    public async Task MarkAttemptFailedAsync(long id, string error, CancellationToken cancellationToken = default)
    {
        var queueItem = await _queueRepository.GetByIdAsync(id, cancellationToken);
        if (queueItem is null)
        {
            return;
        }

        if (queueItem.RetryCount < _options.RetryCount)
        {
            // Exponential backoff: 30s → 60s → 120s (2^n * base)
            var backoffSeconds = TimeSpan.FromSeconds(Math.Pow(2, queueItem.RetryCount) * 30);
            var nextRetryAt = DateTimeOffset.UtcNow.Add(backoffSeconds);

            await _queueRepository.MarkRetryAsync(id, error, nextRetryAt, cancellationToken);
            _logger.LogWarning(
                "Queue item {QueueId} failed (attempt {RetryCount}/{MaxRetries}), scheduled for retry in {BackoffSeconds}s. Error: {Error}",
                id, queueItem.RetryCount + 1, _options.RetryCount, backoffSeconds.TotalSeconds, error);
            return;
        }

        await _queueRepository.MarkFailedAsync(id, error, cancellationToken);
        _logger.LogError(
            "Queue item {QueueId} failed permanently after {RetryCount} retries. Error: {Error}",
            id, _options.RetryCount, error);
    }

    public Task ResetForRetryAsync(long id, CancellationToken cancellationToken = default)
        => _queueRepository.ResetForRetryAsync(id, cancellationToken);
}