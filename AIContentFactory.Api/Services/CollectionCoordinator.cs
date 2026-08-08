namespace AIContentFactory.Api.Services;

/// <summary>
/// Coordinates discovery and tracking passes so only one collect/track
/// operation runs at a time across background workers and manual triggers.
/// Registered as a singleton; both background services and the HTTP API
/// go through it to prevent race conditions.
/// </summary>
public sealed class CollectionCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Runs <paramref name="operation"/> while holding the collection gate.
    /// If another discovery/tracking operation is already running, waits
    /// up to <paramref name="timeout"/> for the gate to become available.
    /// </summary>
    public async Task<T> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(timeout, cancellationToken))
        {
            throw new InvalidOperationException(
                "Another collection/tracking operation is still running. Skipping this pass to avoid a race condition.");
        }

        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs an operation while holding the gate, returning the task directly
    /// (fire-and-forget safe). Uses the same lock semantics as
    /// <see cref="RunExclusiveAsync{T}"/>.
    /// </summary>
    public async Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(timeout, cancellationToken))
        {
            throw new InvalidOperationException(
                "Another collection/tracking operation is still running. Skipping this pass to avoid a race condition.");
        }

        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}