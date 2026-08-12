using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;
using AIContentFactory.Api.Services;

namespace AIContentFactory.Api.Controllers;

/// <summary>
/// Viral Analyzer (Agent 3) endpoints.
/// </summary>
[ApiController]
[Route("api/viral-analysis")]
public sealed class ViralAnalysisController : ControllerBase
{
    private readonly IViralAnalysisService _viralAnalysisService;
    private readonly IViralAnalysisRepository _analysisRepository;
    private readonly ILogger<ViralAnalysisController> _logger;

    public ViralAnalysisController(
        IViralAnalysisService viralAnalysisService,
        IViralAnalysisRepository analysisRepository,
        ILogger<ViralAnalysisController> logger)
    {
        _viralAnalysisService = viralAnalysisService;
        _analysisRepository = analysisRepository;
        _logger = logger;
    }

    /// <summary>Lists viral analysis runs, newest first.</summary>
    /// <param name="limit">Max results.</param>
    /// <param name="offset">Skip count.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Runs returned.</response>
    [SwaggerOperation(
        Summary = "List Viral Analysis Runs",
        Description = "Returns recent viral analysis runs ordered by start time, newest first.")]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ViralAnalysisRun>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ViralAnalysisRun>>> GetRuns(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var runs = await _analysisRepository.GetRunsAsync(
            Math.Clamp(limit, 1, 100),
            Math.Max(offset, 0),
            cancellationToken);

        return Ok(runs);
    }

    /// <summary>Starts a viral analysis run.</summary>
    /// <param name="request">Analysis filters (all optional; Empty = Daily Analysis mode).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Analysis started successfully.</response>
    /// <response code="400">Request validation failed.</response>
    [HttpPost("run")]
    [SwaggerOperation(
        Summary = "Run Viral Analysis",
        Description = "Starts a new analysis. When no filters are provided, the default Daily Analysis mode " +
                      "analyzes all eligible candidates collected in the configured lookback window.")]
    [ProducesResponseType(typeof(RunViralAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RunViralAnalysisResponse>> Run(
        [FromBody] RunViralAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting Viral Analysis. Niche='{Niche}', Keyword='{Keyword}', MaxVideos={MaxVideos}.",
            request.Niche, request.TrendKeyword, request.MaximumVideos);

        var runId = await _viralAnalysisService.RunAsync(request, cancellationToken);

        var run = await _analysisRepository.GetRunByIdAsync(runId, cancellationToken);
        if (run is null)
        {
            return NotFound($"Analysis run {runId} not found after execution.");
        }

        return Ok(new RunViralAnalysisResponse
        {
            AnalysisRunId = runId,
            Status = run.Status,
            TotalCandidates = run.TotalCandidates,
            EligibleCandidates = run.EligibleCandidates
        });
    }

    /// <summary>Gets a complete viral analysis by run id.</summary>
    /// <param name="analysisRunId">The analysis run id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Analysis returned.</response>
    /// <response code="404">Analysis not found.</response>
    [SwaggerOperation(
        Summary = "Get Viral Analysis",
        Description =
            "Returns the complete analysis, including winning patterns, content opportunities and the TOP 1 recommendation.")]
    [HttpGet("{analysisRunId:long}")]
    [ProducesResponseType(typeof(ViralAnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ViralAnalysisResultDto>> GetById(
        [FromRoute] long analysisRunId,
        CancellationToken cancellationToken = default)
    {
        var run = await _analysisRepository.GetRunByIdAsync(analysisRunId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        var patterns = await _analysisRepository.GetPatternsByRunIdAsync(analysisRunId, cancellationToken);
        var opportunities = await _analysisRepository.GetOpportunitiesByRunIdAsync(analysisRunId, cancellationToken);
        var recommended = await _analysisRepository.GetRecommendedOpportunityAsync(analysisRunId, cancellationToken);

        var result = new ViralAnalysisResultDto
        {
            Id = run.Id,
            AnalysisRunId = run.Id,
            AnalyzedAt = run.StartedAt,
            TrendSummary = run.TrendSummary,
            MarketObservation = run.MarketObservation,
            WinningPatterns = patterns.ToList(),
            ContentOpportunities = opportunities.ToList(),
            RecommendedOpportunity = recommended,
            ConfidenceScore = run.ConfidenceScore,
            AnalysisVersion = run.AnalysisVersion,
            CreatedAt = run.CreatedAt
        };

        return Ok(result);
    }

    /// <summary>Gets winning patterns for an analysis run.</summary>
    /// <param name="analysisRunId">The analysis run id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Patterns returned.</response>
    /// <response code="404">Analysis not found.</response>
    [SwaggerOperation(
        Summary = "Get Winning Patterns",
        Description = "Returns the cross-video winning patterns detected for an analysis run.")]
    [HttpGet("{analysisRunId:long}/patterns")]
    [ProducesResponseType(typeof(IEnumerable<WinningPatternDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<WinningPatternDto>>> GetPatterns(
        [FromRoute] long analysisRunId,
        CancellationToken cancellationToken = default)
    {
        var run = await _analysisRepository.GetRunByIdAsync(analysisRunId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        var patterns = await _analysisRepository.GetPatternsByRunIdAsync(analysisRunId, cancellationToken);
        var dtos = patterns.Select(p => new WinningPatternDto
        {
            Id = p.Id,
            PatternType = p.PatternType,
            PatternName = p.PatternName,
            Description = p.Description,
            Frequency = p.Frequency,
            SupportingVideoCount = p.SupportingVideoCount,
            AverageMomentumScore = p.AverageMomentumScore,
            Evidence = p.Evidence
        });

        return Ok(dtos);
    }

    /// <summary>Gets ranked content opportunities for an analysis run.</summary>
    /// <param name="analysisRunId">The analysis run id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Opportunities returned.</response>
    /// <response code="404">Analysis not found.</response>
    [SwaggerOperation(
        Summary = "Get Content Opportunities",
        Description = "Returns the ranked content opportunities generated for an analysis run.")]
    [HttpGet("{analysisRunId:long}/opportunities")]
    [ProducesResponseType(typeof(IEnumerable<ContentOpportunityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ContentOpportunityDto>>> GetOpportunities(
        [FromRoute] long analysisRunId,
        CancellationToken cancellationToken = default)
    {
        var run = await _analysisRepository.GetRunByIdAsync(analysisRunId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        var opportunities = await _analysisRepository.GetOpportunitiesByRunIdAsync(analysisRunId, cancellationToken);
        var dtos = opportunities.Select(MapToDto);

        return Ok(dtos);
    }

    /// <summary>Gets the TOP 1 recommended opportunity for an analysis run.</summary>
    /// <param name="analysisRunId">The analysis run id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Recommendation returned.</response>
    /// <response code="404">Analysis or recommendation not found.</response>
    [SwaggerOperation(
        Summary = "Get TOP 1 Recommendation",
        Description = "Returns the recommended content opportunity - the strategic blueprint for the next agent.")]
    [HttpGet("{analysisRunId:long}/recommendation")]
    [ProducesResponseType(typeof(ViralAnalysisRecommendationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ViralAnalysisRecommendationDto>> GetRecommendation(
        [FromRoute] long analysisRunId,
        CancellationToken cancellationToken = default)
    {
        var run = await _analysisRepository.GetRunByIdAsync(analysisRunId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        var recommended = await _analysisRepository.GetRecommendedOpportunityAsync(analysisRunId, cancellationToken);
        if (recommended is null)
        {
            return NotFound("No TOP 1 recommendation available for this analysis run.");
        }

        var evidence = recommended.Evidence
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var dto = new ViralAnalysisRecommendationDto
        {
            Opportunity = MapToDto(recommended),
            ConfidenceScore = run.ConfidenceScore ?? recommended.ConfidenceScore,
            WhyThisOpportunity = recommended.WhyNow,
            Evidence = evidence,
            Risks = string.IsNullOrWhiteSpace(recommended.RiskLevel)
                ? new List<string>()
                : new List<string> { $"Risk level: {recommended.RiskLevel}" },
            DifferentiationStrategy = recommended.DifferentiationStrategy ?? string.Empty
        };

        return Ok(dto);
    }

    // ---------- Mapping helpers ----------

    private static ContentOpportunityDto MapToDto(ContentOpportunity o) => new()
    {
        Id = o.Id,
        Rank = o.Rank,
        Topic = o.Topic,
        Angle = o.Angle,
        TargetAudience = o.TargetAudience,
        Hook = o.Hook,
        Format = o.Format,
        Structure = o.Structure,
        Emotion = o.Emotion,
        PsychologicalTrigger = o.PsychologicalTrigger,
        WhyNow = o.WhyNow,
        ContentGap = o.ContentGap,
        DifferentiationStrategy = o.DifferentiationStrategy,
        CallToAction = o.CallToAction,
        OpportunityScore = o.OpportunityScore,
        ConfidenceScore = o.ConfidenceScore,
        RiskLevel = o.RiskLevel,
        SupportingVideoIds = o.SupportingVideoIds,
        Evidence = o.Evidence
    };
}