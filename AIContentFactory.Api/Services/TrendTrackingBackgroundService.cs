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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wrap in the coordinator so we never run a tracking pass while
                // a discovery pass (or manual collect) is in-flight.
                await _coordinator.RunExclusiveAsync(
                    RunTrackingPassIfNeededAsync,
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
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

    private async Task RunTrackingPassIfNeededAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var quotaTracker = scope.ServiceProvider.GetRequiredService<IQuotaTracker>();
        var trendCollector = scope.ServiceProvider.GetRequiredService<TrendCollectorService>();

        // Only start a tracking pass once search.list is exhausted for today.
        // Before that point discovery searches still take precedence.
        if (!await quotaTracker.IsSearchQuotaExhaustedAsync(cancellationToken))
        {
            _logger.LogDebug("Search quota not exhausted yet. Skipping tracking pass.");
            return;
        }

        _logger.LogInformation("Search quota exhausted. Running Tracking Mode statistics refresh.");
        var summary = await trendCollector.TrackExistingAsync(cancellationToken);

        _logger.LogInformation(
            "Tracking pass finished: collected={Collected}, tracked={Tracked}, skipped={Skipped}, duration={DurationMs}ms.",
            summary.TotalCollected, summary.TotalTracked, summary.TotalSkipped, summary.DurationMs);
    }
}