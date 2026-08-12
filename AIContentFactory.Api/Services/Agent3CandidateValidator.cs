using AIContentFactory.Api.Models.Analysis;

namespace AIContentFactory.Api.Services;

/// <summary>Agent 3 input validation: analysis candidates.</summary>
public sealed class Agent3CandidateValidator : IInputValidator<AnalysisCandidate>
{
    public DataQualityResult Validate(AnalysisCandidate candidate)
    {
        var result = DataQualityResult.Valid();

        if (candidate.VideoId <= 0)
            return DataQualityResult.Invalid("VideoId is required.");

        if (candidate.Statistics is null)
            return DataQualityResult.Incomplete("Statistics snapshot is missing.");

        if (candidate.Performance is null)
            return DataQualityResult.Incomplete("Performance metrics are missing.");

        if (candidate.Knowledge is null)
            return DataQualityResult.Incomplete("Video knowledge (Agent 2) is missing.");

        if (candidate.Transcript is null && string.IsNullOrWhiteSpace(candidate.Knowledge?.Summary))
            return DataQualityResult.Incomplete("No transcript and no valid knowledge summary available.");

        return result;
    }
}