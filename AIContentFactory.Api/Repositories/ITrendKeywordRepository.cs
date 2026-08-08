using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for trend keywords.
/// </summary>
public interface ITrendKeywordRepository
{
    /// <summary>
    /// Inserts a keyword, or updates priority/reason if it already exists
    /// (unique by keyword + country + language).
    /// </summary>
    Task<long> UpsertAsync(TrendKeyword keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists keywords with optional filters.
    /// </summary>
    Task<IEnumerable<TrendKeyword>> ListAsync(
        string? country,
        string? language,
        string? niche,
        int? minPriority,
        string? status,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether a keyword already exists for the given country/language.</summary>
    Task<bool> ExistsAsync(string keyword, string country, string language, CancellationToken cancellationToken = default);

    /// <summary>Updates the lifecycle status of a keyword (e.g. active → collected).</summary>
    Task UpdateStatusAsync(long id, string status, CancellationToken cancellationToken = default);
}

