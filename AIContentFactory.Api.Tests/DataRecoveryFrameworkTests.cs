using Xunit;
using AIContentFactory.Api.Services;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Models.Analysis;

namespace AIContentFactory.Api.Tests;

public class RetryCalculatorTests
{
    [Fact]
    public void Calculate_FirstAttempt_ReturnsShortDelay()
    {
        var calc = new RetryCalculator(RetryPolicy.Default);
        var delay = calc.Calculate(0);
        Assert.True(delay.TotalSeconds is >= 0.75 and <= 2.0,
            $"First retry delay {delay.TotalSeconds}s should be ~1s (±25% jitter)");
    }

    [Fact]
    public void Calculate_FourthAttempt_ReturnsLongerDelay()
    {
        var calc = new RetryCalculator(RetryPolicy.Default);
        var delay = calc.Calculate(3);
        Assert.True(delay.TotalSeconds >= 6.0,
            $"4th attempt delay {delay.TotalSeconds}s should be at least 6s (2^3 * 1s * 0.75 jitter)");
    }

    [Fact]
    public void Calculate_RespectsMaxDelay()
    {
        var policy = new RetryPolicy { MaxRetryAttempts = 10, MaxDelaySeconds = 60 };
        var calc = new RetryCalculator(policy);
        var delay = calc.Calculate(10);
        // Capped at 60s then jittered by up to +25%: max = 75s
        Assert.True(delay.TotalSeconds <= 76.0,
            $"Delay should be capped at max ({policy.MaxDelaySeconds}s) + 25% jitter, got {delay.TotalSeconds}s");
    }

    [Fact]
    public void ShouldRetry_UnderLimit_ReturnsTrue()
    {
        var calc = new RetryCalculator(new RetryPolicy { MaxRetryAttempts = 5 });
        Assert.True(calc.ShouldRetry(3));
    }

    [Fact]
    public void ShouldRetry_AtLimit_ReturnsFalse()
    {
        var calc = new RetryCalculator(new RetryPolicy { MaxRetryAttempts = 3 });
        Assert.False(calc.ShouldRetry(3));
    }

    [Fact]
    public void NextRetryAt_ReturnsFutureTimestamp()
    {
        var calc = new RetryCalculator(RetryPolicy.Default);
        var nextAt = calc.NextRetryAt(0);
        Assert.True(nextAt > DateTimeOffset.UtcNow);
    }
}

public class DataCleanserTests
{
    [Fact]
    public void NormalizeString_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Null(DataCleanser.NormalizeString(null));
        Assert.Null(DataCleanser.NormalizeString("   "));
    }

    [Fact]
    public void NormalizeString_Trims()
    {
        Assert.Equal("hello", DataCleanser.NormalizeString("  hello  "));
    }

    [Fact]
    public void NormalizeTags_DeduplicatesCaseInsensitive()
    {
        var input = new[] { " AI ", "ai", " Machine Learning ", "AI" };
        var result = DataCleanser.NormalizeTags(input);
        Assert.Equal(2, result!.Length);
        Assert.Contains("ai", result);
        Assert.Contains("machine learning", result);
    }

    [Fact]
    public void NormalizeUrl_RemovesTrailingSlash()
    {
        Assert.Equal("https://api.example.com", DataCleanser.NormalizeUrl("https://api.example.com/"));
        Assert.Equal("https://api.example.com", DataCleanser.NormalizeUrl("https://api.example.com"));
    }

    [Fact]
    public void NormalizeDecimal_ClampsRange()
    {
        Assert.Equal(50m, DataCleanser.NormalizeDecimal(50m, 0, 100));
        Assert.Equal(0m, DataCleanser.NormalizeDecimal(-10m, 0, 100));
        Assert.Equal(100m, DataCleanser.NormalizeDecimal(200m, 0, 100));
    }

    [Fact]
    public void IsSafeString_ControlChars_ReturnsFalse()
    {
        Assert.False(DataCleanser.IsSafeString("hello\u0000world"));
    }

    [Fact]
    public void IsSafeString_NormalText_ReturnsTrue()
    {
        Assert.True(DataCleanser.IsSafeString("hello\nworld"));
        Assert.True(DataCleanser.IsSafeString(null));
    }
}

public class DataQualityResultTests
{
    [Fact]
    public void Valid_ReturnsValidState()
    {
        var result = DataQualityResult.Valid();
        Assert.True(result.IsValid);
        Assert.False(result.IsIncomplete);
        Assert.False(result.IsInvalid);
    }

    [Fact]
    public void Incomplete_SetsCorrectState()
    {
        var result = DataQualityResult.Incomplete("Missing optional field");
        Assert.Equal(DataQualityState.Incomplete, result.State);
        Assert.Single(result.Reasons);
    }

    [Fact]
    public void Invalid_SetsCorrectState()
    {
        var result = DataQualityResult.Invalid("Required field missing");
        Assert.Equal(DataQualityState.Invalid, result.State);
    }

    [Fact]
    public void WithReason_ChainsMultipleReasons()
    {
        var result = DataQualityResult.Incomplete("Reason 1")
            .WithReason("Reason 2");
        Assert.Equal(2, result.Reasons.Count);
    }
}

public class Agent0ValidatorTests
{
    [Fact]
    public void ValidKeyword_Passes()
    {
        var v = new Agent0KeywordValidator();
        var result = v.Validate(new TrendKeyword { Keyword = "AI tools", Priority = 50 });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyKeyword_Fails()
    {
        var v = new Agent0KeywordValidator();
        var result = v.Validate(new TrendKeyword { Keyword = "" });
        Assert.True(result.IsInvalid);
    }

    [Fact]
    public void PriorityOutOfRange_MarkedIncomplete()
    {
        var v = new Agent0KeywordValidator();
        var result = v.Validate(new TrendKeyword { Keyword = "test", Priority = 200 });
        Assert.True(result.IsIncomplete);
    }
}

public class Agent1ValidatorTests
{
    [Fact]
    public void ValidVideo_Passes()
    {
        var v = new Agent1VideoValidator();
        var r = v.Validate(new TrendingVideo { PlatformVideoId = "abc123", Title = "Test", PublishedAt = DateTimeOffset.UtcNow });
        Assert.True(r.IsValid);
    }

    [Fact]
    public void MissingVideoId_Fails()
    {
        var v = new Agent1VideoValidator();
        var r = v.Validate(new TrendingVideo { Title = "Test" });
        Assert.True(r.IsInvalid);
    }

    [Fact]
    public void MissingTitle_MarkedIncomplete()
    {
        var v = new Agent1VideoValidator();
        var r = v.Validate(new TrendingVideo { PlatformVideoId = "abc" });
        Assert.True(r.IsIncomplete);
    }
}

public class Agent2ValidatorTests
{
    [Fact]
    public void FullKnowledge_Passes()
    {
        var v = new Agent2KnowledgeValidator();
        var r = v.Validate(new VideoKnowledge { Summary = "Test", MainTopic = "AI", Keywords = new[] { "ai" }, Hook = "What if?" });
        Assert.True(r.IsValid);
    }

    [Fact]
    public void EmptySummaryAndTopic_MarkedIncomplete()
    {
        var v = new Agent2KnowledgeValidator();
        var r = v.Validate(new VideoKnowledge());
        Assert.True(r.IsIncomplete);
    }
}

public class Agent3ValidatorTests
{
    [Fact]
    public void FullCandidate_Passes()
    {
        var v = new Agent3CandidateValidator();
        var c = new AnalysisCandidate
        {
            VideoId = 1,
            Statistics = new VideoStatistics(),
            Performance = new VideoPerformanceSummary(),
            Knowledge = new VideoKnowledge { Summary = "ok" },
            Transcript = "sample transcript"
        };
        Assert.True(v.Validate(c).IsValid);
    }

    [Fact]
    public void MissingKnowledge_MarkedIncomplete()
    {
        var v = new Agent3CandidateValidator();
        var c = new AnalysisCandidate { VideoId = 1, Statistics = new VideoStatistics(), Performance = new VideoPerformanceSummary() };
        Assert.True(v.Validate(c).IsIncomplete);
    }

    [Fact]
    public void NoTranscriptOrSummary_MarkedIncomplete()
    {
        var v = new Agent3CandidateValidator();
        var c = new AnalysisCandidate { VideoId = 1, Statistics = new VideoStatistics(), Performance = new VideoPerformanceSummary(), Knowledge = new VideoKnowledge() };
        Assert.True(v.Validate(c).IsIncomplete);
    }

    [Fact]
    public void ZeroVideoId_MarkedInvalid()
    {
        var v = new Agent3CandidateValidator();
        Assert.True(v.Validate(new AnalysisCandidate()).IsInvalid);
    }
}

public class AgentMetricsTrackerTests
{
    [Fact]
    public void RecordSuccess_IncrementsCounters()
    {
        var tracker = new AgentMetricsTracker(null!);
        tracker.RecordSuccess("TestAgent", "op", 1, 100);
        var snap = tracker.GetSnapshot("TestAgent");
        Assert.Equal(1, snap.TotalProcessed);
        Assert.Equal(1, snap.Successful);
    }

    [Fact]
    public void RecordFailure_Transient_IncrementsRetryable()
    {
        var tracker = new AgentMetricsTracker(null!);
        tracker.RecordFailure("Agent1", "collect", 42, "Transient", "Timeout");
        var snap = tracker.GetSnapshot("Agent1");
        Assert.Equal(1, snap.Retryable);
        Assert.Equal(0, snap.PermanentFailed);
    }

    [Fact]
    public void RecordFailure_Permanent_IncrementsPermanentFailed()
    {
        var tracker = new AgentMetricsTracker(null!);
        tracker.RecordFailure("Agent2", "extract", 10, "Permanent", "Invalid ID");
        var snap = tracker.GetSnapshot("Agent2");
        Assert.Equal(1, snap.PermanentFailed);
    }

    [Fact]
    public void SuccessRate_CalculatesCorrectly()
    {
        var tracker = new AgentMetricsTracker(null!);
        tracker.RecordSuccess("X", "op", 1, 10);
        tracker.RecordSuccess("X", "op", 2, 20);
        tracker.RecordFailure("X", "op", 3, "Transient", "err");
        var snap = tracker.GetSnapshot("X");
        Assert.Equal(3, snap.TotalProcessed);
        Assert.Equal(2, snap.Successful);
        Assert.Equal(66.7, snap.SuccessRate);
    }
}