namespace AIContentFactory.Api.Services;

/// <summary>
/// Domain-specific input validation for any agent.
/// Each agent implements its own validation rules.
/// </summary>
public interface IInputValidator<T>
{
    DataQualityResult Validate(T input);
}

/// <summary>
/// Domain-specific output validation for any agent.
/// Each agent implements its own validation rules.
/// </summary>
public interface IOutputValidator<T>
{
    DataQualityResult Validate(T output);
}