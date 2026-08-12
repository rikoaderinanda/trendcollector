using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Transcript;

/// <summary>
/// Retrieves YouTube captions using YouTube's public watch page and
/// timedtext caption endpoints. Does NOT download the video.
///
/// The watch-page payload is extracted with a bracket-balancing parser rather
/// than a naive regex, because the embedded <c>ytInitialPlayerResponse</c>
/// JSON contains strings (URLs, raw HTML) that themselves include "};"
/// sequences. A lazy regex stops at the first "};" and truncates the JSON,
/// which made every transcript look unavailable.
/// </summary>
public sealed partial class YouTubeTranscriptProvider : ITranscriptProvider
{
    private const string WatchUrl = "https://www.youtube.com/watch?v={0}";
    private const string TranscriptSource = "youtube_captions";

    /// <summary>JS variable holding the watch-page player payload.</summary>
    private const string PlayerResponseVar = "ytInitialPlayerResponse";

    /// <summary>JS variable with watch data; used as a fallback source of caption tracks.</summary>
    private const string InitialDataVar = "ytInitialData";

    /// <summary>
    /// Number of watch-page fetches before giving up on captions. YouTube
    /// load-balances watch-page responses across servers that include
    /// <c>captionTracks</c> and servers that omit them ("A/B testing"), so a
    /// single request is not deterministic. Retrying with fresh requests
    /// dramatically increases the chance of hitting a server that includes caps.
    /// </summary>
    private const int MaxCaptionTrackAttempts = 3;

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

            var (xml, timedTextStatus, timedTextLength) =
                await FetchTimedTextAsync(captionTrack.BaseUrl, cancellationToken);
            var transcriptText = ParseCaptionXml(xml);

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                _logger.LogWarning(
                    "Caption track for video {VideoId} contained no text. " +
                    "HTTP {Status}, body length {BodyLength}.",
                    platformVideoId, timedTextStatus, timedTextLength);
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
        for (var attempt = 1; attempt <= MaxCaptionTrackAttempts; attempt++)
        {
            var pageHtml = await GetWatchPageHtmlAsync(videoId, cancellationToken);

            // Primary source: ytInitialPlayerResponse embedded in the watch page.
            var playerResponse = ExtractJsonObject(pageHtml, PlayerResponseVar);
            if (playerResponse is null)
            {
                _logger.LogWarning(
                    "Unable to extract '{Variable}' for video {VideoId} (attempt {Attempt}/{MaxAttempts}). " +
                    "HTML length: {HtmlLength}, first 300 chars: {HtmlSnippet}",
                    PlayerResponseVar, videoId, attempt, MaxCaptionTrackAttempts,
                    pageHtml.Length, Shorten(pageHtml, 300));
            }
            else
            {
                _logger.LogDebug(
                    "Extracted '{Variable}' for video {VideoId} ({JsonLength} chars, attempt {Attempt}/{MaxAttempts}).",
                    PlayerResponseVar, videoId, playerResponse.Length, attempt, MaxCaptionTrackAttempts);

                var captionTracks = ParseCaptionTracks(playerResponse);

                // Fallback 1: resilient deep search of the same payload in case
                // YouTube moved the captionTracks node to a different JSON path.
                if (captionTracks.Count == 0)
                {
                    captionTracks = FindCaptionTracksDeep(playerResponse);
                    if (captionTracks.Count > 0)
                    {
                        _logger.LogDebug(
                            "Found {Count} caption track(s) in '{Variable}' via deep search for video {VideoId}.",
                            captionTracks.Count, PlayerResponseVar, videoId);
                    }
                }

                // Fallback 2: some videos carry caption data in ytInitialData instead.
                if (captionTracks.Count == 0)
                {
                    var initialData = ExtractJsonObject(pageHtml, InitialDataVar);
                    if (initialData is not null)
                    {
                        captionTracks = FindCaptionTracksDeep(initialData);
                        if (captionTracks.Count > 0)
                        {
                            _logger.LogDebug(
                                "Found {Count} caption track(s) in '{Variable}' for video {VideoId}.",
                                captionTracks.Count, InitialDataVar, videoId);
                        }
                    }
                }

                if (captionTracks.Count > 0)
                {
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

                _logger.LogWarning(
                    "No caption tracks for video {VideoId} (attempt {Attempt}/{MaxAttempts}). " +
                    "playerResponse length: {JsonLength}, has captions key: {HasCaptionsKey}.",
                    videoId, attempt, MaxCaptionTrackAttempts, playerResponse.Length,
                    playerResponse.Contains("\"captions\"", StringComparison.Ordinal));
            }

            // YouTube load-balances watch-page responses between servers that
            // include captionTracks and servers that omit them ("A/B testing").
            // A fresh request often reaches a different server that includes
            // the caption data, so retry with a short backoff.
            if (attempt < MaxCaptionTrackAttempts)
            {
                var delayMs = attempt * 250;
                _logger.LogDebug(
                    "Retrying caption track discovery for video {VideoId} in {Delay} ms (attempt {Attempt}).",
                    videoId, delayMs, attempt);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        _logger.LogWarning(
            "No caption tracks found for video {VideoId} after {MaxAttempts} attempts.",
            videoId, MaxCaptionTrackAttempts);
        return null;
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

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug(
            "Watch page fetched for video {VideoId}: HTTP {Status}, {Bytes} chars.",
            videoId, (int)response.StatusCode, content.Length);

        return content;
    }

    /// <summary>
    /// Fetches the caption track body, returning the HTTP status and raw body
    /// length so callers can distinguish between an HTTP failure, an empty
    /// response (YouTube's current po_token-protected timedtext behavior) and
    /// a genuinely empty caption track.
    /// </summary>
    private async Task<(string Body, int StatusCode, int BodyLength)> FetchTimedTextAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.Referrer = new Uri("https://www.youtube.com/");

        using var response =
            await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug(
            "Timedtext fetched: HTTP {Status}, {Bytes} body chars.",
            (int)response.StatusCode, body.Length);

        return (body, (int)response.StatusCode, body.Length);
    }

    /// <summary>
    /// Extracts the JSON object assigned to <paramref name="variableName"/> in
    /// the watch-page HTML using bracket balancing.
    /// A lazy regex stops at the first "};" even when that sequence occurs
    /// inside a quoted string (URLs, raw HTML), truncating the JSON and making
    /// caption detection fail. Bracket balancing stops only when the top-level
    /// object is actually closed.
    /// </summary>
    private static string? ExtractJsonObject(string html, string variableName)
    {
        var nameIndex = html.IndexOf(variableName, StringComparison.Ordinal);
        if (nameIndex < 0)
        {
            return null;
        }

        var cursor = nameIndex + variableName.Length;

        // Allow whitespace between the variable name and '=' (YouTube emits
        // "var ytInitialPlayerResponse = {" with spaces around the '=').
        while (cursor < html.Length && char.IsWhiteSpace(html[cursor]))
        {
            cursor++;
        }

        if (cursor >= html.Length || html[cursor] != '=')
        {
            return null;
        }

        cursor++;

        // Skip whitespace between '=' and '{' (e.g. "var x =  { ... }").
        while (cursor < html.Length && char.IsWhiteSpace(html[cursor]))
        {
            cursor++;
        }

        if (cursor >= html.Length || html[cursor] != '{')
        {
            return null;
        }

        return ExtractBalancedJson(html, cursor);
    }

    private static string? ExtractBalancedJson(string html, int openBraceIndex)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = openBraceIndex; i < html.Length; i++)
        {
            var c = html[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return html[openBraceIndex..(i + 1)];
                    }

                    break;
            }
        }

        return null;
    }

    private static List<CaptionTrack> ParseCaptionTracks(string playerResponseJson)
    {
        var tracks = new List<CaptionTrack>();

        using var document = JsonDocument.Parse(playerResponseJson);

        if (!document.RootElement.TryGetProperty("captions", out var captions) ||
            !captions.TryGetProperty("playerCaptionsTracklistRenderer", out var renderer) ||
            !renderer.TryGetProperty("captionTracks", out var captionTracksElement) ||
            captionTracksElement.ValueKind != JsonValueKind.Array)
        {
            return tracks;
        }

        foreach (var trackElement in captionTracksElement.EnumerateArray())
        {
            var track = ParseCaptionTrack(trackElement);
            if (track is not null)
            {
                tracks.Add(track);
            }
        }

        return tracks;
    }

    /// <summary>
    /// Resilient fallback that walks the whole payload looking for any
    /// property literally named "captionTracks". Protects against future
    /// changes in where YouTube puts the track list inside the JSON.
    /// </summary>
    private static List<CaptionTrack> FindCaptionTracksDeep(string json)
    {
        var tracks = new List<CaptionTrack>();

        try
        {
            using var document = JsonDocument.Parse(json);
            WalkForCaptionTracks(document.RootElement, tracks);
        }
        catch (JsonException)
        {
            // Ignore malformed fallback payload - caller handles empty list.
        }

        return tracks;
    }

    private static void WalkForCaptionTracks(JsonElement element, List<CaptionTrack> tracks)
    {
        if (tracks.Count >= 10)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("captionTracks") &&
                        property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var trackElement in property.Value.EnumerateArray())
                        {
                            if (tracks.Count >= 10)
                            {
                                break;
                            }

                            var track = ParseCaptionTrack(trackElement);
                            if (track is not null)
                            {
                                tracks.Add(track);
                            }
                        }
                    }
                    else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        WalkForCaptionTracks(property.Value, tracks);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    WalkForCaptionTracks(item, tracks);
                }

                break;
        }
    }

    private static CaptionTrack? ParseCaptionTrack(JsonElement trackElement)
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

        if (string.IsNullOrEmpty(baseUrl))
        {
            return null;
        }

        return new CaptionTrack
        {
            BaseUrl = baseUrl,
            LanguageCode = languageCode ?? "unknown",
            Language = name ?? languageCode ?? "unknown",
            IsTranslatable = isTranslatable
        };
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

    private static string Shorten(string text, int maxLength)
    {
        var cleaned = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return cleaned.Length <= maxLength
            ? cleaned
            : cleaned[..maxLength] + "...";
    }

    private sealed record CaptionTrack
    {
        public string BaseUrl { get; init; } = string.Empty;
        public string LanguageCode { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public bool IsTranslatable { get; init; }
    }

    [GeneratedRegex("<text[^>]*>(.*?)</text>", RegexOptions.Singleline)]
    private static partial Regex TextRegex();
}