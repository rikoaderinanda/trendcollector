

namespace AIContentFactory.Api.Services;

/// <summary>
/// Shared data cleansing utilities for all agents.
/// Provides common normalization functions that do NOT alter semantic content
/// (transcripts, titles, AI-generated text). Only structural cleanup.
/// </summary>
public static class DataCleanser
{
    public static string? NormalizeString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string[]? NormalizeTags(string[]? tags)
        => tags?.Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var trimmed = url.Trim();
        return trimmed.EndsWith('/') ? trimmed[..^1] : trimmed;
    }

    public static int? NormalizeInt(int? value, int min = 0, int max = int.MaxValue)
        => value.HasValue ? Math.Clamp(value.Value, min, max) : null;

    public static long? NormalizeLong(long? value, long min = 0, long max = long.MaxValue)
        => value.HasValue ? Math.Clamp(value.Value, min, max) : null;

    public static decimal? NormalizeDecimal(decimal? value, decimal min = 0, decimal max = decimal.MaxValue)
        => value.HasValue ? Math.Clamp(value.Value, min, max) : null;

    /// <summary>Checks if a string contains only printable characters.</summary>
    public static bool IsSafeString(string? value)
        => value is null || value.All(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t');
}