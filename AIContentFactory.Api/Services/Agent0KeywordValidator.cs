using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <summary>Agent 0 input validation: trend keywords.</summary>
public sealed class Agent0KeywordValidator : IInputValidator<TrendKeyword>
{
    public DataQualityResult Validate(TrendKeyword keyword)
    {
        var result = DataQualityResult.Valid();

        if (string.IsNullOrWhiteSpace(keyword.Keyword))
            return DataQualityResult.Invalid("Keyword is required and cannot be empty.");

        if (keyword.Priority is < 1 or > 100)
            result = DataQualityResult.Incomplete("Priority out of range 1-100.");

        return result;
    }
}