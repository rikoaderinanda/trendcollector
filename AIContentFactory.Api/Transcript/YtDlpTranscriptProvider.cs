using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Entities;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.Transcript;

/// <summary>
/// Retrieves YouTube captions by invoking the <c>yt-dlp</c> executable in
/// subprocess mode.
///
/// yt-dlp internally handles the proof-of-origin token (po_token) challenge
/// that YouTube now requires for its transcript endpoints. Raw HTTP scraping
/// (see <see cref="YouTubeTranscriptProvider"/>) returns HTTP 200 with an
/// empty body on the timedtext endpoint and HTTP 400 on
/// <c>youtubei/v1/get_transcript</c> without a po_token, so this provider is
/// the reliable fallback when scraping fails.
/// </summary>
public sealed class YtDlpTranscriptProvider : ITranscriptProvider
{
    private const string TranscriptSource = "yt_dlp";

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    private static readonly string[] SubtitleFormats = ["vtt", "srt", "ass", "json3", "srv3"];

    private readonly ILogger<YtDlpTranscriptProvider> _logger;
    private readonly TranscriptOptions _options;
    private readonly YtDlpRateLimiter _rateLimiter;
    private readonly string _ytDlpPath;

    public YtDlpTranscriptProvider(
        IOptions<TranscriptOptions> options,
        YtDlpRateLimiter rateLimiter,
        ILogger<YtDlpTranscriptProvider> logger)
    {
        _options = options.Value;
        _rateLimiter = rateLimiter;
        _logger = logger;
        _ytDlpPath = ResolveYtDlpPath(_options.YtDlpPath);
    }

    /// <summary>
    /// Resolves the yt-dlp executable path using, in order:
    /// <list type="number">
    /// <item>A configured absolute path or command name.</item>
    /// <item>The <c>tools\yt-dlp.exe</c> directory shipped next to the app
    /// (the csproj copies the binary to the output directory).</item>
    /// <item>The bare command name so OS PATH resolution can find it.</item>
    /// </list>
    /// </summary>
    private static string ResolveYtDlpPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (configuredPath == "yt-dlp" || configuredPath == "yt-dlp.exe")
            {
                return configuredPath;
            }

            if (File.Exists(configuredPath) ||
                configuredPath.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                configuredPath.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                return configuredPath;
            }
        }

        // Check the tools folder next to the running assembly (works for the
        // source-tree build and for published output because the csproj copies
        // tools\yt-dlp.exe to the output directory).
        var baseDir = AppContext.BaseDirectory;
        var toolsPath = Path.Combine(baseDir, "tools", "yt-dlp.exe");
        if (File.Exists(toolsPath))
        {
            return toolsPath;
        }

        var rootToolsPath = Path.Combine(baseDir, "yt-dlp.exe");
        if (File.Exists(rootToolsPath))
        {
            return rootToolsPath;
        }

        return "yt-dlp";
    }

    public async Task<VideoTranscript?> GetTranscriptAsync(
        string platformVideoId,
        string? preferredLanguage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platformVideoId))
        {
            _logger.LogWarning("yt-dlp transcript skipped: empty video id.");
            return null;
        }

        var timeout = TimeSpan.FromSeconds(Math.Max(5, _options.FallbackTimeoutSeconds));

        if (!IsYtDlpAvailable())
        {
            _logger.LogError(
                "yt-dlp executable '{Path}' was not found on this machine. " +
                "Transcript fallback is unavailable.", _ytDlpPath);
            return null;
        }

        var languageCandidates = BuildLanguageCandidates(preferredLanguage);
        var workDir = CreateTempDirectory();
        var cookiesFile = BuildCookiesFile();

        IDisposable? limiterLease = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Acquire the shared yt-dlp rate limiter before invocation.
            // Paces consecutive yt-dlp runs to avoid HTTP 429 bursts.
            limiterLease = await _rateLimiter.AcquireAsync(cancellationToken);

            _logger.LogDebug(
                "yt-dlp: attempting subtitles for video {VideoId} in languages '{Langs}'.",
                platformVideoId, string.Join(",", languageCandidates));

            var result = await FetchSubtitlesAsync(
                platformVideoId,
                preferredLanguage,
                languageCandidates,
                workDir,
                cookiesFile,
                timeout,
                cancellationToken);

            if (result is null)
            {
                _logger.LogWarning(
                    "yt-dlp: no subtitle file produced for video {VideoId} after one batch attempt.",
                    platformVideoId);
                return null;
            }

            var transcript = NormalizeTranscriptText(result.Content);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                _logger.LogWarning(
                    "yt-dlp: subtitle file {File} for video {VideoId} contained no usable text.",
                    result.FilePath, platformVideoId);
                return null;
            }

            _logger.LogInformation(
                "yt-dlp: subtitles retrieved for video {VideoId} in language '{Lang}' " +
                "({Length} chars, file {File}).",
                platformVideoId, result.Language, transcript.Length, result.FilePath);

            return new VideoTranscript
            {
                Transcript = transcript,
                Language = NormalizeLanguage(result.Language, preferredLanguage),
                Source = TranscriptSource
            };
        }
        catch (TranscriptTransientException)
        {
            // Transient failure (429/5xx/timeout). The caller (queue worker)
            // will retry with exponential backoff rather than marking the job
            // as permanently TranscriptUnavailable. Re-throw so downstream can
            // apply its own retry policy.
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("yt-dlp transcript fetch cancelled for video {VideoId}.", platformVideoId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp transcript fetch failed for video {VideoId}.", platformVideoId);
            return null;
        }
        finally
        {
            limiterLease?.Dispose();
            TryCleanupDirectory(workDir);
            TryDeleteFile(cookiesFile);
        }
    }

    // ---------- Language candidates ----------

    private static List<string> BuildLanguageCandidates(string? preferredLanguage)
    {
        var candidates = new List<string>(6);

        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            candidates.Add(preferredLanguage);
            candidates.Add(preferredLanguage + "-orig");
        }

        candidates.Add("en");
        candidates.Add("en-orig");
        candidates.Add("auto");

        // yt-dlp's --sub-langs accepts language tags joined by commas. Remove
        // duplicates while preserving order so the preferred language is first.
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ---------- yt-dlp subprocess ----------

    private async Task<SubtitleResult?> FetchSubtitlesAsync(
        string videoId,
        string? preferredLanguage,
        List<string> languageCandidates,
        string workDir,
        string cookiesFile,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var outputTemplate = Path.Combine(workDir, "%(id)s.%(ext)s");
        var filesOutPath = Path.Combine(workDir, "files.out.txt");
        var langsArg = string.Join(",", languageCandidates);

        var args = new List<string>
        {
            "--no-warnings",
            "--no-progress",
            "--skip-download",
            "--no-part",
            "--no-mtime",
            "--write-auto-subs",
            "--write-subs",
            "--sub-langs", langsArg,
            "--sub-format", string.Join("/", SubtitleFormats),
            "--convert-subs", "vtt",
            "--print-to-file", "after_move:filepath", filesOutPath,
            "--output", outputTemplate,
            "--user-agent", UserAgent,
            "--referer", $"https://www.youtube.com/watch?v={videoId}",
            "--cookies", cookiesFile,
            // Let yt-dlp handle transient HTTP errors (429/5xx) internally with
            // a small number of retries. The queue-level retry (with exponential
            // backoff and jitter) is the appropriate layer for long-term rate
            // limit handling; keeping yt-dlp's internal retries low avoids
            // multiplying request volume against the rate limiter.
            "--retries", "3",
            "--sleep-interval", "5",
            "--max-sleep-interval", "30",
            "--fragment-retries", "3",
            $"https://www.youtube.com/watch?v={videoId}"
        };

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                _logger.LogError("yt-dlp failed to start for video {VideoId}.", videoId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp failed to start for video {VideoId}.", videoId);
            return null;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            await process.WaitForExitAsync(timeoutCts.Token);

            if (cancellationToken.IsCancellationRequested)
            {
                KillProcessTree(process);
                return null;
            }

            if (process.ExitCode != 0)
            {
                var stderrText = stderr.ToString();
                _logger.LogWarning(
                    "yt-dlp exited with code {ExitCode} for video {VideoId}. stderr: {Stderr}",
                    process.ExitCode, videoId, Shorten(stderrText, 500));

                // Distinguish transient failures (rate limited / server errors /
                // temporary block) from permanent ones (video deleted, private,
                // no captions at all). Transient failures should be retried by
                // the queue with backoff instead of being terminal.
                if (IsTransientFailure(stderrText))
                {
                    throw new TranscriptTransientException(
                        $"yt-dlp failed for video {videoId} with a transient error (HTTP {process.ExitCode}). " +
                        Shorten(stderrText, 300));
                }

                return null;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "yt-dlp timed out after {Timeout}s for video {VideoId}. Killing process.",
                timeout.TotalSeconds, videoId);
            KillProcessTree(process);
            throw new TranscriptTransientException(
                $"yt-dlp timed out after {timeout.TotalSeconds}s for video {videoId}.");
        }

        var subtitleFiles = LocateSubtitleFiles(workDir, filesOutPath);
        if (subtitleFiles.Count == 0)
        {
            _logger.LogDebug(
                "yt-dlp: no subtitle file produced for video {VideoId}. stdout: {Stdout}",
                videoId, Shorten(stdout.ToString(), 300));
            return null;
        }

        // Prefer a subtitle file whose language matches the caller preference,
        // then a manually-authored English track, then any other track.
        var chosen = SelectBestSubtitleFile(subtitleFiles, preferredLanguage);
        if (chosen is null)
        {
            return null;
        }

        var fileInfo = new FileInfo(chosen);
        if (fileInfo.Length <= 0)
        {
            _logger.LogDebug("yt-dlp: subtitle file {File} is empty.", chosen);
            return null;
        }

        if (fileInfo.Length > _options.MaxSubtitleFileBytes)
        {
            _logger.LogWarning(
                "yt-dlp: subtitle file {File} is {Bytes} bytes, exceeding the {MaxBytes} byte limit.",
                chosen, fileInfo.Length, _options.MaxSubtitleFileBytes);
            return null;
        }

        var content = await File.ReadAllTextAsync(chosen, Encoding.UTF8, cancellationToken);
        return new SubtitleResult
        {
            Content = content,
            Language = InferLanguage(fileInfo.Name, content),
            FilePath = chosen
        };
    }

    private static List<string> LocateSubtitleFiles(string workDir, string filesOutPath)
    {
        var candidates = new List<string>();

        if (File.Exists(filesOutPath))
        {
            foreach (var line in File.ReadAllLines(filesOutPath))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    candidates.Add(trimmed);
                }
            }
        }

        var subtitleExtensions = new HashSet<string>(
            SubtitleFormats.Select(f => "." + f),
            StringComparer.OrdinalIgnoreCase);

        candidates.AddRange(Directory.EnumerateFiles(workDir)
            .Where(f => subtitleExtensions.Contains(Path.GetExtension(f))));

        return candidates.Where(File.Exists).ToList();
    }

    /// <summary>
    /// Picks the most desirable subtitle file from the produced set. Preference
    /// order is: caller-preferred language, English, any other. Auto-generated
    /// tracks are preferred over manually-authored ones only when the requested
    /// language is auto-generated.
    /// </summary>
    private static string? SelectBestSubtitleFile(
        List<string> files,
        string? preferredLanguage)
    {
        if (files.Count == 0)
        {
            return null;
        }

        // Extract the language tag from each file name (e.g. "id.auto.vtt" -> "id").
        static string LanguageOf(string file)
        {
            var match = Regex.Match(Path.GetFileName(file),
                @"\.([a-z]{2,3})(?:\.auto)?\.(?:vtt|srt|ass|json3|srv3)",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty;
        }

        // Prefer auto-generated files (contain ".auto.") over manual ones.
        static bool IsAuto(string file) =>
            Path.GetFileName(file).Contains(".auto.", StringComparison.OrdinalIgnoreCase);

        // 1. Preferred language.
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            var preferred = files
                .Where(f => string.Equals(LanguageOf(f), preferredLanguage, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(IsAuto)
                .FirstOrDefault();
            if (preferred is not null)
            {
                return preferred;
            }
        }

        // 2. English.
        var english = files
            .Where(f => LanguageOf(f) is "en")
            .OrderByDescending(IsAuto)
            .FirstOrDefault();
        if (english is not null)
        {
            return english;
        }

        // 3. Any other (non-empty, checked by caller).
        return files.First();
    }

    private static string InferLanguage(string fileName, string content)
    {
        // Language tag sometimes appears in the filename (e.g. "en.auto.vtt").
        var match = Regex.Match(
            fileName,
            @"\.([a-z]{2,3})(?:\.auto)?\.(?:vtt|srt|ass|json3|srv3)",
            RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.ToLowerInvariant();
        }

        // Fallback: scan the VTT header for a "Language:" line.
        var langMatch = Regex.Match(content, @"(?im)^language[:\s]+([a-z]{2,3})");
        if (langMatch.Success)
        {
            return langMatch.Groups[1].Value.ToLowerInvariant();
        }

        return "unknown";
    }

    private static string NormalizeLanguage(string inferred, string? callerPreferred)
    {
        if (inferred.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
            inferred.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return callerPreferred ?? "unknown";
        }

        return inferred;
    }

    /// <summary>
    /// Converts VTT/SRT/ASS subtitle content into a plain text transcript.
    /// Strips timestamps, cue numbers, metadata headers and ASS override tags.
    /// </summary>
    public static string NormalizeTranscriptText(string subtitleContent)
    {
        var lines = subtitleContent
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        var sb = new StringBuilder();
        var inCue = false;
        var sawText = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                if (inCue)
                {
                    sb.Append(' ');
                    inCue = false;
                }

                continue;
            }

            if (trimmed.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Kind:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Language:", StringComparison.OrdinalIgnoreCase))
            {
                inCue = false;
                continue;
            }

            // Cue header lines like "1" or "00:00:01.000 --> 00:00:04.000".
            if (Regex.IsMatch(trimmed, @"^\d+$") ||
                trimmed.Contains("-->", StringComparison.Ordinal))
            {
                inCue = true;
                continue;
            }

            // ASS style/header sections.
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inCue = false;
                continue;
            }

            // Strip ASS override tags ({...}) and HTML tags.
            var cleaned = Regex.Replace(trimmed, @"\{[^}]*\}", string.Empty);
            cleaned = Regex.Replace(cleaned, "<[^>]+>", string.Empty);

            // Decode HTML entities such as ampersand-lt and ampersand-gt into
            // their literal characters.
            cleaned = WebUtility.HtmlDecode(cleaned).Trim();

            if (cleaned.Length == 0)
            {
                continue;
            }

            if (sawText)
            {
                sb.Append(' ');
            }

            sb.Append(cleaned);
            sawText = true;
            inCue = true;
        }

        // YouTube auto-generated captions frequently contain the same phrase
        // duplicated across adjacent VTT cues (e.g. word-level ASR alignment
        // repeats the last few words of the prior cue). Remove consecutive
        // identical word-phrases so downstream AI extraction receives a clean
        // transcript instead of a stuttering duplication.
        return RemoveConsecutiveDuplicatePhrases(sb.ToString().Trim());
    }

    /// <summary>
    /// Removes immediately adjacent duplicate word-phrases (stutter / ASR
    /// repetition). YouTube auto-generated captions frequently repeat the same
    /// n-gram in consecutive VTT cues (e.g. "...real estate investment Other
    /// than REITs, real estate investment...").
    ///
    /// For every position in the word sequence we test adjacent n-grams of
    /// increasing size (1..maxNgram) and, when two neighbouring windows are
    /// identical, drop the duplicated one. Word-boundary punctuation and case
    /// are normalized before comparison so "investment," vs "investment" match.
    /// </summary>
    public static string RemoveConsecutiveDuplicatePhrases(
        string text,
        int maxNgram = 12)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
        {
            return text;
        }

        var normalized = words
            .Select(w => w.Trim().TrimEnd(',', '.', '!', '?', ';', ':').ToLowerInvariant())
            .ToArray();

        var kept = new List<string>(words.Length);
        var i = 0;

        while (i < words.Length)
        {
            var duplicated = false;

            // Test increasing adjacent n-gram sizes starting at i. n is capped
            // so the second window fits within the remaining text.
            for (var n = 1; n <= maxNgram && i + n + n <= words.Length; n++)
            {
                var first = normalized.AsSpan(i, n);
                var second = normalized.AsSpan(i + n, n);

                if (first.SequenceEqual(second))
                {
                    // Keep the first window, advance past both windows so the
                    // duplicated second window is dropped entirely.
                    for (var k = 0; k < n; k++)
                    {
                        kept.Add(words[i + k]);
                    }

                    i += n + n;
                    duplicated = true;
                    break;
                }
            }

            if (!duplicated)
            {
                kept.Add(words[i]);
                i++;
            }
        }

        return string.Join(' ', kept);
    }

    // ---------- File & process helpers ----------

    private bool IsYtDlpAvailable()
    {
        try
        {
            if (File.Exists(_ytDlpPath))
            {
                return true;
            }

            if (_ytDlpPath.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                _ytDlpPath.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                // A literal path was specified but does not exist.
                return false;
            }

            // Bare command name: probe for it on PATH ("where" / "which").
            using var probe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "where.exe" : "which",
                    ArgumentList = { _ytDlpPath },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            probe.Start();
            probe.WaitForExit(5000);
            return probe.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to probe yt-dlp availability at '{Path}'.", _ytDlpPath);
            return false;
        }
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aicf-ytdlp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void TryCleanupDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temporary directory {Path}.", path);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temporary file {Path}.", path);
        }
    }

    private void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(1000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill yt-dlp process.");
        }
    }

    private static string BuildCookiesFile()
    {
        // yt-dlp accepts a cookies file in Netscape format. A consent cookie
        // avoids the European consent wall for region-locked transcript access.
        var path = Path.Combine(Path.GetTempPath(), $"aicf-cookies-{Guid.NewGuid():N}.txt");
        const string consent = "YES+cb.20240101-01-p0.en+FX+700";
        var lines = new[]
        {
            "# Netscape HTTP Cookie File",
            ".youtube.com\tTRUE\t/\tFALSE\t0\tCONSENT\t" + consent,
            ".google.com\tTRUE\t/\tFALSE\t0\tCONSENT\t" + consent
        };

        File.WriteAllText(path, string.Join("\n", lines));
        return path;
    }

    /// <summary>
    /// Determines whether a yt-dlp stderr message indicates a transient failure
    /// that might succeed on retry (rate limiting, temporary HTTP errors) versus
    /// a permanent failure (video deleted, private, no captions exist).
    /// </summary>
    /// <summary>
    /// Determines whether the error indicates daily quota exhaustion (permanent
    /// for the day) before checking transient failure patterns. Quota exhaustion
    /// must NOT be treated as transient — retrying it would waste attempts
    /// against a permanently (for the day) saturated window.
    /// </summary>
    private static bool IsQuotaExhausted(string stderr)
    {
        if (string.IsNullOrEmpty(stderr))
        {
            return false;
        }

        return stderr.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("daily limit", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("quota limit", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("daily quota", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("exceeded your quota", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a yt-dlp stderr message indicates a transient failure
    /// that might succeed on retry (rate limiting, temporary HTTP errors) versus
    /// a permanent failure (video deleted, private, no captions exist).
    ///
    /// Daily quota exhaustion is NOT transient and is excluded here —
    /// callers check <see cref="IsQuotaExhausted"/> first.
    /// </summary>
    private static bool IsTransientFailure(string stderr)
    {
        if (string.IsNullOrEmpty(stderr))
        {
            return false;
        }

        // Permanent (for the day) quota exhaustion must not be retried as
        // a transient 429.
        if (IsQuotaExhausted(stderr))
        {
            return false;
        }

        return stderr.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("HTTP Error 500", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("HTTP Error 502", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("HTTP Error 503", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("HTTP Error 504", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Request throttled", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Rate limit", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Connection reset", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Temporary failure", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Unable to extract", StringComparison.OrdinalIgnoreCase) &&
               stderr.Contains("429", StringComparison.OrdinalIgnoreCase);
    }

    private static string Shorten(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var cleaned = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength] + "...";
    }

    private sealed record SubtitleResult
    {
        public required string Content { get; init; }
        public required string Language { get; init; }
        public required string FilePath { get; init; }
    }
}