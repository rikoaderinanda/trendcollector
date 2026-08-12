using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <summary>Agent 1 input validation: collected trending videos.</summary>
public sealed class Agent1VideoValidator : IInputValidator<TrendingVideo>
{
    public DataQualityResult Validate(TrendingVideo video)
    {
        var result = DataQualityResult.Valid();

        if (string.IsNullOrWhiteSpace(video.PlatformVideoId))
            return DataQualityResult.Invalid("PlatformVideoId is required.");

        if (string.IsNullOrWhiteSpace(video.Title))
            result = DataQualityResult.Incomplete("Title is missing.").WithReason("Optional metadata may be unavailable.");

        if (!video.PublishedAt.HasValue)
            result = result.IsValid ? DataQualityResult.Incomplete("PublishedAt is missing.") : result.WithReason("PublishedAt is missing.");

        return result;
    }
}