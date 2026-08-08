namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Status values for <see cref="TrendDiscoveryJob"/>.
/// </summary>
public static class TrendDiscoveryJobStatus
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}