using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using EventBus.Infrastructure.Models;

namespace EventBus.Infrastructure.Queue;

public class ConcurrentQueueProvider<T>(ILogger<ConcurrentQueueProvider<T>> logger) : IQueueProvider<T>
{
    private readonly ConcurrentQueue<T> _queue = new();
    private int _count = 0;

    public int Count => _count;

    public bool IsEmpty => _queue.IsEmpty;

    public Task<Result<bool, Failure>> EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        try
        {
            _queue.Enqueue(item);
            Interlocked.Increment(ref _count);
            logger.LogDebug("Item enqueued to concurrent queue. Current count: {Count}", Count);
            return Task.FromResult(Result<bool, Failure>.Ok(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enqueuing item to concurrent queue");
            return Task.FromResult(Result<bool, Failure>.Fail(new Failure(FailureCode.EnqueueError.ToString(), ex.Message) { Exception = ex }));
        }
    }

    public Task<Result<T?, Failure>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_queue.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _count);
                logger.LogDebug("Item dequeued from concurrent queue. Remaining count: {Count}", Count);
                return Task.FromResult(Result<T?, Failure>.Ok(item));
            }
            
            logger.LogDebug("Concurrent queue is empty, no item to dequeue");
            return Task.FromResult(Result<T?, Failure>.Ok(default));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dequeuing item from concurrent queue");
            return Task.FromResult(Result<T?, Failure>.Fail(new Failure(FailureCode.DequeueError.ToString(), ex.Message) { Exception = ex }));
        }
    }

    public Task<Result<T?, Failure>> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_queue.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _count);
                logger.LogDebug("Item successfully dequeued from concurrent queue. Remaining count: {Count}", Count);
                return Task.FromResult(Result<T?, Failure>.Ok(item));
            }
            
            logger.LogDebug("Concurrent queue is empty, no item to dequeue");
            return Task.FromResult(Result<T?, Failure>.Ok(default));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error trying to dequeue item from concurrent queue");
            return Task.FromResult(Result<T?, Failure>.Fail(new Failure(FailureCode.TryDequeueError.ToString(), ex.Message) { Exception = ex }));
        }
    }

    public Task<Result<T?, Failure>> PeekAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_queue.TryPeek(out var item))
            {
                logger.LogDebug("Item peeked from concurrent queue");
                return Task.FromResult(Result<T?, Failure>.Ok(item));
            }
            
            logger.LogDebug("Concurrent queue is empty, no item to peek");
            return Task.FromResult(Result<T?, Failure>.Ok(default));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error peeking item from concurrent queue");
            return Task.FromResult(Result<T?, Failure>.Fail(new Failure(FailureCode.PeekError.ToString(), ex.Message) { Exception = ex }));
        }
    }

    public Task<Result<bool, Failure>> ClearAsync(CancellationToken cancellationToken = default)
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
            return Task.FromResult(Result<bool, Failure>.Ok(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing concurrent queue");
            return Task.FromResult(Result<bool, Failure>.Fail(new Failure(FailureCode.ClearError.ToString(), ex.Message) { Exception = ex }));
        }
    }
}

/// <summary>
/// 支援多個命名佇列的 ConcurrentQueue 實作
/// </summary>
public class ConcurrentQueueService(ILogger<ConcurrentQueueService> logger) : IQueueProvider
{
    private readonly ConcurrentDictionary<string, object> _queues = new();

    private IQueueProvider<T> GetOrCreateQueue<T>(string queueName)
    {
        return (IQueueProvider<T>)_queues.GetOrAdd(queueName, _ =>
        {
            // 使用 NullLogger 避免依賴問題
            var queueLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConcurrentQueueProvider<T>>.Instance;
            return new ConcurrentQueueProvider<T>(queueLogger);
        });
    }

    public async Task<Result<bool, Failure>> EnqueueAsync<T>(T item, string queueName = "default", CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue<T>(queueName);
        var result = await queue.EnqueueAsync(item, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            logger.LogDebug("Item enqueued to queue: {QueueName}", queueName);
        }
        return result;
    }

    public async Task<Result<T?, Failure>> DequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue<T>(queueName);
        var result = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            logger.LogDebug("Item dequeued from queue: {QueueName}", queueName);
        }
        return result;
    }

    public async Task<Result<T?, Failure>> TryDequeueAsync<T>(string queueName = "default", CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue<T>(queueName);
        return await queue.TryDequeueAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<T?, Failure>> PeekAsync<T>(string queueName = "default", CancellationToken cancellationToken = default)
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

    public async Task<Result<bool, Failure>> ClearAsync(string queueName = "default", CancellationToken cancellationToken = default)
    {
        try
        {
            if (_queues.TryGetValue(queueName, out var queueObj))
            {
                var clearMethod = queueObj.GetType().GetMethod("ClearAsync");
                if (clearMethod != null)
                {
                    var taskResult = (Task<Result<bool, Failure>>)clearMethod.Invoke(queueObj, new object[] { cancellationToken })!;
                    var result = await taskResult.ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        logger.LogInformation("Queue cleared: {QueueName}", queueName);
                    }
                    return result;
                }
            }
            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing queue: {QueueName}", queueName);
            return Result<bool, Failure>.Fail(new Failure(FailureCode.ClearQueueError.ToString(), ex.Message) { Exception = ex });
        }
    }

    public IEnumerable<string> GetQueueNames()
    {
        return _queues.Keys.ToList();
    }
}