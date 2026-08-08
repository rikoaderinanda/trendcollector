using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;
using AIContentFactory.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace AIContentFactory.Api.Controllers;

/// <summary>
/// Knowledge extraction endpoints.
/// </summary>
[ApiController]
[Route("knowledge-extraction")]
public sealed class KnowledgeExtractionController : ControllerBase
{
    private readonly IQueueService _queueService;
    private readonly IKnowledgeExtractionService _extractionService;
    private readonly IVideoMetadataRepository _videoMetadataRepository;
    private readonly IVideoTranscriptRepository _transcriptRepository;
    private readonly IVideoKnowledgeRepository _knowledgeRepository;
    private readonly ILogger<KnowledgeExtractionController> _logger;

    public KnowledgeExtractionController(
        IQueueService queueService,
        IKnowledgeExtractionService extractionService,
        IVideoMetadataRepository videoMetadataRepository,
        IVideoTranscriptRepository transcriptRepository,
        IVideoKnowledgeRepository knowledgeRepository,
        ILogger<KnowledgeExtractionController> logger)
    {
        _queueService = queueService;
        _extractionService = extractionService;
        _videoMetadataRepository = videoMetadataRepository;
        _transcriptRepository = transcriptRepository;
        _knowledgeRepository = knowledgeRepository;
        _logger = logger;
    }

    /// <summary>Lists knowledge extraction queue items.</summary>
    /// <param name="status">Optional status filter: Pending, Running, Completed, Failed, TranscriptUnavailable.</param>
    /// <param name="date">Optional filter: only items created on this calendar date (yyyy-MM-dd).</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Queue items returned.</response>
    [SwaggerOperation(
        Summary = "List knowledge extraction jobs",
        Description = "Returns the knowledge extraction queue, optionally filtered by status, with pagination.")]
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IEnumerable<KnowledgeExtractionJobDto>), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(KnowledgeExtractionJobDtoExample))]
    public async Task<ActionResult<IEnumerable<KnowledgeExtractionJobDto>>> GetJobs(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _queueService.ListAsync(
            status,
            date,
            Math.Clamp(limit, 1, 100),
            Math.Max(offset, 0),
            cancellationToken);

        return Ok(jobs.Select(MapJob));
    }

    /// <summary>Gets full knowledge extraction detail for a video.</summary>
    /// <param name="videoId">Internal video id (trending_videos.id).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Detail returned.</response>
    /// <response code="404">Video not found.</response>
    [SwaggerOperation(
        Summary = "Get knowledge extraction detail by video",
        Description = "Returns the video metadata, transcript, structured knowledge, and queue status.")]
    [HttpGet("video/{videoId:long}")]
    [ProducesResponseType(typeof(KnowledgeExtractionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(KnowledgeExtractionDetailDtoExample))]
    public async Task<ActionResult<KnowledgeExtractionDetailDto>> GetVideoDetail(
        [FromRoute] long videoId,
        CancellationToken cancellationToken = default)
    {
        if (!await _videoMetadataRepository.ExistsAsync(videoId, cancellationToken))
        {
            return NotFound();
        }

        var metadata = await _videoMetadataRepository.GetByIdAsync(videoId, cancellationToken);
        var transcript = await _transcriptRepository.GetByVideoIdAsync(videoId, cancellationToken);
        var knowledge = await _knowledgeRepository.GetByVideoIdAsync(videoId, cancellationToken);
        var queue = await _queueService.GetByVideoIdAsync(videoId, cancellationToken);

        return Ok(new KnowledgeExtractionDetailDto
        {
            Metadata = metadata,
            Transcript = transcript,
            Knowledge = knowledge,
            Queue = queue
        });
    }

    /// <summary>Enqueues a video for knowledge extraction (Pending).</summary>
    /// <param name="videoId">Internal video id.</param>
    /// <param name="priority">Queue priority (higher is processed first).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Video enqueued.</response>
    /// <response code="404">Video not found.</response>
    [SwaggerOperation(
        Summary = "Enqueue a video for knowledge extraction",
        Description = "Creates a Pending queue item for the video. The background worker will process it automatically. No-op when already queued.")]
    [HttpPost("queue/{videoId:long}")]
    [ProducesResponseType(typeof(KnowledgeExtractionJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(KnowledgeExtractionJobDtoExample))]
    public async Task<ActionResult<KnowledgeExtractionJobDto>> EnqueueVideo(
        [FromRoute] long videoId,
        [FromQuery] int priority = 0,
        CancellationToken cancellationToken = default)
    {
        if (!await _videoMetadataRepository.ExistsAsync(videoId, cancellationToken))
        {
            return NotFound();
        }

        var queueItem = await _queueService.EnqueueAsync(videoId, priority, cancellationToken);

        _logger.LogInformation(
            "Video {VideoId} enqueued for knowledge extraction (queue {QueueId}, status {Status}).",
            videoId, queueItem.Id, queueItem.Status);

        return Ok(MapJob(queueItem));
    }

    /// <summary>Runs knowledge extraction manually for a video.</summary>
    /// <param name="videoId">Internal video id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Extraction triggered.</response>
    /// <response code="404">Video not found.</response>
    [SwaggerOperation(
        Summary = "Run knowledge extraction manually",
        Description = "Enqueues the video (if not yet queued), marks it Running and processes it immediately.")]
    [HttpPost("run/{videoId:long}")]
    [ProducesResponseType(typeof(RunKnowledgeExtractionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(RunKnowledgeExtractionResponseExample))]
    public async Task<ActionResult<RunKnowledgeExtractionResponse>> RunVideo(
        [FromRoute] long videoId,
        CancellationToken cancellationToken = default)
    {
        if (!await _videoMetadataRepository.ExistsAsync(videoId, cancellationToken))
        {
            return NotFound();
        }

        var startedAt = DateTimeOffset.UtcNow;
        var queueItem = await _queueService.EnqueueAsync(videoId, priority: 0, cancellationToken);

        _logger.LogInformation(
            "Manual knowledge extraction triggered for video {VideoId}, queue {QueueId}.",
            videoId, queueItem.Id);

        try
        {
            await _queueService.MarkRunningAsync(queueItem.Id, cancellationToken);
            await _extractionService.ProcessQueueItemAsync(queueItem.Id, cancellationToken);

            var finished = await _queueService.GetByIdAsync(queueItem.Id, cancellationToken);
            return Ok(new RunKnowledgeExtractionResponse
            {
                QueueId = queueItem.Id,
                VideoId = videoId,
                Status = finished?.Status.ToString() ?? "Unknown",
                RetryCount = finished?.RetryCount ?? 0,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Manual knowledge extraction failed for video {VideoId}, queue {QueueId}.",
                videoId, queueItem.Id);

            await _queueService.MarkAttemptFailedAsync(queueItem.Id, ex.Message, cancellationToken);

            return Ok(new RunKnowledgeExtractionResponse
            {
                QueueId = queueItem.Id,
                VideoId = videoId,
                Status = "Failed",
                RetryCount = 0,
                ErrorMessage = ex.Message,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>Retries a failed knowledge extraction job.</summary>
    /// <param name="queueId">Queue item id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Retry triggered.</response>
    /// <response code="404">Queue item not found.</response>
    [SwaggerOperation(
        Summary = "Retry a failed knowledge extraction job",
        Description = "Resets a failed or terminated queue item to Pending and processes it immediately.")]
    [HttpPost("retry/{queueId:long}")]
    [ProducesResponseType(typeof(RunKnowledgeExtractionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(RunKnowledgeExtractionResponseExample))]
    public async Task<ActionResult<RunKnowledgeExtractionResponse>> RetryJob(
        [FromRoute] long queueId,
        CancellationToken cancellationToken = default)
    {
        var queueItem = await _queueService.GetByIdAsync(queueId, cancellationToken);
        if (queueItem is null)
        {
            return NotFound();
        }

        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Manual retry triggered for knowledge extraction queue {QueueId} (video {VideoId}).",
            queueId, queueItem.VideoId);

        try
        {
            await _queueService.ResetForRetryAsync(queueId, cancellationToken);
            await _queueService.MarkRunningAsync(queueId, cancellationToken);
            await _extractionService.ProcessQueueItemAsync(queueId, cancellationToken);

            var finished = await _queueService.GetByIdAsync(queueId, cancellationToken);
            return Ok(new RunKnowledgeExtractionResponse
            {
                QueueId = queueId,
                VideoId = queueItem.VideoId,
                Status = finished?.Status.ToString() ?? "Unknown",
                RetryCount = finished?.RetryCount ?? 0,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Manual retry failed for knowledge extraction queue {QueueId} (video {VideoId}).",
                queueId, queueItem.VideoId);

            await _queueService.MarkAttemptFailedAsync(queueId, ex.Message, cancellationToken);

            return Ok(new RunKnowledgeExtractionResponse
            {
                QueueId = queueId,
                VideoId = queueItem.VideoId,
                Status = "Failed",
                RetryCount = 0,
                ErrorMessage = ex.Message,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private static KnowledgeExtractionJobDto MapJob(KnowledgeExtractionQueue item)
    {
        return new KnowledgeExtractionJobDto
        {
            Id = item.Id,
            VideoId = item.VideoId,
            Status = item.Status.ToString(),
            Priority = item.Priority,
            RetryCount = item.RetryCount,
            NextRetryAt = item.NextRetryAt,
            StartedAt = item.StartedAt,
            FinishedAt = item.FinishedAt,
            DurationMs = item.DurationMs,
            ErrorMessage = item.ErrorMessage,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}