using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using AIContentFactory.Api.Models;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;
using AIContentFactory.Api.Services;

namespace AIContentFactory.Api.Controllers;

/// <summary>
/// Trend collection endpoints.
/// </summary>
[ApiController]
[Route("api/trend")]
public sealed class TrendController : ControllerBase
{
    private readonly TrendCollectorService _trendCollectorService;
    private readonly CollectionCoordinator _coordinator;
    private readonly IVideoRepository _videoRepository;
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<TrendController> _logger;

    public TrendController(
        TrendCollectorService trendCollectorService,
        CollectionCoordinator coordinator,
        IVideoRepository videoRepository,
        IJobRepository jobRepository,
        ILogger<TrendController> logger)
    {
        _trendCollectorService = trendCollectorService;
        _coordinator = coordinator;
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

        // Manual collects also go through the coordinator so they never race
        // with a background discovery or tracking pass.
        var summary = await _coordinator.RunExclusiveAsync(
            (ct) => _trendCollectorService.CollectAsync(request, ct),
            TimeSpan.FromSeconds(10),
            cancellationToken);

        return Ok(summary);
    }

    /// <summary>Lists collection jobs.</summary>
    /// <param name="date">Optional filter: only jobs started on this calendar date (yyyy-MM-dd).</param>
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
        [FromQuery] DateTime? date = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _jobRepository.ListAsync(
            date,
            Math.Clamp(limit, 1, 100),
            Math.Max(offset, 0),
            cancellationToken);

        return Ok(jobs);
    }

    /// <summary>Lists collected videos.</summary>
    /// <param name="language">Optional language filter (e.g. "id").</param>
    /// <param name="date">Optional filter: only videos created on this calendar date (yyyy-MM-dd).</param>
    /// <param name="sortBy">Optional sort column: published_at, views, likes, comments, favorites, engagement_rate, view_per_day, video_age_days, captured_at, views_per_hour, like_velocity, comment_velocity, or growth_score.</param>
    /// <param name="sortDirection">Sort direction: "asc" or "desc" (default "desc").</param>
    /// <param name="minViews">Minimum latest-snapshot views.</param>
    /// <param name="maxViews">Maximum latest-snapshot views.</param>
    /// <param name="minLikes">Minimum latest-snapshot likes.</param>
    /// <param name="maxLikes">Maximum latest-snapshot likes.</param>
    /// <param name="minComments">Minimum latest-snapshot comments.</param>
    /// <param name="maxComments">Maximum latest-snapshot comments.</param>
    /// <param name="minFavorites">Minimum latest-snapshot favorites.</param>
    /// <param name="maxFavorites">Maximum latest-snapshot favorites.</param>
    /// <param name="minEngagementRate">Minimum latest-snapshot engagement rate.</param>
    /// <param name="maxEngagementRate">Maximum latest-snapshot engagement rate.</param>
    /// <param name="minViewPerDay">Minimum latest-snapshot views per day.</param>
    /// <param name="maxViewPerDay">Maximum latest-snapshot views per day.</param>
    /// <param name="minVideoAgeDays">Minimum latest-snapshot video age (days).</param>
    /// <param name="maxVideoAgeDays">Maximum latest-snapshot video age (days).</param>
    /// <param name="capturedAfter">Only videos whose latest snapshot was captured at/after this timestamp.</param>
    /// <param name="capturedBefore">Only videos whose latest snapshot was captured at/before this timestamp.</param>
    /// <param name="minViewsPerHour">Minimum latest-snapshot views per hour (tracking mode).</param>
    /// <param name="maxViewsPerHour">Maximum latest-snapshot views per hour (tracking mode).</param>
    /// <param name="minLikeVelocity">Minimum latest-snapshot like velocity (tracking mode).</param>
    /// <param name="maxLikeVelocity">Maximum latest-snapshot like velocity (tracking mode).</param>
    /// <param name="minCommentVelocity">Minimum latest-snapshot comment velocity (tracking mode).</param>
    /// <param name="maxCommentVelocity">Maximum latest-snapshot comment velocity (tracking mode).</param>
    /// <param name="minGrowthScore">Minimum latest-snapshot growth score (tracking mode).</param>
    /// <param name="maxGrowthScore">Maximum latest-snapshot growth score (tracking mode).</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Videos returned.</response>
    [SwaggerOperation(
        Summary = "List collected videos",
        Description =
            "Returns collected videos, optionally filtered by language, collection date, statistics ranges, and sorting.")]
    [HttpGet("videos")]
    [ProducesResponseType(typeof(IEnumerable<VideoListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<VideoListItemDto>>> GetVideos(
        [FromQuery] string? language = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] long? minViews = null,
        [FromQuery] long? maxViews = null,
        [FromQuery] long? minLikes = null,
        [FromQuery] long? maxLikes = null,
        [FromQuery] long? minComments = null,
        [FromQuery] long? maxComments = null,
        [FromQuery] long? minFavorites = null,
        [FromQuery] long? maxFavorites = null,
        [FromQuery] decimal? minEngagementRate = null,
        [FromQuery] decimal? maxEngagementRate = null,
        [FromQuery] decimal? minViewPerDay = null,
        [FromQuery] decimal? maxViewPerDay = null,
        [FromQuery] decimal? minVideoAgeDays = null,
        [FromQuery] decimal? maxVideoAgeDays = null,
        [FromQuery] DateTimeOffset? capturedAfter = null,
        [FromQuery] DateTimeOffset? capturedBefore = null,
        [FromQuery] decimal? minViewsPerHour = null,
        [FromQuery] decimal? maxViewsPerHour = null,
        [FromQuery] decimal? minLikeVelocity = null,
        [FromQuery] decimal? maxLikeVelocity = null,
        [FromQuery] decimal? minCommentVelocity = null,
        [FromQuery] decimal? maxCommentVelocity = null,
        [FromQuery] decimal? minGrowthScore = null,
        [FromQuery] decimal? maxGrowthScore = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var query = new VideoListQuery
        {
            Language = language,
            Date = date,
            SortBy = sortBy,
            SortDirection = sortDirection,
            MinViews = minViews,
            MaxViews = maxViews,
            MinLikes = minLikes,
            MaxLikes = maxLikes,
            MinComments = minComments,
            MaxComments = maxComments,
            MinFavorites = minFavorites,
            MaxFavorites = maxFavorites,
            MinEngagementRate = minEngagementRate,
            MaxEngagementRate = maxEngagementRate,
            MinViewPerDay = minViewPerDay,
            MaxViewPerDay = maxViewPerDay,
            MinVideoAgeDays = minVideoAgeDays,
            MaxVideoAgeDays = maxVideoAgeDays,
            CapturedAfter = capturedAfter,
            CapturedBefore = capturedBefore,
            MinViewsPerHour = minViewsPerHour,
            MaxViewsPerHour = maxViewsPerHour,
            MinLikeVelocity = minLikeVelocity,
            MaxLikeVelocity = maxLikeVelocity,
            MinCommentVelocity = minCommentVelocity,
            MaxCommentVelocity = maxCommentVelocity,
            MinGrowthScore = minGrowthScore,
            MaxGrowthScore = maxGrowthScore,
            Limit = limit,
            Offset = offset,
        };

        var videos = await _videoRepository.ListWithLatestStatsAsync(query, cancellationToken);

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