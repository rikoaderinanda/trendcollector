using System.Collections.Concurrent;

namespace AIContentFactory.Api.Services;

/// <summary>
/// In-memory metrics tracker for all agents.
/// Wraps structured logging with counters for dashboard consumption.
/// </summary>
public sealed class AgentMetricsTracker : IAgentMetricsTracker
{
    private readonly ConcurrentDictionary<string, AgentCounters> _counters = new();
    private readonly ILogger<AgentMetricsTracker> _logger;

    public AgentMetricsTracker(ILogger<AgentMetricsTracker>? logger = null) => _logger = logger;

    public void RecordSuccess(string agentName, string operation, long entityId, long durationMs)
    {
        var c = _counters.GetOrAdd(agentName, _ => new());
        Interlocked.Increment(ref c.TotalProcessed);
        Interlocked.Increment(ref c.Successful);
        _logger?.LogInformation("[Metrics] Agent={Agent} Op={Op} Entity={Id} Status=Success Duration={Ms}ms",
            agentName, operation, entityId, durationMs);
    }

    public void RecordFailure(string agentName, string operation, long entityId, string failureType, string reason)
    {
        var c = _counters.GetOrAdd(agentName, _ => new());
        Interlocked.Increment(ref c.TotalProcessed);
        if (failureType == "Transient") Interlocked.Increment(ref c.Retryable);
        else Interlocked.Increment(ref c.PermanentFailed);
        _logger?.LogWarning("[Metrics] Agent={Agent} Op={Op} Entity={Id} Status=Failed Type={Type} Reason={Reason}",
            agentName, operation, entityId, failureType, reason);
    }

    public AgentMetricsSnapshot GetSnapshot(string agentName)
    {
        var c = _counters.GetOrAdd(agentName, _ => new());
        return new AgentMetricsSnapshot
        {
            AgentName = agentName,
            TotalProcessed = c.TotalProcessed,
            Successful = c.Successful,
            Failed = c.PermanentFailed,
            Retryable = c.Retryable,
            PermanentFailed = c.PermanentFailed,
            Incomplete = c.Incomplete,
            Recovered = c.Recovered,
            Quarantined = c.Quarantined
        };
    }

    public IReadOnlyList<AgentMetricsSnapshot> GetAllSnapshots()
        => _counters.Keys.Select(k => GetSnapshot(k)).ToList();

    private sealed class AgentCounters
    {
        public long TotalProcessed, Successful, Retryable, PermanentFailed, Incomplete, Recovered, Quarantined;
    }
}