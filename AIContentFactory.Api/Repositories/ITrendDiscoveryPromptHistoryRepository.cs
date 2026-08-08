using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for AI prompt history.
/// </summary>
public interface ITrendDiscoveryPromptHistoryRepository
{
    /// <summary>Creates a record of a prompt + its raw AI response.</summary>
    Task<long> CreateAsync(TrendDiscoveryPromptHistory history, CancellationToken cancellationToken = default);
}