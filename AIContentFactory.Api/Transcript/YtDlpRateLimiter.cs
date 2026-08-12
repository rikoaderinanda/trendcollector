namespace AIContentFactory.Api.Transcript;

/// <summary>
/// Shared in-process rate limiter that serializes yt-dlp transcript
/// invocations and enforces a minimum interval between them.
///
/// This protects YouTube from bursty transcript requests: even with
/// BatchSize=1, a single yt-dlp invocation may fan out into many HTTP
/// requests (multiple languages × subtitle formats × internal retries),
/// so pacing at the yt-dlp invocation level is required.
///
/// Registered as a SINGLETON so all YtDlpTranscriptProvider instances
/// share the same gate.
/// </summary>
public sealed class YtDlpRateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minimumInterval;
    private DateTime _lastStartedAt = DateTime.MinValue;
    private readonly ILogger<YtDlpRateLimiter> _logger;

    public YtDlpRateLimiter(
        TimeSpan minimumInterval,
        ILogger<YtDlpRateLimiter> logger)
    {
        _minimumInterval = minimumInterval;
        _logger = logger;
    }

    /// <summary>
    /// Acquires the gate and waits until the minimum interval since the
    /// previous yt-dlp invocation has elapsed. Caller MUST call
    /// <see cref="Release"/> in a finally block.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Wait for the required gap since the last invocation.
            var elapsed = DateTime.UtcNow - _lastStartedAt;
            if (elapsed < _minimumInterval)
            {
                var delay = _minimumInterval - elapsed;
                _logger.LogInformation(
                    "yt-dlp transcript request throttled locally. Waiting {DelaySeconds:0}s.",
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }

            _lastStartedAt = DateTime.UtcNow;
        }
        catch
        {
            _gate.Release();
            throw;
        }

        return new GateReleaser(_gate);
    }

    private sealed class GateReleaser : IDisposable
    {
        private readonly SemaphoreSlim _gate;
        private bool _released;

        public GateReleaser(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            if (!_released)
            {
                _gate.Release();
                _released = true;
            }
        }
    }
}