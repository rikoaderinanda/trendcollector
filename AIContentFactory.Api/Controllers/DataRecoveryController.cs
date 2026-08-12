using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Controllers;

/// <summary>
/// Centralized data recovery endpoints for all agents.
/// </summary>
[ApiController]
[Route("api/data-recovery")]
public sealed class DataRecoveryController : ControllerBase
{
    private readonly IDataProcessingFailureRepository _failureRepo;
    private readonly ILogger<DataRecoveryController> _logger;

    public DataRecoveryController(
        IDataProcessingFailureRepository failureRepo,
        ILogger<DataRecoveryController> logger)
    {
        _failureRepo = failureRepo;
        _logger = logger;
    }

    /// <summary>Lists failures by agent and status.</summary>
    [HttpGet("failures")]
    [ProducesResponseType(typeof(IEnumerable<DataProcessingFailure>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DataProcessingFailure>>> GetFailures(
        [FromQuery] string? agent = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        // Reuse the retryable query but filter by agent/status in-memory
        var all = await _failureRepo.GetRetryableAsync(limit, ct);
        var filtered = all
            .Where(f => (agent == null || f.AgentName == agent)
                     && (status == null || f.Status == status));
        return Ok(filtered);
    }

    /// <summary>Retries a specific failure by id.</summary>
    [HttpPost("failures/{failureId:long}/retry")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RetryFailure(
        [FromRoute] long failureId,
        CancellationToken ct = default)
    {
        var failure = await _failureRepo.GetByIdAsync(failureId, ct);
        if (failure is null) return NotFound();

        _logger.LogInformation("Manual retry triggered for failure {Id} (Agent {Agent}, Entity {Type}#{EntityId}).",
            failureId, failure.AgentName, failure.EntityType, failure.EntityId);

        // Mark the failure as retryable so the recovery worker picks it up
        await _failureRepo.MarkRetryAttemptFailedAsync(failureId,
            "Manual retry triggered by user.",
            DateTimeOffset.UtcNow, ct);

        return Ok(new { failureId, status = "Retryable", retryCount = failure.RetryCount + 1 });
    }

    /// <summary>Quarantines a failure (marks for manual review only).</summary>
    [HttpPost("failures/{failureId:long}/quarantine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> QuarantineFailure(
        [FromRoute] long failureId,
        CancellationToken ct = default)
    {
        var failure = await _failureRepo.GetByIdAsync(failureId, ct);
        if (failure is null) return NotFound();

        await _failureRepo.MarkQuarantinedAsync(failureId, ct);
        _logger.LogInformation("Failure {Id} quarantined.", failureId);

        return Ok(new { failureId, status = "Quarantined" });
    }
}