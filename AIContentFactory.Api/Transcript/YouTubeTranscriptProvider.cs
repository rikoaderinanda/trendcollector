using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Transcript;

/// <summary>
/// Retrieves YouTube captions using YouTube's public watch page and
/// timedtext caption endpoints. Does NOT download the video.
/// </summary>
public sealed partial class YouTubeTranscriptProvider : ITranscriptProvider
{
    private const string WatchUrl = "https://www.youtube.com/watch?v={0}";
    private const string TranscriptSource = "youtube_captions";

    private readonly HttpClient _httpClient;
    private readonly ILogger<YouTubeTranscriptProvider> _logger;

    public YouTubeTranscriptProvider(
        HttpClient httpClient,
        ILogger<YouTubeTranscriptProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<VideoTranscript?> GetTranscriptAsync(
        string platformVideoId,
        string? preferredLanguage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var captionTrack = await GetBestCaptionTrackAsync(platformVideoId, preferredLanguage, cancellationToken);
            if (captionTrack is null)
            {
                _logger.LogWarning("No caption track found for video {VideoId}.", platformVideoId);
                return null;
            }

            var xml = await _httpClient.GetStringAsync(captionTrack.BaseUrl, cancellationToken);
            var transcriptText = ParseCaptionXml(xml);

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                _logger.LogWarning("Caption track for video {VideoId} contained no text.", platformVideoId);
                return null;
            }

            _logger.LogInformation(
                "Transcript loaded for video {VideoId}: {Length} characters, language '{Language}'.",
                platformVideoId, transcriptText.Length, captionTrack.Language);

            return new VideoTranscript
            {
                Transcript = transcriptText,
                Language = captionTrack.Language,
                Source = TranscriptSource
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve transcript for video {VideoId}.", platformVideoId);
            return null;
        }
    }

    // ---------- Caption track discovery ----------

    private async Task<CaptionTrack?> GetBestCaptionTrackAsync(
        string videoId,
        string? preferredLanguage,
        CancellationToken cancellationToken)
    {
        var pageHtml = await GetWatchPageHtmlAsync(videoId, cancellationToken);
        
        var playerResponse = ExtractPlayerResponseJson(pageHtml);
        if (playerResponse is null)
        {
            _logger.LogWarning("Unable to extract player response for video {VideoId}.", videoId);
            return null;
        }

        var captionTracks = ParseCaptionTracks(playerResponse);
        if (captionTracks.Count == 0)
        {
            return null;
        }

        // Prefer the requested language, then fall back to the first track.
        if (!string.IsNullOrEmpty(preferredLanguage))
        {
            var preferred = captionTracks.FirstOrDefault(t =>
                string.Equals(t.LanguageCode, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return captionTracks[0];
    }

    private async Task<string> GetWatchPageHtmlAsync(string videoId, CancellationToken cancellationToken)
    {
        var url = string.Format(WatchUrl, videoId);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        // When captions are not available for a video, YouTube (as of 2024)
        // returns a "playabilityStatus" stating the transcript is unavailable.
        return html;
    }

    private static string? ExtractPlayerResponseJson(string pageHtml)
    {
        var match = PlayerResponseRegex().Match(pageHtml);
        if (!match.Success)
        {
            return null;
        }

        var json = match.Groups[1].Value;
        // Unescape common HTML entities that can appear inside the JSON.
        json = json.Replace("\\u0026", "&")
                   .Replace("\\/", "/");

        return json;
    }

    private static List<CaptionTrack> ParseCaptionTracks(string playerResponseJson)
    {
        var tracks = new List<CaptionTrack>();

        using var document = System.Text.Json.JsonDocument.Parse(playerResponseJson);

        if (!document.RootElement.TryGetProperty("captions", out var captions) ||
            !captions.TryGetProperty("playerCaptionsTracklistRenderer", out var renderer) ||
            !renderer.TryGetProperty("captionTracks", out var captionTracksElement) ||
            captionTracksElement.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return tracks;
        }

        foreach (var trackElement in captionTracksElement.EnumerateArray())
        {
            var baseUrl = trackElement.TryGetProperty("baseUrl", out var baseUrlElement)
                ? baseUrlElement.GetString()
                : null;

            var languageCode = trackElement.TryGetProperty("languageCode", out var codeElement)
                ? codeElement.GetString()
                : null;

            var name = trackElement.TryGetProperty("name", out var nameElement) &&
                       nameElement.TryGetProperty("simpleText", out var simpleTextElement)
                ? simpleTextElement.GetString()
                : null;

            var isTranslatable = trackElement.TryGetProperty("isTranslatable", out var translatableElement) &&
                                 translatableElement.GetBoolean();

            if (!string.IsNullOrEmpty(baseUrl))
            {
                tracks.Add(new CaptionTrack
                {
                    BaseUrl = baseUrl,
                    LanguageCode = languageCode ?? "unknown",
                    Language = name ?? languageCode ?? "unknown",
                    IsTranslatable = isTranslatable
                });
            }
        }

        return tracks;
    }

    // ---------- XML parsing ----------

    private static string ParseCaptionXml(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var texts = document.Descendants("text")
                .Select(e => e.Value.Trim())
                .Where(v => !string.IsNullOrEmpty(v));

            return string.Join(" ", texts);
        }
        catch (Exception)
        {
            // Fall back to a regex parse when the XML is malformed.
            var matches = TextRegex().Matches(xml);
            return string.Join(" ", matches.Select(m => m.Groups[1].Value.Trim()));
        }
    }

    // ---------- Helpers ----------

    private sealed record CaptionTrack
    {
        public string BaseUrl { get; init; } = string.Empty;
        public string LanguageCode { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public bool IsTranslatable { get; init; }
    }

    [GeneratedRegex("ytInitialPlayerResponse\\s*=\\s*(\\{.*?\\});", RegexOptions.Singleline)]
    private static partial Regex PlayerResponseRegex();

    [GeneratedRegex("<text[^>]*>(.*?)</text>", RegexOptions.Singleline)]
    private static partial Regex TextRegex();
}