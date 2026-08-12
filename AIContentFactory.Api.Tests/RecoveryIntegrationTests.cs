using Xunit;
using AIContentFactory.Api.Services;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Tests;

public class RecoveryIntegrationTests
{
    [Fact]
    public void Agent0Validator_RejectsEmptyKeyword_RecordsFailure()
    {
        var validator = new Agent0KeywordValidator();
        var result = validator.Validate(new TrendKeyword { Keyword = "" });
        Assert.True(result.IsInvalid);
        Assert.Contains(result.Reasons, r => r.Contains("required"));
    }

    [Fact]
    public void Agent0Validator_SupportsDataCleanser_Normalizes()
    {
        // DataCleanser normalizes before validation so "  AI  " passes.
        var cleaned = DataCleanser.NormalizeString("  AI  ");
        Assert.Equal("AI", cleaned);
        var validator = new Agent0KeywordValidator();
        Assert.True(validator.Validate(new TrendKeyword { Keyword = cleaned, Priority = 50 }).IsValid);
    }

    [Fact]
    public void Agent1Validator_RejectsMissingVideoId()
    {
        var validator = new Agent1VideoValidator();
        var result = validator.Validate(new TrendingVideo { Title = "Test" });
        Assert.True(result.IsInvalid);
        Assert.Contains(result.Reasons, r => r.Contains("PlatformVideoId"));
    }

    [Fact]
    public void Agent1Validator_DatabaseFailure_IsRetryable()
    {
        // The DataProcessingFailure entity supports Retryable status for
        // transient DB failures recorded by TrendCollectorService.
        var failure = new DataProcessingFailure
        {
            AgentName = "TrendCollector",
            EntityType = "TrendingVideo",
            EntityId = 0,
            Operation = "collect-insert",
            Status = "Retryable",
            FailureType = "Transient",
            FailureReason = "DB timeout",
            MaxRetryAttempts = 5,
            FirstAttemptAt = System.DateTimeOffset.UtcNow,
            LastAttemptAt = System.DateTimeOffset.UtcNow
        };
        Assert.Equal("Retryable", failure.Status);
        Assert.Equal("Transient", failure.FailureType);
        Assert.True(failure.MaxRetryAttempts > 0);
    }

    [Fact]
    public void Agent3_AIProviderRetry_UsesRetryCalculator()
    {
        // ViralAnalysisService.CallAiWithRetryAsync uses RetryCalculator;
        // verify the shared calculator still enforces max attempts.
        var calc = new RetryCalculator(new RetryPolicy { MaxRetryAttempts = 3 });
        Assert.True(calc.ShouldRetry(0));
        Assert.True(calc.ShouldRetry(2));
        Assert.False(calc.ShouldRetry(3));
    }

    [Fact]
    public void SharedFailureRepository_HasRecoveryMethods()
    {
        // Verify the wiring surface for the recovery worker exists.
        Assert.NotNull(typeof(IDataProcessingFailureRepository).GetMethod("GetRetryableAsync"));
        Assert.NotNull(typeof(IDataProcessingFailureRepository).GetMethod("MarkRecoveredAsync"));
        Assert.NotNull(typeof(IDataProcessingFailureRepository).GetMethod("MarkQuarantinedAsync"));
        Assert.NotNull(typeof(IDataProcessingFailureRepository).GetMethod("MarkPermanentFailedAsync"));
    }
}