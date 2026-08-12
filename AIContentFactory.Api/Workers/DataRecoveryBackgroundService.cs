using AIContentFactory.Api.Services;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Workers;

/// <summary>
/// Centralized recovery background service that finds retryable failures
/// across all agents and retries them with exponential backoff.
/// </summary>
public sealed class DataRecoveryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRecoveryBackgroundService> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public DataRecoveryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataRecoveryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataRecoveryBackgroundService started.");
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Data recovery sweep failed."); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        _logger.LogInformation("DataRecoveryBackgroundService stopped.");
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var failureRepo = scope.ServiceProvider.GetRequiredService<IDataProcessingFailureRepository>();
        var retryCalc = scope.ServiceProvider.GetRequiredService<RetryCalculator>();

        var retryable = (await failureRepo.GetRetryableAsync(10, ct)).ToList();
        if (retryable.Count == 0) return;

        _logger.LogInformation("Found {Count} retryable failures to recover.", retryable.Count);

        foreach (var failure in retryable)
        {
            ct.ThrowIfCancellationRequested();

            if (!retryCalc.ShouldRetry(failure.RetryCount))
            {
                await failureRepo.MarkPermanentFailedAsync(failure.Id,
                    $"Max retry attempts ({failure.MaxRetryAttempts}) exhausted.", ct);
                continue;
            }

            // Attempt recovery — each agent's recovery logic is triggered
            // via the recovery API controller or agent-specific services.
            // The current implementation records attempt failure but delegates
            // actual re-processing to the agent's own worker (e.g.
            // KnowledgeExtractionBackgroundService for queue items).
            var nextRetry = retryCalc.NextRetryAt(failure.RetryCount);
            await failureRepo.MarkRetryAttemptFailedAsync(failure.Id,
                $"Recovery attempt {failure.RetryCount + 1} — retrying via agent worker.", nextRetry, ct);
        }
    }
}