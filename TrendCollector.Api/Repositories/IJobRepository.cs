using TrendCollector.Api.Models.Entities;

namespace TrendCollector.Api.Repositories;

/// <summary>
/// Data access for collection jobs.
/// </summary>
public interface IJobRepository
{
    /// <summary>Creates a new collection job in "running" state and returns its id.</summary>
    Task<long> CreateAsync(CollectionJob job, CancellationToken cancellationToken = default);

    /// <summary>Marks a job as completed with the collected counters.</summary>
    Task CompleteAsync(long id, int totalCollected, int totalSaved, int totalSkipped, CancellationToken cancellationToken = default);

    /// <summary>Marks a job as failed with an error message.</summary>
    Task FailAsync(long id, string error, CancellationToken cancellationToken = default);

    /// <summary>Lists jobs ordered by start time, newest first.</summary>
    Task<IEnumerable<CollectionJob>> ListAsync(int limit, int offset, CancellationToken cancellationToken = default);
}