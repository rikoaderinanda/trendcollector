namespace AIContentFactory.Api.Services;

/// <summary>
/// Shared exponential-backoff calculator extracted from Agent 2's
/// QueueService. Used by all agents to compute unified retry delays
/// with jitter.
/// </summary>
public sealed class RetryCalculator
{
    private readonly RetryPolicy _policy;

    public RetryCalculator(RetryPolicy policy)
    {
        _policy = policy;
    }

    public TimeSpan Calculate(int currentRetryCount)
    {
        var baseSeconds = Math.Max(1, _policy.InitialDelaySeconds);
        var maxSeconds = Math.Max(baseSeconds, _policy.MaxDelaySeconds);
        var exponential = Math.Pow(_policy.BackoffMultiplier, currentRetryCount) * baseSeconds;
        var capped = Math.Min(exponential, maxSeconds);
        var jittered = capped * (0.75 + Random.Shared.NextDouble() * 0.5);
        return TimeSpan.FromSeconds(jittered);
    }

    public DateTimeOffset NextRetryAt(int currentRetryCount)
        => DateTimeOffset.UtcNow.Add(Calculate(currentRetryCount));

    public bool ShouldRetry(int currentRetryCount)
        => currentRetryCount < _policy.MaxRetryAttempts;
}

/// <summary>Configurable retry policy for all agents.</summary>
public sealed class RetryPolicy
{
    public int MaxRetryAttempts { get; set; } = 5;
    public double InitialDelaySeconds { get; set; } = 1;
    public double MaxDelaySeconds { get; set; } = 600;
    public double BackoffMultiplier { get; set; } = 2.0;

    public static RetryPolicy Default => new();
}