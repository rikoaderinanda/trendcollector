using Microsoft.AspNetCore.Mvc;
using AIContentFactory.Api.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace AIContentFactory.Api.Controllers;

/// <summary>
/// Health check endpoint for monitoring / load-balancer probes.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ILogger<HealthController> _logger;

    public HealthController(DbConnectionFactory connectionFactory, ILogger<HealthController> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>Returns service health and database status.</summary>
    /// <response code="200">Service healthy.</response>
    /// <response code="503">Service unhealthy (database unreachable).</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Health check",
        Description = "Returns liveness plus database connectivity status.")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            await cmd.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed: database unreachable.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    status = "unhealthy",
                    database = "unreachable",
                    checkedAt = DateTimeOffset.UtcNow,
                    uptimeMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
                });
        }

        return Ok(new
        {
            status = "healthy",
            database = "connected",
            checkedAt = DateTimeOffset.UtcNow,
            uptimeMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
        });
    }
}