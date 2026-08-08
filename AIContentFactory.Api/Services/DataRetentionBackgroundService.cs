using Dapper;
using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Data;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Periodic data-retention sweep: removes old collection_jobs and old
/// video_statistics snapshots while preserving the latest snapshot of every
/// video. Runs on a configurable interval and is idempotent.
/// </summary>
public sealed class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TrackingModeOptions _trackingOptions;
    private readonly ILogger<DataRetentionBackgroundService> _logger;

    /// <summary>How often the retention sweep runs.</summary>
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    /// <summary>Number of days of collection_jobs history to keep.</summary>
    private static readonly TimeSpan JobsRetentionWindow = TimeSpan.FromDays(30);

    /// <summary>Number of days of video_statistics snapshots to keep (beyond latest).</summary>
    private static readonly TimeSpan StatisticsRetentionWindow = TimeSpan.FromDays(30);

    public DataRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<TrackingModeOptions> trackingOptions,
        ILogger<DataRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _trackingOptions = trackingOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_trackingOptions.Enabled)
        {
            _logger.LogInformation("DataRetentionBackgroundService is disabled (TrackingMode.Enabled=false). Skipping retention sweep.");
            return;
        }

        _logger.LogInformation(
            "DataRetentionBackgroundService started. Sweeping every {Hours}h. Keeps {JobsDays} days of job history and {StatsDays} days of statistics snapshots.",
            _interval.TotalHours, JobsRetentionWindow.TotalDays, StatisticsRetentionWindow.TotalDays);

        // Give the application a short warm-up before the first sweep.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data retention sweep failed.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("DataRetentionBackgroundService stopped.");
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<DbConnectionFactory>();

        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        // 1. Remove collection_jobs older than the retention window (finished or not).
        var jobCutoff = DateTime.UtcNow - JobsRetentionWindow;
        const string deleteJobsSql = """
            DELETE FROM collection_jobs
            WHERE started_at < @Cutoff;
            """;

        var deletedJobs = await connection.ExecuteAsync(
            deleteJobsSql,
            new { Cutoff = jobCutoff },
            commandTimeout: 120);

        // 2. Remove old video_statistics snapshots that are NOT the latest for each video.
        //    Keeps the most recent snapshot intact (so growth/velocity history stays available),
        //    but drops everything older than the retention window.
        const string deleteStatsSql = """
            DELETE FROM video_statistics v
            WHERE v.captured_at < @Cutoff
              AND v.id NOT IN (
                  SELECT DISTINCT ON (vs.video_id) vs.id
                  FROM video_statistics vs
                  ORDER BY vs.video_id, vs.captured_at DESC
              );
            """;

        var statsCutoff = DateTime.UtcNow - StatisticsRetentionWindow;
        var deletedStats = await connection.ExecuteAsync(
            deleteStatsSql,
            new { Cutoff = statsCutoff },
            commandTimeout: 120);

        if (deletedJobs > 0 || deletedStats > 0)
        {
            _logger.LogInformation(
                "Data retention sweep: deleted {Jobs} old collection_jobs and {Stats} old statistics snapshots.",
                deletedJobs, deletedStats);
        }
        else
        {
            _logger.LogDebug("Data retention sweep: nothing to clean.");
        }
    }
}