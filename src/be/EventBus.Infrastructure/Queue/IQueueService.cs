namespace EventBus.Infrastructure.Queue;

public interface IQueueService<T>
{
    /// <summary>
    /// 將項目加入佇列
    /// </summary>
    Task EnqueueAsync(T item, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 從佇列移除並返回項目
    /// </summary>
    Task<T?> DequeueAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 嘗試從佇列移除並返回項目（非阻塞）
    /// </summary>
    Task<(bool Success, T? Item)> TryDequeueAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查看佇列中的下一個項目但不移除
    /// </summary>
    Task<T?> PeekAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取得佇列中的項目數量
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// 檢查佇列是否為空
    /// </summary>
    bool IsEmpty { get; }
    
    /// <summary>
    /// 清空佇列
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 通用佇列服務介面（非泛型版本）
/// </summary>
public interface IQueueService
{
    Task EnqueueAsync<T>(T item, string queueName = "default", CancellationToken cancellationToken = default);
    Task<T?> DequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default);
    Task<(bool Success, T? Item)> TryDequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default);
    Task<T?> PeekAsync<T>(string queueName = "default", CancellationToken cancellationToken = default);
    int GetCount(string queueName = "default");
    bool IsEmpty(string queueName = "default");
    Task ClearAsync(string queueName = "default", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取得所有佇列名稱
    /// </summary>
    IEnumerable<string> GetQueueNames();
}