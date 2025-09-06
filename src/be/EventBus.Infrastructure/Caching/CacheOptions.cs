namespace EventBus.Infrastructure.Caching;

public class CacheOptions
{
    public const string SectionName = "EventBus:Cache";

    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);
    public bool EnableLogging { get; set; } = true;
    public int MaxItems { get; set; } = 10000;
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(5);

    // 不同類型快取的個別設定
    public Dictionary<string, TimeSpan> TypeSpecificExpirations { get; set; } = new();
}