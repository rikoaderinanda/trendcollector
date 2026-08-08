namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for the daily_api_usage table.
/// </summary>
public interface IQuotaRepository
{
    /// <summary>Returns the number of calls made to an endpoint on a given UTC date (0 when no row exists).</summary>
    Task<int> GetCallCountAsync(DateTime usageDate, string endpoint, CancellationToken cancellationToken = default);

    /// <summary>Atomically increments the call counter for an endpoint on a date (UPSERT).</summary>
    Task IncrementCallCountAsync(DateTime usageDate, string endpoint, CancellationToken cancellationToken = default);
}