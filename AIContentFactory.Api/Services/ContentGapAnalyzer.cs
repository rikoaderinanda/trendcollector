using System.Text;
using AIContentFactory.Api.Models.Analysis;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IContentGapAnalyzer" />
public sealed class ContentGapAnalyzer : IContentGapAnalyzer
{
    /// <summary>
    /// Analyzes eligible candidates and detects content gaps based on:
    /// - Common problem words appearing in titles/hooks/summaries
    /// - Audience types mentioned in target_audience
    /// - Content formats already covered (vs missing formats)
    /// - Keywords appearing most frequently (potential underserved angles)
    /// </summary>
    public string AnalyzeGaps(IReadOnlyList<AnalysisCandidate> eligibleCandidates)
    {
        if (eligibleCandidates.Count == 0)
        {
            return "No eligible candidates available for gap analysis.";
        }

        var sb = new StringBuilder();

        // 1. Audience coverage
        var audiences = eligibleCandidates
            .Where(c => c.Knowledge?.TargetAudience is not null)
            .SelectMany(c => SplitValues(c.Knowledge!.TargetAudience!))
            .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (audiences.Count > 0)
        {
            sb.AppendLine("- Covered audiences: " +
                          string.Join(", ", audiences.Take(5).Select(g => $"{g.Key} ({g.Count()})")));
        }

        // 2. Format coverage
        var formats = eligibleCandidates
            .Where(c => c.Knowledge?.ContentType is not null)
            .GroupBy(c => c.Knowledge!.ContentType!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (formats.Count > 0)
        {
            sb.AppendLine("- Covered formats: " + string.Join(", ", formats.Select(g => $"{g.Key} ({g.Count()})")));
        }

        // 3. Problem words appearing in titles
        var problemWords = new[] { "mistake", "problem", "wrong", "fail", "error", "struggle", "stop", "never" };
        var problems = eligibleCandidates
            .Where(c => ContainsAny(c.Title, problemWords) ||
                        (c.Knowledge?.Summary is not null && ContainsAny(c.Knowledge!.Summary!, problemWords)))
            .Select(c => c.Title)
            .ToList();

        if (problems.Count >= 2)
        {
            sb.AppendLine($"- {problems.Count} videos address a common pain point (mistake/problem/wrong/fail). " +
                          "Strong signal: a 'quick fix' angle could differentiate.");
        }

        // 4. Missing angles (keywords appearing once or twice)
        var keywordFreq = eligibleCandidates
            .Where(c => c.Knowledge?.Keywords is not null)
            .SelectMany(c => c.Knowledge!.Keywords!)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .GroupBy(k => k.Trim().ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .ToList();

        var topKeywords = keywordFreq.Take(5).Select(g => g.Key).ToList();
        if (topKeywords.Count > 0)
        {
            sb.AppendLine("- Top keywords: " + string.Join(", ", topKeywords));
        }

        // 5. Underserved combinations
        var allTitles = string.Join(" ", eligibleCandidates.Select(c => c.Title)).ToLowerInvariant();
        var commonWords = new[] { "workflow", "beginner", "advanced", "complete", "simple", "step by step", "for x" };
        var missing = commonWords.Where(w => !allTitles.Contains(w, StringComparison.Ordinal)).ToList();

        if (missing.Count > 0)
        {
            sb.AppendLine("- Missing/common underserved angles: " + string.Join(", ", missing));
        }

        // 6. Short-form vs long-form opportunity
        var shortFormVideos = eligibleCandidates.Count(c =>
            c.Knowledge?.ContentType is not null &&
            (c.Knowledge.ContentType.Contains("Short", StringComparison.OrdinalIgnoreCase)
             || c.Knowledge.ContentType.Contains("Reel", StringComparison.OrdinalIgnoreCase)
             || c.Knowledge.ContentType.Contains("Shorts", StringComparison.OrdinalIgnoreCase)));
        if (shortFormVideos > 0 && shortFormVideos < eligibleCandidates.Count / 2)
        {
            sb.AppendLine($"- Only {shortFormVideos}/{eligibleCandidates.Count} use short-form formats. " +
                          "A concise 30-60 second short-form version of the top topic is a clear content gap.");
        }

        return sb.Length == 0
            ? "No significant content gaps detected from the available metadata."
            : sb.ToString();
    }

    // ---------- Helpers ----------

    private static IEnumerable<string> SplitValues(string value)
        => value.Split(',', ';', '|')
            .Select(v => v.Trim())
            .Where(v => v.Length > 0);

    private static bool ContainsAny(string text, IEnumerable<string> words)
    {
        var lowered = text.ToLowerInvariant();
        return words.Any(word => lowered.Contains(word, StringComparison.Ordinal));
    }
}