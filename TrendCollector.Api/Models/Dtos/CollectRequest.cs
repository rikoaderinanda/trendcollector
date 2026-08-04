namespace TrendCollector.Api.Models.Dtos;

/// <summary>
/// Request body for starting a trend collection.
/// </summary>
public sealed class CollectRequest
{
    /// <summary>Search keyword, e.g. "AI".</summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>Language code, e.g. "id", "en".</summary>
    public string? Language { get; set; } = "id";

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "ID".</summary>
    public string? Country { get; set; } = "ID";

    /// <summary>Maximum number of search results (1-50).</summary>
    public int MaxResults { get; set; } = 20;
}