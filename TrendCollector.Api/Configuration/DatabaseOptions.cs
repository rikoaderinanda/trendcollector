namespace TrendCollector.Api.Configuration;

/// <summary>
/// Options for the PostgreSQL database connection.
/// Bound from the "ConnectionStrings" configuration section.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    /// <summary>
    /// The Npgsql connection string for the PostgreSQL database.
    /// </summary>
    public string Postgres { get; set; } = string.Empty;
}
