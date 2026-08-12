using AIContentFactory.Api.Models.Dtos;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Orchestrates the full Viral Analyzer pipeline.
/// </summary>
public interface IViralAnalysisService
{
    /// <summary>
    /// Runs a complete viral analysis and returns the analysis run id.
    /// </summary>
    Task<long> RunAsync(RunViralAnalysisRequest request, CancellationToken cancellationToken = default);
}