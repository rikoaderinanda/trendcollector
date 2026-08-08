using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;
using AIContentFactory.Api.Services;

namespace AIContentFactory.Api.Controllers;

/// <summary>
/// Trend Discovery endpoints - discovers WHAT should be collected by the Trend Collector.
/// </summary>
[ApiController]
[Route("api/trend-discovery")]
public sealed class TrendDiscoveryController : ControllerBase
{
    private readonly TrendDiscoveryService _discoveryService;
    private readonly ITrendKeywordRepository _keywordRepository;
    private readonly ITrendDiscoveryJobRepository _jobRepository;
    private readonly ILogger<TrendDiscoveryController> _logger;

    public TrendDiscoveryController(
        TrendDiscoveryService discoveryService,
        ITrendKeywordRepository keywordRepository,
        ITrendDiscoveryJobRepository jobRepository,
        ILogger<TrendDiscoveryController> logger)
    {
        _discoveryService = discoveryService;
        _keywordRepository = keywordRepository;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    /// <summary>Runs an immediate trend discovery job using the configured AI provider.</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Discovery job finished.</response>
    [HttpPost("run")]
    [SwaggerOperation(
        Summary = "Run trend discovery",
        Description = "Triggers an immediate discovery run. The AI provider generates YouTube search keywords " +
                      "which are upserted into the trend_keywords table (duplicates update priority). " +
                      "The full prompt and raw AI response are stored for audit.")]
    [ProducesResponseType(typeof(RunDiscoveryResponse), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(RunDiscoveryResponseExample))]
    public async Task<ActionResult<RunDiscoveryResponse>> Run(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("POST /api/trend-discovery/run received.");

        var result = await _discoveryService.RunAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists discovered keywords with optional filters.</summary>
    /// <param name="country">Filter by country, e.g. "Global", "ID".</param>
    /// <param name="language">Filter by language code, e.g. "en", "id".</param>
    /// <param name="niche">Filter by niche, e.g. "Artificial Intelligence".</param>
    /// <param name="minPriority">Only keywords with priority >= this value (1-100).</param>
    /// <param name="status">Filter by status, e.g. "active".</param>
    /// <param name="limit">Maximum number of results (1-100).</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Keywords returned.</response>
    [HttpGet("keywords")]
    [SwaggerOperation(
        Summary = "List discovered keywords",
        Description = "Returns discovered search keywords, optionally filtered by country, language, niche, " +
                      "minimum priority and status. Ordered by priority (highest first).")]
    [ProducesResponseType(typeof(IEnumerable<TrendKeyword>), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(TrendKeywordExample))]
    public async Task<ActionResult<IEnumerable<TrendKeyword>>> GetKeywords(
        [FromQuery] string? country = null,
        [FromQuery] string? language = null,
        [FromQuery] string? niche = null,
        [FromQuery] int? minPriority = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var keywords = await _keywordRepository.ListAsync(
            country,
            language,
            niche,
            minPriority,
            status,
            Math.Clamp(limit, 1, 100),
            Math.Max(offset, 0),
            cancellationToken);

        return Ok(keywords);
    }

    /// <summary>Lists trend discovery execution history.</summary>
    /// <param name="date">Optional filter: only jobs started on this calendar date (yyyy-MM-dd).</param>
    /// <param name="limit">Maximum number of results (1-100).</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Jobs returned.</response>
    [HttpGet("jobs")]
    [SwaggerOperation(
        Summary = "List discovery jobs",
        Description = "Returns the history of trend discovery executions, ordered by start time (newest first).")]
    [ProducesResponseType(typeof(IEnumerable<TrendDiscoveryJob>), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(TrendDiscoveryJobExample))]
    public async Task<ActionResult<IEnumerable<TrendDiscoveryJob>>> GetJobs(
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
}