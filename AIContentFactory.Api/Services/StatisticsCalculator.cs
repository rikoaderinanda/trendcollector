using Microsoft.Extensions.Options;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Calculates derived engagement metrics for a video statistics snapshot.
/// </summary>
public sealed class StatisticsCalculator
{
    private readonly TrackingModeOptions _trackingOptions;

    public StatisticsCalculator(IOptions<TrackingModeOptions> trackingOptions)
    {
        _trackingOptions = trackingOptions.Value;
    }

    /// <summary>
    /// Builds a <see cref="VideoStatistics"/> snapshot and computes the derived metrics.
    /// A views value of zero yields zero ratios. Missing likes/comments are treated as zero for calculations.
    /// </summary>
    public VideoStatistics Calculate(
        long videoId,
        long? views,
        long? likes,
        long? comments,
        long? favorites,
        DateTimeOffset? publishedAt,
        DateTimeOffset capturedAt)
    {
        var ageDays = CalculateAgeDays(publishedAt, capturedAt);
        var safeViews = views ?? 0;

        decimal? engagementRate = null;
        decimal? likeRatio = null;
        decimal? commentRatio = null;
        decimal? viewPerDay = null;

        if (safeViews > 0)
        {
            var safeLikes = likes ?? 0;
            var safeComments = comments ?? 0;

            engagementRate = Math.Round((safeLikes + safeComments) / (decimal)safeViews * 100m, 4);
            likeRatio = Math.Round(safeLikes / (decimal)safeViews * 100m, 4);
            commentRatio = Math.Round(safeComments / (decimal)safeViews * 100m, 4);
            viewPerDay = Math.Round(safeViews / (decimal)ageDays, 4);
        }

        return new VideoStatistics
        {
            VideoId = videoId,
            Views = views,
            Likes = likes,
            Comments = comments,
            Favorites = favorites,
            EngagementRate = engagementRate,
            LikeRatio = likeRatio,
            CommentRatio = commentRatio,
            ViewPerDay = viewPerDay,
            VideoAgeDays = ageDays,
            CapturedAt = capturedAt
        };
    }

    /// <summary>
    /// Computes velocity metrics (per-hour growth) by comparing the current snapshot
    /// against the previous snapshot of the same video.
    /// Returns a zero-velocity object when there is no previous snapshot
    /// (first capture) or when the timestamps are not strictly increasing.
    /// </summary>
    public VideoVelocity CalculateVelocity(VideoStatistics current, VideoStatistics? previous)
    {
        // First snapshot: no baseline to compare against. Return zeroed velocity
        // with an explicit IsFirstSnapshot flag so callers can surface
        // "no velocity data yet" instead of implying unknown (null) values.
        if (previous is null)
        {
            return new VideoVelocity
            {
                IsFirstSnapshot = true,
                ViewsPerHour = 0m,
                LikeVelocity = 0m,
                CommentVelocity = 0m,
                GrowthScore = 0m
            };
        }

        var viewsPerHour = CalculatePerHourVelocity(
            current.Views, previous.Views,
            current.CapturedAt, previous.CapturedAt);

        var likeVelocity = CalculatePerHourVelocity(
            current.Likes, previous.Likes,
            current.CapturedAt, previous.CapturedAt);

        var commentVelocity = CalculatePerHourVelocity(
            current.Comments, previous.Comments,
            current.CapturedAt, previous.CapturedAt);

        var growthScore = CalculateGrowthScore(viewsPerHour, likeVelocity, commentVelocity);

        return new VideoVelocity
        {
            ViewsPerHour = viewsPerHour,
            LikeVelocity = likeVelocity,
            CommentVelocity = commentVelocity,
            GrowthScore = growthScore
        };
    }

    private static decimal? CalculatePerHourVelocity(
        long? currentValue,
        long? previousValue,
        DateTimeOffset currentCapturedAt,
        DateTimeOffset previousCapturedAt)
    {
        if (currentValue is null || previousValue is null)
        {
            return null;
        }

        var hours = (currentCapturedAt - previousCapturedAt).TotalHours;
        if (hours <= 0)
        {
            return null;
        }

        var delta = currentValue.Value - previousValue.Value;
        if (delta <= 0)
        {
            return 0m;
        }

        return Math.Round(delta / (decimal)hours, 4);
    }

    /// <summary>
    /// Composite 0-100 growth score.
    /// Normalizes the raw per-hour velocities by dividing each by a
    /// saturation constant, clamps to 1, then applies the configured weights.
    /// </summary>
    private decimal? CalculateGrowthScore(
        decimal? viewsPerHour,
        decimal? likeVelocity,
        decimal? commentVelocity)
    {
        if (viewsPerHour is null && likeVelocity is null && commentVelocity is null)
        {
            return null;
        }

        const decimal viewsSaturation = 50_000m; // 50K views/hour => full score
        const decimal likeSaturation = 5_000m; // 5K likes/hour  => full score
        const decimal commentSaturation = 1_000m; // 1K comments/hour => full score

        var normalizedViews = Normalize(viewsPerHour, viewsSaturation);
        var normalizedLikes = Normalize(likeVelocity, likeSaturation);
        var normalizedComments = Normalize(commentVelocity, commentSaturation);

        var weightSum = _trackingOptions.ViewsVelocityWeight
                        + _trackingOptions.LikeVelocityWeight
                        + _trackingOptions.CommentVelocityWeight;

        if (weightSum <= 0)
        {
            return null;
        }

        var score = (normalizedViews * _trackingOptions.ViewsVelocityWeight
                     + normalizedLikes * _trackingOptions.LikeVelocityWeight
                     + normalizedComments * _trackingOptions.CommentVelocityWeight)
                    / weightSum;

        return Math.Round(Math.Clamp(score * 100m, 0m, 100m), 2);
    }

    private static decimal Normalize(decimal? value, decimal saturation)
    {
        if (value is null or <= 0)
        {
            return 0m;
        }

        return Math.Min(value.Value / saturation, 1m);
    }

    /// <summary>Days between published and captured, minimum 1.</summary>
    private static int CalculateAgeDays(DateTimeOffset? publishedAt, DateTimeOffset capturedAt)
    {
        if (publishedAt is null)
        {
            return 1;
        }

        var age = (capturedAt - publishedAt.Value).TotalDays;
        if (age <= 0)
        {
            return 1;
        }

        // Ceiling so a 1.2-day-old video counts as 2 days, never 0 or below.
        return (int)Math.Ceiling(age);
    }
}