using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using TrendCollector.Api.Models.Dtos;
using TrendCollector.Api.Models.Entities;
using TrendCollector.Api.Repositories;
using TrendCollector.Api.Services;

namespace TrendCollector.Api.Controllers;

/// <summary>
/// Trend collection endpoints.
/// </summary>
[ApiController]
[Route("api/trend")]
public sealed class TrendController : ControllerBase
{
    private readonly TrendCollectorService _trendCollectorService;
    private readonly IVideoRepository _videoRepository;
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<TrendController> _logger;

    public TrendController(
        TrendCollectorService trendCollectorService,
        IVideoRepository videoRepository,
        IJobRepository jobRepository,
        ILogger<TrendController> logger)
    {
        _trendCollectorService = trendCollectorService;
        _videoRepository = videoRepository;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    /// <summary>Collects trending videos for a keyword from YouTube.</summary>
    /// <param name="request">Keyword, language, country and max results.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Collection finished successfully.</response>
    /// <response code="400">Request validation failed.</response>
    [HttpPost("collect")]
    [SwaggerOperation(
        Summary = "Collect trending videos",
        Description = "Searches YouTube for a keyword, fetches full video and channel details, saves them to the database and returns an execution summary.")]
    [SwaggerRequestExample(typeof(CollectRequest), typeof(CollectRequestExample))]
    [ProducesResponseType(typeof(CollectSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(CollectSummaryExample))]
    public async Task<ActionResult<CollectSummary>> Collect(
        [FromBody, SwaggerRequestBody("Keyword to search, language and country codes, and the maximum number of results (1-50).", Required = true)]
        CollectRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Collecting trend for keyword '{Keyword}'", request.Keyword);

        var summary = await _trendCollectorService.CollectAsync(request, cancellationToken);
        return Ok(summary);
    }

    /// <summary>Lists collection jobs.</summary>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Jobs returned.</response>
    [SwaggerOperation(
        Summary = "List collection jobs",
        Description = "Returns the history of trend collection jobs ordered by start time, newest first.")]
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IEnumerable<CollectionJob>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CollectionJob>>> GetJobs(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _jobRepository.ListAsync(
            Math.Clamp(limit, 1, 100),
            Math.Max(offset, 0),
            cancellationToken);

        return Ok(jobs);
    }

    /// <summary>Lists collected videos.</summary>
    /// <param name="language">Optional language filter (e.g. "id").</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Videos returned.</response>
    [SwaggerOperation(
        Summary = "List collected videos",
        Description = "Returns collected videos, optionally filtered by language, with pagination.")]
    [HttpGet("videos")]
    [ProducesResponseType(typeof(IEnumerable<TrendingVideo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrendingVideo>>> GetVideos(
        [FromQuery] string? language = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var videos = await _videoRepository.ListAsync(
            language,
            Math.Clamp(limit, 1, 100),
            Math.Max(offset, 0),
            cancellationToken);

        return Ok(videos);
    }

    /// <summary>Gets a single video with its latest statistics.</summary>
    /// <param name="id">Internal video id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Video found.</response>
    /// <response code="404">Video not found.</response>
    [SwaggerOperation(
        Summary = "Get video detail",
        Description = "Returns a single video with its most recent statistics snapshot.")]
    [HttpGet("videos/{id:long}")]
    [ProducesResponseType(typeof(VideoDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VideoDetailDto>> GetVideo(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var video = await _videoRepository.GetByIdAsync(id, cancellationToken);
        if (video is null)
        {
            return NotFound();
        }

        var statistics = await _videoRepository.GetLatestStatisticsAsync(id, cancellationToken);

        return Ok(new VideoDetailDto { Video = video, Statistics = statistics });
    }
}