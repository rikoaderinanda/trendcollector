using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IQuotaTracker" />
public sealed class QuotaTracker : IQuotaTracker
{
    private readonly IQuotaRepository _quotaRepository;
    private readonly TrackingModeOptions _options;

    public QuotaTracker(IQuotaRepository quotaRepository, IOptions<TrackingModeOptions> options)
    {
        _quotaRepository = quotaRepository;
        _options = options.Value;
    }

    public Task<int> GetSearchCallCountTodayAsync(CancellationToken cancellationToken = default)
        => _quotaRepository.GetCallCountAsync(DateTime.UtcNow.Date, IQuotaTracker.SearchEndpoint, cancellationToken);

    public Task IncrementSearchCallCountAsync(CancellationToken cancellationToken = default)
        => _quotaRepository.IncrementCallCountAsync(DateTime.UtcNow.Date, IQuotaTracker.SearchEndpoint, cancellationToken);

    public Task IncrementVideosCallCountAsync(CancellationToken cancellationToken = default)
        => _quotaRepository.IncrementCallCountAsync(DateTime.UtcNow.Date, IQuotaTracker.VideosEndpoint, cancellationToken);

    public async Task<bool> IsSearchQuotaExhaustedAsync(CancellationToken cancellationToken = default)
    {
        var count = await GetSearchCallCountTodayAsync(cancellationToken);
        return count >= _options.MaxSearchCallsPerDay;
    }
}