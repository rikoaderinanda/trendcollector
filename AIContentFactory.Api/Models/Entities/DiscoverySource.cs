namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Possible sources of trend keywords. Currently only AI is implemented;
/// other sources will be added in the future.
/// </summary>
public static class DiscoverySource
{
    public const string AI = "AI";
    public const string GoogleTrends = "GoogleTrends";
    public const string Reddit = "Reddit";
    public const string X = "X";
    public const string TikTok = "TikTok";
    public const string Instagram = "Instagram";
    public const string NewsApi = "NewsAPI";
    public const string Rss = "RSS";
    public const string Manual = "Manual";
}