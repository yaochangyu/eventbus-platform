using EventBus.Infrastructure.Models;

namespace EventBus.Infrastructure.Queue;

public interface IQueueProvider<T>
{
    /// <summary>
    /// 將項目加入佇列
    /// </summary>
    Task<Result<bool, Failure>> EnqueueAsync(T item, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 從佇列移除並返回項目
    /// </summary>
    Task<Result<T?, Failure>> DequeueAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 嘗試從佇列移除並返回項目（非阻塞）
    /// </summary>
    Task<Result<T?, Failure>> TryDequeueAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查看佇列中的下一個項目但不移除
    /// </summary>
    Task<Result<T?, Failure>> PeekAsync(CancellationToken cancellationToken = default);
    
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
    Task<Result<bool, Failure>> ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 通用佇列服務介面（非泛型版本）
/// </summary>
public interface IQueueProvider
{
    Task<Result<bool, Failure>> EnqueueAsync<T>(T item, string queueName = "default", CancellationToken cancellationToken = default);
    Task<Result<T?, Failure>> DequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default);
    Task<Result<T?, Failure>> TryDequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default);
    Task<Result<T?, Failure>> PeekAsync<T>(string queueName = "default", CancellationToken cancellationToken = default);
    int GetCount(string queueName = "default");
    bool IsEmpty(string queueName = "default");
    Task<Result<bool, Failure>> ClearAsync(string queueName = "default", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取得所有佇列名稱
    /// </summary>
    IEnumerable<string> GetQueueNames();
}