using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for platforms.
/// </summary>
public interface IPlatformRepository
{
    /// <summary>Gets the platform id by code, inserting the platform if it does not exist yet.</summary>
    Task<int> GetOrCreateAsync(string code, CancellationToken cancellationToken = default);
}