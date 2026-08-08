using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for raw (never-discarded) AI responses.
/// </summary>
public interface IVideoKnowledgeRawRepository
{
    /// <summary>Stores a raw AI response.</summary>
    Task InsertAsync(VideoKnowledgeRaw raw, CancellationToken cancellationToken = default);
}