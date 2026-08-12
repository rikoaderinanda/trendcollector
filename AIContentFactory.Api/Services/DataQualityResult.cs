namespace AIContentFactory.Api.Services;

/// <summary>
/// Standardized wrapper for data validation results across all agents.
/// Indicates whether data is Valid, Incomplete (optional data missing),
/// or Invalid (required data missing/broken), with a list of reasons.
/// </summary>
public sealed class DataQualityResult
{
    public DataQualityState State { get; set; } = DataQualityState.Valid;
    public List<string> Reasons { get; set; } = new();
    public bool IsValid => State == DataQualityState.Valid;
    public bool IsIncomplete => State == DataQualityState.Incomplete;
    public bool IsInvalid => State == DataQualityState.Invalid;

    public static DataQualityResult Valid()
        => new() { State = DataQualityState.Valid };

    public static DataQualityResult Incomplete(string reason)
        => new() { State = DataQualityState.Incomplete, Reasons = new List<string> { reason } };

    public static DataQualityResult Invalid(string reason)
        => new() { State = DataQualityState.Invalid, Reasons = new List<string> { reason } };

    public DataQualityResult WithReason(string reason)
    {
        Reasons.Add(reason);
        return this;
    }
}

public enum DataQualityState { Valid, Incomplete, Invalid }