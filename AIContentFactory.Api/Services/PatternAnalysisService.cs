using AIContentFactory.Api.Models.Analysis;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IPatternAnalysisService" />
public sealed class PatternAnalysisService : IPatternAnalysisService
{
    /// <summary>
    /// Detects winning patterns by comparing the top candidates against each other.
    /// A pattern is considered "winning" when at least 2 of the top videos share it
    /// and the average momentum of those videos is meaningful.
    /// </summary>
    public IReadOnlyList<WinningPattern> DetectWinningPatterns(
        IReadOnlyList<AnalysisCandidate> eligibleCandidates,
        long analysisRunId,
        int topN = 5)
    {
        if (eligibleCandidates.Count == 0)
        {
            return Array.Empty<WinningPattern>();
        }

        // Rank by momentum, not total views.
        var top = eligibleCandidates
            .Where(c => c.Performance is not null)
            .OrderByDescending(c => c.Performance!.MomentumScore)
            .Take(topN)
            .ToList();

        if (top.Count < 2)
        {
            return Array.Empty<WinningPattern>();
        }

        var patterns = new List<WinningPattern>();

        // ---- Hook patterns ----
        patterns.AddRange(DetectCategory(
            top,
            analysisRunId,
            "Hook",
            c => c.Knowledge?.Hook,
            c => ClassifyHook(c.Knowledge?.Hook)));

        // ---- Content structure patterns ----
        patterns.AddRange(DetectCategory(
            top,
            analysisRunId,
            "Structure",
            c => Join(c.Knowledge?.ContentStructure),
            c => ClassifyStructure(c.Knowledge?.ContentStructure)));

        // ---- Emotion patterns ----
        patterns.AddRange(DetectCategory(
            top,
            analysisRunId,
            "Emotion",
            c => c.Knowledge?.Emotion,
            c => c.Knowledge?.Emotion));

        // ---- Psychological trigger patterns ----
        patterns.AddRange(DetectMultiple(
            top,
            analysisRunId,
            "PsychologicalTrigger",
            c => c.Knowledge?.PsychologicalTriggers));

        // ---- Engagement technique patterns ----
        patterns.AddRange(DetectMultiple(
            top,
            analysisRunId,
            "Engagement",
            c => c.Knowledge?.EngagementTechniques));

        // ---- Story pattern ----
        patterns.AddRange(DetectCategory(
            top,
            analysisRunId,
            "StoryPattern",
            c => c.Knowledge?.StoryPattern,
            c => c.Knowledge?.StoryPattern));

        // ---- Content type ----
        patterns.AddRange(DetectCategory(
            top,
            analysisRunId,
            "ContentType",
            c => c.Knowledge?.ContentType,
            c => c.Knowledge?.ContentType));

        return patterns
            .OrderByDescending(p => p.SupportingVideoCount)
            .ThenByDescending(p => p.AverageMomentumScore)
            .ToList();
    }

    // ---------- Helpers ----------

    /// <summary>
    /// Detects a single-label pattern (hook, emotion, content type, story pattern).
    /// Groups videos by the classified value and counts frequency.
    /// </summary>
    private static List<WinningPattern> DetectCategory(
        List<AnalysisCandidate> top,
        long analysisRunId,
        string patternType,
        Func<AnalysisCandidate, string?> rawValue,
        Func<AnalysisCandidate, string?> classify)
    {
        var groups = top
            .Select(c => new { Candidate = c, Value = classify(c) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .GroupBy(x => Normalize(x.Value!))
            .ToList();

        var result = new List<WinningPattern>();
        foreach (var group in groups)
        {
            var candidates = group.Select(x => x.Candidate).ToList();
            if (candidates.Count < 2)
            {
                continue;
            }

            var avgMomentum = candidates.Average(c => c.Performance?.MomentumScore ?? 0m);
            var firstValue = group.First(x => !string.IsNullOrWhiteSpace(rawValue(x.Candidate)));

            result.Add(new WinningPattern
            {
                AnalysisRunId = analysisRunId,
                PatternType = patternType,
                PatternName = Normalize(firstValue.Value!),
                Description = BuildDescription(patternType, firstValue.Value!, candidates.Count, top.Count),
                Frequency = candidates.Count,
                SupportingVideoCount = candidates.Count,
                AverageMomentumScore = Math.Round(avgMomentum, 2),
                Evidence = BuildEvidence(patternType, firstValue.Value!, candidates)
            });
        }

        return result;
    }

    /// <summary>
    /// Detects multi-value patterns (psychological triggers, engagement techniques).
    /// Each candidate can contribute multiple values.
    /// </summary>
    private static List<WinningPattern> DetectMultiple(
        List<AnalysisCandidate> top,
        long analysisRunId,
        string patternType,
        Func<AnalysisCandidate, string[]?> values)
    {
        var valueToCandidates = new Dictionary<string, List<AnalysisCandidate>>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in top)
        {
            var valuesList = values(candidate) ?? Array.Empty<string>();
            foreach (var value in valuesList)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var normalized = Normalize(value);
                if (!valueToCandidates.TryGetValue(normalized, out var list))
                {
                    list = new List<AnalysisCandidate>();
                    valueToCandidates[normalized] = list;
                }

                if (!list.Contains(candidate))
                {
                    list.Add(candidate);
                }
            }
        }

        var result = new List<WinningPattern>();
        foreach (var (value, candidates) in valueToCandidates)
        {
            if (candidates.Count < 2)
            {
                continue;
            }

            var avgMomentum = candidates.Average(c => c.Performance?.MomentumScore ?? 0m);

            result.Add(new WinningPattern
            {
                AnalysisRunId = analysisRunId,
                PatternType = patternType,
                PatternName = value,
                Description = BuildDescription(patternType, value, candidates.Count, top.Count),
                Frequency = candidates.Count,
                SupportingVideoCount = candidates.Count,
                AverageMomentumScore = Math.Round(avgMomentum, 2),
                Evidence = BuildEvidence(patternType, value, candidates)
            });
        }

        return result;
    }

    // ---------- Classification helpers ----------

    private static string? ClassifyHook(string? hook)
    {
        if (string.IsNullOrWhiteSpace(hook))
        {
            return null;
        }

        var lowered = hook.ToLowerInvariant();

        if (lowered.Contains('?') && lowered.Length < 120)
        {
            return "Question";
        }

        if (lowered.Contains("never") || lowered.Contains("stop") || lowered.Contains("don't"))
        {
            return "Direct Challenge";
        }

        if (lowered.Contains("secret") || lowered.Contains("nobody") || lowered.Contains("reveal")
            || lowered.Contains("you won't believe") || lowered.Contains("what if"))
        {
            return "Curiosity Gap";
        }

        if (lowered.Contains("mistake") || lowered.Contains("wrong") || lowered.Contains("lying")
            || lowered.Contains("controvers"))
        {
            return "Controversial Statement";
        }

        if (lowered.Contains("problem") || lowered.Contains("struggle") || lowered.Contains("failing"))
        {
            return "Problem Statement";
        }

        if (lowered.Contains("story") || lowered.Contains("i remember") || lowered.Contains("one time"))
        {
            return "Story Opening";
        }

        return "Strong Claim";
    }

    private static string? ClassifyStructure(string[]? structure)
    {
        if (structure is null || structure.Length == 0)
        {
            return null;
        }

        var normalized = structure.Select(Normalize).ToList();
        var joined = string.Join(" → ", normalized);

        if (joined.Contains("Hook") && joined.Contains("Problem") &&
            (joined.Contains("Solution") || joined.Contains("Result")))
        {
            return "Hook → Problem → Solution";
        }

        if (joined.Contains("Hook") && joined.Contains("Story") &&
            (joined.Contains("Lesson") || joined.Contains("Result")))
        {
            return "Hook → Story → Lesson";
        }

        if (joined.Contains("Hook") && joined.Contains("Demonstration") && joined.Contains("Result"))
        {
            return "Hook → Demonstration → Result";
        }

        if (joined.Contains("Problem") && (joined.Contains("Mistake") || joined.Contains("Common Mistake")) &&
            joined.Contains("Solution"))
        {
            return "Problem → Mistake → Solution";
        }

        if (joined.Contains("Before") && joined.Contains("After"))
        {
            return "Before → After";
        }

        if (normalized.Count >= 4 && normalized.All(IsListItem))
        {
            return "List";
        }

        if (joined.Contains("Tutorial") || joined.Contains("Step"))
        {
            return "Tutorial";
        }

        if (joined.Contains("Story"))
        {
            return "Storytelling";
        }

        if (joined.Contains("Comparison") || joined.Contains("Vs"))
        {
            return "Comparison";
        }

        if (joined.Contains("Reaction"))
        {
            return "Reaction";
        }

        if (joined.Contains("Case Study") || joined.Contains("Case study"))
        {
            return "Case Study";
        }

        return joined.Length > 0 ? "Custom Structure" : null;
    }

    private static bool IsListItem(string value) =>
        int.TryParse(value, out _)
        || (value.Length <= 3 && value.Any(char.IsDigit))
        || value.StartsWith("Step", StringComparison.OrdinalIgnoreCase)
        || char.IsDigit(value[0]);

    // ---------- Text helpers ----------

    private static string Join(string[]? values)
        => values is { Length: > 0 } ? string.Join(" | ", values) : string.Empty;

    private static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string BuildDescription(string patternType, string value, int count, int total)
        => $"{count}/{total} top-performing videos use {value} in their {patternType.ToLowerInvariant()}.";

    private static string BuildEvidence(string patternType, string value, List<AnalysisCandidate> candidates)
    {
        var videoIds = string.Join(", ", candidates.Select(c => c.VideoId));
        var avgMomentum = candidates.Average(c => c.Performance?.MomentumScore ?? 0m);
        var titles = string.Join("; ", candidates.Take(3).Select(c => $"'{Truncate(c.Title, 60)}'"));
        return
            $"{candidates.Count} video(s) [{videoIds}] share the {patternType.ToLowerInvariant()} pattern '{value}'. " +
            $"Average momentum score: {avgMomentum:0.0}. Titles: {titles}.";
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";
}