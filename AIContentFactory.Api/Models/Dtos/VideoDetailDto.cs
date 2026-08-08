using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// A video with its most recent statistics snapshot.
/// </summary>
public sealed class VideoDetailDto
{
    public TrendingVideo Video { get; set; } = null!;

    public VideoStatistics? Statistics { get; set; }
}