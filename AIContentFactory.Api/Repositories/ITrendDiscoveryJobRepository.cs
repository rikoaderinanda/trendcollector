using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for trend discovery jobs.
/// </summary>
public interface ITrendDiscoveryJobRepository
{
    /// <summary>Creates a new discovery job in "running" state and returns its id.</summary>
    Task<long> CreateAsync(TrendDiscoveryJob job, CancellationToken cancellationToken = default);

    /// <summary>Marks a job as completed with the total keyword count.</summary>
    Task CompleteAsync(long id, int totalKeywords, CancellationToken cancellationToken = default);

    /// <summary>Marks a job as failed with an error message.</summary>
    Task FailAsync(long id, string error, CancellationToken cancellationToken = default);

    /// <summary>Lists jobs ordered by start time, newest first, optionally filtered by the calendar date of started_at.</summary>
    Task<IEnumerable<TrendDiscoveryJob>> ListAsync(DateTime? date, int limit, int offset, CancellationToken cancellationToken = default);
}