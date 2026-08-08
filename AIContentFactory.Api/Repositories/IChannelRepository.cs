using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for channels.
/// </summary>
public interface IChannelRepository
{
    /// <summary>Inserts a channel or updates it if it already exists on the same platform.</summary>
    Task<long> UpsertAsync(Channel channel, CancellationToken cancellationToken = default);
}