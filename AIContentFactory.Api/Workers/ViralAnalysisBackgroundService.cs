using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Services;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.Workers;

/// <summary>
/// Background service that runs a Viral Analysis once per day after
/// Agent 1 (Trend Collector) and Agent 2 (Knowledge Extraction) have
/// completed. Uses the default Daily Analysis mode.
/// </summary>
public sealed class ViralAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ViralAnalysisOptions _options;
    private readonly ILogger<ViralAnalysisBackgroundService> _logger;

    /// <summary>How long to wait before the first daily check at startup.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    public ViralAnalysisBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ViralAnalysisOptions> options,
        ILogger<ViralAnalysisBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Viral Analysis background service is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(60, _options.WorkerIntervalMinutes));

        _logger.LogInformation(
            "ViralAnalysisBackgroundService started. Checking every {IntervalMinutes} minutes.",
            interval.TotalMinutes);

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var lastRunDateUtc = DateTime.UtcNow.Date.AddDays(-1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var todayUtc = DateTime.UtcNow.Date;

                // Run once per UTC day.
                if (todayUtc > lastRunDateUtc)
                {
                    await RunDailyAnalysisAsync(stoppingToken);
                    lastRunDateUtc = todayUtc;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Viral analysis background cycle failed.");
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

        _logger.LogInformation("ViralAnalysisBackgroundService stopped.");
    }

    private async Task RunDailyAnalysisAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IViralAnalysisService>();

        _logger.LogInformation("Running daily Viral Analysis.");

        var request = new RunViralAnalysisRequest
        {
            // Default Daily Analysis mode: analyze the configured lookback window.
            MinimumCandidateScore = _options.MinimumMomentumScore,
            MaximumVideos = _options.MaxVideosPerAnalysis
        };

        var runId = await service.RunAsync(request, cancellationToken);

        _logger.LogInformation("Daily Viral Analysis completed. Run id: {RunId}.", runId);
    }
}