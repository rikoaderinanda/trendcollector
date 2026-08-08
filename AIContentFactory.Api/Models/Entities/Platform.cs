namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// A social platform (youtube, tiktok, instagram, facebook, reddit, x).
/// </summary>
public sealed class Platform
{
    public int Id { get; set; }

    /// <summary>Platform code, e.g. "youtube".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name, e.g. "YouTube".</summary>
    public string Name { get; set; } = string.Empty;
}