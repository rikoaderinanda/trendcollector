using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <summary>Agent 2 output validation: extracted video knowledge.</summary>
public sealed class Agent2KnowledgeValidator : IOutputValidator<VideoKnowledge>
{
    public DataQualityResult Validate(VideoKnowledge knowledge)
    {
        var result = DataQualityResult.Valid();

        if (string.IsNullOrWhiteSpace(knowledge.Summary) && string.IsNullOrWhiteSpace(knowledge.MainTopic))
            result = DataQualityResult.Incomplete("Summary and MainTopic are both missing.");

        if (knowledge.Keywords is null || knowledge.Keywords.Length == 0)
            result = result.IsValid ? DataQualityResult.Incomplete("Keywords are missing.") : result.WithReason("Keywords are missing.");

        if (string.IsNullOrWhiteSpace(knowledge.Hook))
            result = result.IsValid ? DataQualityResult.Incomplete("Hook is missing.") : result.WithReason("Hook is missing.");

        return result;
    }
}