using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EventBus.Infrastructure.Queue;

public class ConcurrentQueueService<T>(ILogger<ConcurrentQueueService<T>> logger) : IQueueService<T>
{
    private readonly ConcurrentQueue<T> _queue = new();
    private int _count = 0;

    public int Count => _count;

    public bool IsEmpty => _queue.IsEmpty;

    public Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        try
        {
            _queue.Enqueue(item);
            Interlocked.Increment(ref _count);
            logger.LogDebug("Item enqueued to concurrent queue. Current count: {Count}", Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enqueuing item to concurrent queue");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<T?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_queue.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _count);
                logger.LogDebug("Item dequeued from concurrent queue. Remaining count: {Count}", Count);
                return Task.FromResult<T?>(item);
            }
            
            logger.LogDebug("Concurrent queue is empty, no item to dequeue");
            return Task.FromResult<T?>(default);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dequeuing item from concurrent queue");
            throw;
        }
    }

    public Task<(bool Success, T? Item)> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_queue.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _count);
                logger.LogDebug("Item successfully dequeued from concurrent queue. Remaining count: {Count}", Count);
                return Task.FromResult((true, (T?)item));
            }
            
            logger.LogDebug("Concurrent queue is empty, no item to dequeue");
            return Task.FromResult((false, (T?)default));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error trying to dequeue item from concurrent queue");
            return Task.FromResult((false, (T?)default));
        }
    }

    public Task<T?> PeekAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_queue.TryPeek(out var item))
            {
                logger.LogDebug("Item peeked from concurrent queue");
                return Task.FromResult<T?>(item);
            }
            
            logger.LogDebug("Concurrent queue is empty, no item to peek");
            return Task.FromResult<T?>(default);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error peeking item from concurrent queue");
            return Task.FromResult<T?>(default);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clearedCount = 0;
            while (_queue.TryDequeue(out _))
            {
                clearedCount++;
            }
            
            Interlocked.Exchange(ref _count, 0);
            logger.LogInformation("Concurrent queue cleared, removed {Count} items", clearedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing concurrent queue");
            throw;
        }
        
        return Task.CompletedTask;
    }
}

/// <summary>
/// 支援多個命名佇列的 ConcurrentQueue 實作
/// </summary>
public class ConcurrentQueueService(ILogger<ConcurrentQueueService> logger) : IQueueService
{
    private readonly ConcurrentDictionary<string, object> _queues = new();

    private IQueueService<T> GetOrCreateQueue<T>(string queueName)
    {
        return (IQueueService<T>)_queues.GetOrAdd(queueName, _ =>
        {
            // 使用 NullLogger 避免依賴問題
            var queueLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConcurrentQueueService<T>>.Instance;
            return new ConcurrentQueueService<T>(queueLogger);
        });
    }

    public async Task EnqueueAsync<T>(T item, string queueName = "default", CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue<T>(queueName);
        await queue.EnqueueAsync(item, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Item enqueued to queue: {QueueName}", queueName);
    }

    public async Task<T?> DequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue<T>(queueName);
        var result = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Item dequeued from queue: {QueueName}", queueName);
        return result;
    }

    public async Task<(bool Success, T? Item)> TryDequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue<T>(queueName);
        return await queue.TryDequeueAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> PeekAsync<T>(string queueName = "default", CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue<T>(queueName);
        return await queue.PeekAsync(cancellationToken).ConfigureAwait(false);
    }

    public int GetCount(string queueName = "default")
    {
        if (_queues.TryGetValue(queueName, out var queueObj))
        {
            var countProperty = queueObj.GetType().GetProperty("Count");
            return (int)(countProperty?.GetValue(queueObj) ?? 0);
        }
        return 0;
    }

    public bool IsEmpty(string queueName = "default")
    {
        return GetCount(queueName) == 0;
    }

    public async Task ClearAsync(string queueName = "default", CancellationToken cancellationToken = default)
    {
        if (_queues.TryGetValue(queueName, out var queueObj))
        {
            var clearMethod = queueObj.GetType().GetMethod("ClearAsync");
            if (clearMethod != null)
            {
                await ((Task)clearMethod.Invoke(queueObj, new object[] { cancellationToken })!).ConfigureAwait(false);
                logger.LogInformation("Queue cleared: {QueueName}", queueName);
            }
        }
    }

    public IEnumerable<string> GetQueueNames()
    {
        return _queues.Keys.ToList();
    }
}