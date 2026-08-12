using Xunit;
using AIContentFactory.Api.Transcript;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIContentFactory.Api.Tests;

public class YtDlpRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_PreventsConcurrentExecutions()
    {
        var limiter = new YtDlpRateLimiter(TimeSpan.FromMilliseconds(50), NullLogger<YtDlpRateLimiter>.Instance);
        var first = await limiter.AcquireAsync(CancellationToken.None);
        var secondStarted = false;
        var secondTask = Task.Run(async () =>
        {
            await limiter.AcquireAsync(CancellationToken.None);
            secondStarted = true;
        });

        // Give the second task time to try; it should be blocked.
        await Task.Delay(100);
        Assert.False(secondStarted);

        first.Dispose();
        await secondTask;
        Assert.True(secondStarted);
    }

    [Fact]
    public async Task AcquireAsync_EnforcesMinimumInterval()
    {
        var interval = TimeSpan.FromMilliseconds(200);
        var limiter = new YtDlpRateLimiter(interval, NullLogger<YtDlpRateLimiter>.Instance);

        var start = DateTime.UtcNow;
        using (await limiter.AcquireAsync(CancellationToken.None)) { }
        using (await limiter.AcquireAsync(CancellationToken.None)) { }
        var elapsed = DateTime.UtcNow - start;

        Assert.True(elapsed >= interval, $"Two invocations should be >= {interval}ms apart, got {elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task AcquireAsync_CancellationTokenCancelsWaiting()
    {
        var limiter = new YtDlpRateLimiter(TimeSpan.FromSeconds(60), NullLogger<YtDlpRateLimiter>.Instance);
        var first = await limiter.AcquireAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await limiter.AcquireAsync(cts.Token);
        });

        first.Dispose();
    }
}

public class TranscriptDeduplicationTests
{
    [Fact]
    public void RemoveConsecutiveDuplicatePhrases_RemovesAdjacentDuplicateNgram()
    {
        // Replicates the real stutter pattern seen in collected transcripts.
        var input = "Other than REITs, real estate investment " +
                    "Other than REITs, real estate investment " +
                    "trusts, my first passive real estate " +
                    "trusts, my first passive real estate";

        var result = YtDlpTranscriptProvider.RemoveConsecutiveDuplicatePhrases(input);

        // The duplicated phrases should be collapsed to a single occurrence,
        // preserving the original punctuation ("REITs," is kept as-is because
        // the algorithm normalizes case/punctuation only for comparison).
        Assert.Contains("Other than REITs, real estate investment trusts", result);
        Assert.DoesNotContain("investment Other than REITs", result,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveConsecutiveDuplicatePhrases_KeepsNormalText()
    {
        var input = "The quick brown fox jumps over the lazy dog and runs away";
        Assert.Equal(input, YtDlpTranscriptProvider.RemoveConsecutiveDuplicatePhrases(input));
    }

    [Fact]
    public void RemoveConsecutiveDuplicatePhrases_HandlesSingleWordDuplicates()
    {
        var input = "hello hello world";
        Assert.Equal("hello world", YtDlpTranscriptProvider.RemoveConsecutiveDuplicatePhrases(input));
    }

    [Fact]
    public void RemoveConsecutiveDuplicatePhrases_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, YtDlpTranscriptProvider.RemoveConsecutiveDuplicatePhrases(""));
        Assert.Equal(string.Empty, YtDlpTranscriptProvider.RemoveConsecutiveDuplicatePhrases(null!));
    }

    [Fact]
    public void NormalizeTranscriptText_AppliesDedup()
    {
        var vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:02.000\nOther than REITs real estate\n\n" +
                  "00:00:02.000 --> 00:00:04.000\nOther than REITs real estate investment\n\n" +
                  "00:00:04.000 --> 00:00:06.000\ntrusts my first passive\n\n";

        var result = YtDlpTranscriptProvider.NormalizeTranscriptText(vtt);
        Assert.DoesNotContain("estate Other than", result, StringComparison.OrdinalIgnoreCase);
    }
}

public class YtDlpTransientClassificationTests
{
    // 429 is detected via reflection on private IsTransientFailure since
    // these helpers are kept private in the provider. We exercise the
    // public path indirectly by examining what triggers TranscriptTransientException.
    private static bool IsTransient(string stderr)
        => (bool)typeof(YtDlpTranscriptProvider)
            .GetMethod("IsTransientFailure",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { stderr })!;

    private static bool IsQuota(string stderr)
        => (bool)typeof(YtDlpTranscriptProvider)
            .GetMethod("IsQuotaExhausted",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { stderr })!;

    [Theory]
    [InlineData("ERROR: Unable to download video subtitles for 'en': HTTP Error 429: Too Many Requests")]
    [InlineData("ERROR: Too Many Requests")]
    [InlineData("Request throttled. Try again later.")]
    public void RateLimitMessages_AreTransient(string message)
    {
        Assert.True(IsTransient(message));
        Assert.False(IsQuota(message));
    }

    [Theory]
    [InlineData("ERROR: quota exceeded for your project")]
    [InlineData("ERROR: daily limit exceeded")]
    [InlineData("ERROR: quota limit reached")]
    [InlineData("ERROR: daily quota has been exhausted")]
    [InlineData("ERROR: exceeded your quota")]
    public void QuotaExhaustedMessages_AreNotTransient(string message)
    {
        Assert.True(IsQuota(message));
        Assert.False(IsTransient(message));
    }

    [Theory]
    [InlineData("ERROR: HTTP Error 500: Internal Server Error")]
    [InlineData("ERROR: HTTP Error 503: Service Unavailable")]
    [InlineData("ERROR: Operation timed out")]
    public void ServerTimeoutMessages_AreTransient(string message)
    {
        Assert.True(IsTransient(message));
        Assert.False(IsQuota(message));
    }

    [Fact]
    public void TranscriptUnavailable_Messages_AreNotTransient()
    {
        // Video deleted / no captions available are NOT transient.
        Assert.False(IsTransient("ERROR: Video unavailable"));
        Assert.False(IsTransient("ERROR: This video contains no subtitles"));
        Assert.False(IsTransient("ERROR: Private video"));
    }

    [Fact]
    public void EmptyStderr_IsNotTransient()
    {
        Assert.False(IsTransient(string.Empty));
        Assert.False(IsTransient(null!));
    }
}