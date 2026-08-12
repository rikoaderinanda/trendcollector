namespace AIContentFactory.Api.Configuration;

/// <summary>
/// Options for transcript retrieval including the yt-dlp fallback.
/// Bound from the "Transcript" configuration section.
/// </summary>
public sealed class TranscriptOptions
{
    public const string SectionName = "Transcript";

    /// <summary>
    /// When true, the composite transcript provider falls back to yt-dlp
    /// when the HTTP scraping provider cannot retrieve a transcript.
    /// </summary>
    public bool FallbackEnabled { get; set; } = true;

    /// <summary>
    /// Path to the yt-dlp executable. Supports a literal path
    /// (e.g. "C:\Tools\yt-dlp.exe") or a bare command name (e.g. "yt-dlp")
    /// when it is on the PATH.
    /// </summary>
    public string YtDlpPath { get; set; } = "yt-dlp";

    /// <summary>
    /// Maximum time in seconds to wait for a single yt-dlp subtitle fetch.
    /// </summary>
    public int FallbackTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum subtitle file size in bytes to read. Larger files are rejected.
    /// </summary>
    public long MaxSubtitleFileBytes { get; set; } = 5_000_000;

    /// <summary>
    /// When true, the composite provider prefers the existing YouTube Data API
    /// video title as the language hint for yt-dlp subtitles.
    /// </summary>
    public bool UseVideoLanguageHint { get; set; } = true;

    /// <summary>
    /// Minimum interval in seconds between consecutive yt-dlp invocations.
    ///
    /// This is NOT the same as DelayBetweenJobsSeconds (which spaces
    /// queue-level jobs). A single yt-dlp invocation fans out into many HTTP
    /// requests to YouTube (several languages × subtitle formats × internal
    /// retries), so pacing at the yt-dlp level is required to avoid triggering
    /// HTTP 429 rate limiting. Shared in-process via YtDlpRateLimiter.
    /// Default: 30 seconds.
    /// </summary>
    public int MinimumRequestIntervalSeconds { get; set; } = 30;
}
