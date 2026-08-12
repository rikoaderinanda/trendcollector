using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Entities;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.Transcript;

/// <summary>
/// Orchestrates transcript retrieval across the primary HTTP-scraping provider
/// and the yt-dlp fallback provider.
///
/// <para>
/// The primary provider (<see cref="YouTubeTranscriptProvider"/>) scrapes the
/// watch page and timedtext endpoints directly. When it returns no transcript
/// (e.g. because YouTube now requires a proof-of-origin token for its
/// transcript endpoints), the composite falls back to
/// <see cref="YtDlpTranscriptProvider"/> which runs the yt-dlp executable and
/// handles the BotGuard challenge internally.
/// </para>
/// </summary>
public sealed class CompositeTranscriptProvider : ITranscriptProvider
{
    private readonly ITranscriptProvider _primary;
    private readonly ITranscriptProvider? _fallback;
    private readonly TranscriptOptions _options;
    private readonly ILogger<CompositeTranscriptProvider> _logger;

    public CompositeTranscriptProvider(
        ITranscriptProvider primary,
        ITranscriptProvider? fallback,
        IOptions<TranscriptOptions> options,
        ILogger<CompositeTranscriptProvider> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VideoTranscript?> GetTranscriptAsync(
        string platformVideoId,
        string? preferredLanguage = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Primary provider (HTTP scraping).
        var primaryResult = await _primary.GetTranscriptAsync(
            platformVideoId,
            preferredLanguage,
            cancellationToken);

        if (primaryResult is not null)
        {
            _logger.LogDebug(
                "Primary transcript provider succeeded for video {VideoId}.",
                platformVideoId);
            return primaryResult;
        }

        // 2. Fallback provider (yt-dlp), if enabled and registered.
        if (!_options.FallbackEnabled)
        {
            _logger.LogDebug(
                "Primary transcript provider returned nothing for video {VideoId} and " +
                "the yt-dlp fallback is disabled.",
                platformVideoId);
            return null;
        }

        if (_fallback is null)
        {
            _logger.LogDebug(
                "Primary transcript provider returned nothing for video {VideoId} and " +
                "no yt-dlp fallback provider is registered.",
                platformVideoId);
            return null;
        }

        _logger.LogInformation(
            "Primary transcript provider returned nothing for video {VideoId}; " +
            "attempting yt-dlp fallback.",
            platformVideoId);

        return await _fallback.GetTranscriptAsync(
            platformVideoId,
            preferredLanguage,
            cancellationToken);
    }
}