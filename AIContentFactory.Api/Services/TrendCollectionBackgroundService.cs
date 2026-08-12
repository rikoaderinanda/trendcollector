using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Background service that polls for "active" trend keywords and sends them
/// to the Trend Collector API for video collection.
/// </summary>
public sealed class TrendCollectionBackgroundService : BackgroundService
{
    private const int BatchSize = 10;

    /// <summary>How long to wait for the coordinator gate before giving up.</summary>
    private static readonly TimeSpan CoordinatorTimeout = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CollectionCoordinator _coordinator;
    private readonly TrendCollectorOptions _options;
    private readonly ILogger<TrendCollectionBackgroundService> _logger;

    public TrendCollectionBackgroundService(
        IServiceScopeFactory scopeFactory,
        CollectionCoordinator coordinator,
        IOptions<TrendCollectorOptions> options,
        ILogger<TrendCollectionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TrendCollectionBackgroundService started. Polling every {Interval}s for up to {BatchSize} active keywords.",
            _options.PollIntervalSeconds, BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wrap in the coordinator so we never run a discovery pass while
                // a tracking pass (or manual collect) is in-flight.
                await _coordinator.RunExclusiveAsync(
                    ProcessPendingKeywordsAsync,
                    CoordinatorTimeout,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trend collection background cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("TrendCollectionBackgroundService stopped.");
    }

    private async Task ProcessPendingKeywordsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var keywordRepository = scope.ServiceProvider.GetRequiredService<ITrendKeywordRepository>();

        var keywords = await keywordRepository.ListAsync(
            country: null,
            language: null,
            niche: null,
            minPriority: null,
            status: KeywordStatus.Active,
            limit: BatchSize,
            offset: 0,
            cancellationToken);

        var activeList = keywords.ToList();
        if (activeList.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} active keywords to collect.", activeList.Count);

        var collectorService = scope.ServiceProvider.GetRequiredService<TrendCollectorService>();

        foreach (var keyword in activeList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Collecting keyword '{Keyword}' (id={Id}, country={Country}, language={Language}, priority={Priority})",
                keyword.Keyword, keyword.Id, keyword.Country, keyword.Language, keyword.Priority);

            try
            {
                var result = await collectorService.CollectAsync(new CollectRequest
                {
                    Keyword = keyword.Keyword,
                    Country = keyword.Country,
                    Language = keyword.Language,
                    MaxResults = _options.MaxResultsPerKeyword
                }, cancellationToken);

                // Direct service call may switch to Tracking Mode internally when the
                // daily search quota is exhausted. Mark the keyword collected either way.
                await keywordRepository.UpdateStatusAsync(keyword.Id, KeywordStatus.Collected, cancellationToken);
                _logger.LogInformation(
                    "Keyword '{Keyword}' (id={Id}) collected (mode={Mode}, collected={Collected}, saved={Saved}, tracked={Tracked}).",
                    keyword.Keyword, keyword.Id, result.Mode, result.TotalCollected, result.TotalSaved, result.TotalTracked);
            }
            catch (Exception ex)
            {
                // Transient errors. Keep keyword active for the next cycle.
                _logger.LogError(
                    ex,
                    "Unexpected error while collecting keyword '{Keyword}' (id={Id}). It will be retried on the next cycle.",
                    keyword.Keyword, keyword.Id);
            }
        }
    }
}
