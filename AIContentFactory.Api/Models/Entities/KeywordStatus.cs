namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Status values for <see cref="TrendKeyword"/> lifecycle.
/// </summary>
public static class KeywordStatus
{
    /// <summary>Discovered and ready to be collected.</summary>
    public const string Active = "active";

    /// <summary>Already consumed by the Trend Collector.</summary>
    public const string Collected = "collected";

    /// <summary>Temporarily excluded from collection.</summary>
    public const string Paused = "paused";

    /// <summary>Collection attempt failed.</summary>
    public const string Failed = "failed";

    /// <summary>No longer relevant.</summary>
    public const string Archived = "archived";
}