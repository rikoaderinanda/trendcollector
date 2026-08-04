namespace TrendCollector.Api.Models.Entities;

/// <summary>
/// Status values for <see cref="CollectionJob"/>.
/// </summary>
public static class CollectionJobStatus
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}