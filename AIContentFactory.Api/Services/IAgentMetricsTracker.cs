namespace AIContentFactory.Api.Services;

/// <summary>
/// Lightweight per-agent metrics tracking for observability.
/// Persisted to the data_processing_failures table indirectly
/// via structured logging + failure counts.
/// </summary>
public interface IAgentMetricsTracker
{
    /// <summary>Records a successful processing event.</summary>
    void RecordSuccess(string agentName, string operation, long entityId, long durationMs);

    /// <summary>Records a processing failure.</summary>
    void RecordFailure(string agentName, string operation, long entityId, string failureType, string reason);
}

/// <summary>Snapshot of agent metrics for monitoring dashboards.</summary>
public sealed class AgentMetricsSnapshot
{
    public string AgentName { get; set; } = string.Empty;
    public long TotalProcessed { get; set; }
    public long Successful { get; set; }
    public long Failed { get; set; }
    public long Retryable { get; set; }
    public long PermanentFailed { get; set; }
    public long Incomplete { get; set; }
    public long Recovered { get; set; }
    public long Quarantined { get; set; }
    public double AverageDurationMs { get; set; }
    public double SuccessRate => TotalProcessed > 0 ? Math.Round((double)Successful / TotalProcessed * 100, 1) : 0;
}