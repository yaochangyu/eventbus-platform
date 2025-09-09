using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using System.Collections.Concurrent;
using EventBus.Infrastructure.Models;

namespace EventBus.Infrastructure.Queue;

public class ChannelQueueService<T>(ILogger<ChannelQueueService<T>> logger, int capacity = 1000) : IQueueProvider<T>
{
    private readonly Channel<T> _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false
    });

    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;

    public bool IsEmpty => Count == 0;

    public async Task<Result<bool, Failure>> EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Item enqueued to channel queue. Current count: {Count}", Count);
            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enqueuing item to channel queue");
            return Result<bool, Failure>.Fail(new Failure(ex.Message, "ENQUEUE_ERROR"));
        }
    }

    public async Task<Result<T?, Failure>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Item dequeued from channel queue. Remaining count: {Count}", Count);
            return Result<T?, Failure>.Ok(item);
        }
        catch (InvalidOperationException)
        {
            logger.LogDebug("Channel queue is empty or completed");
            return Result<T?, Failure>.Ok(default);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dequeuing item from channel queue");
            return Result<T?, Failure>.Fail(new Failure(ex.Message, "DEQUEUE_ERROR"));
        }
    }

    public Task<Result<T?, Failure>> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_channel.Reader.TryRead(out var item))
            {
                logger.LogDebug("Item successfully dequeued from channel queue. Remaining count: {Count}", Count);
                return Task.FromResult(Result<T?, Failure>.Ok(item));
            }
            
            logger.LogDebug("Channel queue is empty, no item to dequeue");
            return Task.FromResult(Result<T?, Failure>.Ok(default));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error trying to dequeue item from channel queue");
            return Task.FromResult(Result<T?, Failure>.Fail(new Failure(ex.Message, "TRY_DEQUEUE_ERROR")));
        }
    }

    public Task<Result<T?, Failure>> PeekAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_channel.Reader.TryPeek(out var item))
            {
                logger.LogDebug("Item peeked from channel queue");
                return Task.FromResult(Result<T?, Failure>.Ok(item));
            }
            
            logger.LogDebug("Channel queue is empty, no item to peek");
            return Task.FromResult(Result<T?, Failure>.Ok(default));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error peeking item from channel queue");
            return Task.FromResult(Result<T?, Failure>.Fail(new Failure(ex.Message, "PEEK_ERROR")));
        }
    }

    public Task<Result<bool, Failure>> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clearedCount = 0;
            while (_channel.Reader.TryRead(out _))
            {
                clearedCount++;
            }
            
            logger.LogInformation("Channel queue cleared, removed {Count} items", clearedCount);
            return Task.FromResult(Result<bool, Failure>.Ok(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing channel queue");
            return Task.FromResult(Result<bool, Failure>.Fail(new Failure(ex.Message, "CLEAR_ERROR")));
        }
    }
}

/// <summary>
/// 支援多個命名佇列的 Channel 實作
/// </summary>
public class ChannelQueueService(ILogger<ChannelQueueService> logger) : IQueueProvider
{
    private readonly ConcurrentDictionary<string, object> _queues = new();
    private readonly ConcurrentDictionary<string, int> _queueCapacities = new();

    private IQueueProvider<T> GetOrCreateQueue<T>(string queueName, int capacity = 1000)
    {
        return (IQueueProvider<T>)_queues.GetOrAdd(queueName, _ =>
        {
            _queueCapacities.TryAdd(queueName, capacity);
            // 使用 NullLogger 避免依賴問題
            var queueLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ChannelQueueService<T>>.Instance;
            return new ChannelQueueService<T>(queueLogger, capacity);
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
            return Result<bool, Failure>.Fail(new Failure(ex.Message, "CLEAR_QUEUE_ERROR"));
        }
    }

    public IEnumerable<string> GetQueueNames()
    {
        return _queues.Keys.ToList();
    }
}