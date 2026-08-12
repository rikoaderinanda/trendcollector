using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Background service that periodically refreshes statistics of already-collected
/// videos (Tracking Mode) every <see cref="TrackingModeOptions.TrackingIntervalMinutes"/>.
/// Only runs the tracking pass when the daily search.list quota is exhausted,
/// i.e. when discovery would no longer be allowed anyway.
/// </summary>
public sealed class TrendTrackingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CollectionCoordinator _coordinator;
    private readonly TrackingModeOptions _options;
    private readonly ILogger<TrendTrackingBackgroundService> _logger;

    /// <summary>How long to wait before the first pass so the discovery
    /// service can grab the coordinator gate first at startup.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(60);

    /// <summary>How long to wait for the coordinator gate before giving up.</summary>
    private static readonly TimeSpan CoordinatorTimeout = TimeSpan.FromSeconds(30);

    public TrendTrackingBackgroundService(
        IServiceScopeFactory scopeFactory,
        CollectionCoordinator coordinator,
        IOptions<TrackingModeOptions> options,
        ILogger<TrendTrackingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("TrendTrackingBackgroundService is disabled. Skipping startup.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.TrackingIntervalMinutes));

        _logger.LogInformation(
            "TrendTrackingBackgroundService started. Tracking every {IntervalMinutes} minutes when search quota is exhausted.",
            interval.TotalMinutes);

        // Give the discovery service a head start at startup so it can finish
        // its first pass before we contend for the coordinator gate.
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trend tracking background cycle failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("TrendTrackingBackgroundService stopped.");
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var quotaTracker = scope.ServiceProvider.GetRequiredService<IQuotaTracker>();

        // Check the quota *before* taking the coordinator gate, so we never
        // contend with the discovery service when no tracking pass is needed.
        if (!await quotaTracker.IsSearchQuotaExhaustedAsync(cancellationToken))
        {
            _logger.LogDebug("Search quota not exhausted yet. Skipping tracking pass.");
            return;
        }

        _logger.LogInformation("Search quota exhausted. Running Tracking Mode statistics refresh.");

        var trendCollector = scope.ServiceProvider.GetRequiredService<TrendCollectorService>();

        // Only acquire the gate when we actually need to run a tracking pass.
        // This prevents the "Another collection/tracking operation is still
        // running" error that used to occur at startup when the discovery
        // service was already holding the gate.
        var summary = await _coordinator.RunExclusiveAsync(
            ct => trendCollector.TrackExistingAsync(ct),
            CoordinatorTimeout,
            cancellationToken);

        _logger.LogInformation(
            "Tracking pass finished: collected={Collected}, tracked={Tracked}, skipped={Skipped}, duration={DurationMs}ms.",
            summary.TotalCollected, summary.TotalTracked, summary.TotalSkipped, summary.DurationMs);
    }
}