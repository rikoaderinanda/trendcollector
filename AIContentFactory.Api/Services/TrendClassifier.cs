using AIContentFactory.Api.Models.Analysis;

namespace AIContentFactory.Api.Services;

public sealed class TrendClassifier : ITrendClassifier
{
    public TrendClassification Classify(IReadOnlyList<AnalysisCandidate> candidates)
    {
        var result = new TrendClassification();
        if (candidates.Count == 0) return result;

        var top = candidates
            .Where(c => c.Performance is not null)
            .OrderByDescending(c => c.Performance!.MomentumScore)
            .Take(5).ToList();

        if (top.Count == 0) return result;

        result.AverageMomentum = Math.Round(top.Average(c => c.Performance!.MomentumScore), 2);
        var avgVph = top.Average(c => c.Performance!.ViewsPerHour ?? 0m);
        var avgAge = top.Average(c => c.Performance!.VideoAgeDays ?? 1);

        // Classification logic based on momentum trajectory.
        if (result.AverageMomentum >= 75m)
            result.Label = "Established";
        else if (result.AverageMomentum >= 30m && avgVph > 100m)
            result.Label = "Emerging";
        else if (result.AverageMomentum > 0m && avgAge > 14 && avgVph < 10m)
            result.Label = "Declining";
        else
            result.Label = "PotentialEmergingOpportunity";

        result.Explanation = BuildExplanation(result.Label, result.AverageMomentum, avgVph, top.Count);
        return result;
    }

    private static string BuildExplanation(string label, decimal momentum, decimal vph, int count)
        => label switch
        {
            "Established" => $"Strong sustained momentum ({momentum:0.0}/100) across {count} top videos. " +
                             $"High view velocity indicates stable, proven interest.",
            "Emerging" => $"Rising momentum ({momentum:0.0}/100) with {vph:0.0} views/hour. " +
                          $"Topic is gaining traction rapidly.",
            "Declining" => $"Fading momentum ({momentum:0.0}/100) with low view velocity. " +
                           "Topic peaked and is now declining.",
            _ => $"Early-stage signal ({momentum:0.0}/100). " +
                 "Evidence is still building — high-risk, high-reward opportunity."
        };
}