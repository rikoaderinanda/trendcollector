using AIContentFactory.Api.Models.Analysis;

namespace AIContentFactory.Api.Services;

public interface ITrendClassifier
{
    TrendClassification Classify(IReadOnlyList<AnalysisCandidate> candidates);
}

public sealed class TrendClassification
{
    public string Label { get; set; } = "Emerging";
    public string Explanation { get; set; } = string.Empty;
    public decimal AverageMomentum { get; set; }
}