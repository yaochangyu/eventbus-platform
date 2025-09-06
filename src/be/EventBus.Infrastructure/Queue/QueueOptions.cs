namespace EventBus.Infrastructure.Queue;

public class QueueOptions
{
    public const string SectionName = "EventBus:Queue";

    /// <summary>
    /// 佇列提供者類型
    /// </summary>
    public QueueProviderType ProviderType { get; set; } = QueueProviderType.Channel;

    /// <summary>
    /// 預設佇列容量（僅適用於 Channel）
    /// </summary>
    public int DefaultCapacity { get; set; } = 1000;

    /// <summary>
    /// 是否啟用日誌記錄
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 佇列滿時的處理方式（僅適用於 Channel）
    /// </summary>
    public QueueFullMode FullMode { get; set; } = QueueFullMode.Wait;

    /// <summary>
    /// 個別佇列設定
    /// </summary>
    public Dictionary<string, QueueConfiguration> NamedQueues { get; set; } = new();
}

public class QueueConfiguration
{
    public int Capacity { get; set; } = 1000;
    public QueueProviderType? ProviderType { get; set; }
    public QueueFullMode FullMode { get; set; } = QueueFullMode.Wait;
}

public enum QueueProviderType
{
    Channel,
    ConcurrentQueue
}

public enum QueueFullMode
{
    Wait,
    DropOldest,
    DropNewest
}