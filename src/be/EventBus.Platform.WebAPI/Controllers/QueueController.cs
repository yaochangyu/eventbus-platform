using EventBus.Infrastructure.Queue;
using EventBus.Infrastructure.TraceContext;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueueController(
    IQueueService queueService,
    IContextGetter<TraceContext?> traceContextGetter) : ControllerBase
{
    [HttpPost("{queueName}/enqueue")]
    public async Task<IActionResult> Enqueue(string queueName, [FromBody] EnqueueRequest request)
    {
        var traceContext = traceContextGetter.GetContext();
        
        await queueService.EnqueueAsync(request.Item, queueName);
        
        return Ok(new
        {
            QueueName = queueName,
            Item = request.Item,
            QueueCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            EnqueuedAt = DateTime.UtcNow
        });
    }

    [HttpPost("{queueName}/dequeue")]
    public async Task<IActionResult> Dequeue(string queueName)
    {
        var traceContext = traceContextGetter.GetContext();
        
        var item = await queueService.DequeueAsync<object>(queueName);
        
        return Ok(new
        {
            QueueName = queueName,
            Item = item,
            Found = item != null,
            RemainingCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            DequeuedAt = DateTime.UtcNow
        });
    }

    [HttpPost("{queueName}/try-dequeue")]
    public async Task<IActionResult> TryDequeue(string queueName)
    {
        var traceContext = traceContextGetter.GetContext();
        
        var result = await queueService.TryDequeueAsync<object>(queueName);
        
        return Ok(new
        {
            QueueName = queueName,
            Success = result.Success,
            Item = result.Item,
            RemainingCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("{queueName}/peek")]
    public async Task<IActionResult> Peek(string queueName)
    {
        var traceContext = traceContextGetter.GetContext();
        
        var item = await queueService.PeekAsync<object>(queueName);
        
        return Ok(new
        {
            QueueName = queueName,
            Item = item,
            Found = item != null,
            QueueCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("{queueName}/status")]
    public IActionResult GetStatus(string queueName)
    {
        var traceContext = traceContextGetter.GetContext();
        
        return Ok(new
        {
            QueueName = queueName,
            Count = queueService.GetCount(queueName),
            IsEmpty = queueService.IsEmpty(queueName),
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpDelete("{queueName}")]
    public async Task<IActionResult> Clear(string queueName)
    {
        var traceContext = traceContextGetter.GetContext();
        
        var countBeforeClear = queueService.GetCount(queueName);
        await queueService.ClearAsync(queueName);
        
        return Ok(new
        {
            QueueName = queueName,
            ItemsCleared = countBeforeClear,
            TraceId = traceContext?.TraceId,
            ClearedAt = DateTime.UtcNow
        });
    }

    [HttpGet("queues")]
    public IActionResult GetAllQueues()
    {
        var traceContext = traceContextGetter.GetContext();
        var queueNames = queueService.GetQueueNames().ToList();
        
        var queueStatuses = queueNames.Select(name => new
        {
            QueueName = name,
            Count = queueService.GetCount(name),
            IsEmpty = queueService.IsEmpty(name)
        }).ToList();

        return Ok(new
        {
            TotalQueues = queueNames.Count,
            Queues = queueStatuses,
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("{queueName}/batch-enqueue")]
    public async Task<IActionResult> BatchEnqueue(string queueName, [FromBody] BatchEnqueueRequest request)
    {
        var traceContext = traceContextGetter.GetContext();
        
        foreach (var item in request.Items)
        {
            await queueService.EnqueueAsync(item, queueName);
        }
        
        return Ok(new
        {
            QueueName = queueName,
            ItemsEnqueued = request.Items.Count,
            QueueCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            EnqueuedAt = DateTime.UtcNow
        });
    }

    [HttpPost("{queueName}/test-performance")]
    public async Task<IActionResult> TestPerformance(string queueName, [FromBody] QueuePerformanceTestRequest request)
    {
        var traceContext = traceContextGetter.GetContext();
        
        // 清空佇列
        await queueService.ClearAsync(queueName);
        
        // 寫入測試
        var startTime = DateTime.UtcNow;
        var enqueueTasks = new List<Task>();
        
        for (int i = 0; i < request.ItemCount; i++)
        {
            var item = $"perf-test-{i}-{Guid.NewGuid()}";
            enqueueTasks.Add(queueService.EnqueueAsync(item, queueName));
        }
        
        await Task.WhenAll(enqueueTasks);
        var enqueueTime = DateTime.UtcNow - startTime;
        
        // 讀取測試
        startTime = DateTime.UtcNow;
        var dequeueTasks = new List<Task<object?>>();
        
        for (int i = 0; i < request.ItemCount; i++)
        {
            dequeueTasks.Add(queueService.DequeueAsync<object>(queueName));
        }
        
        await Task.WhenAll(dequeueTasks);
        var dequeueTime = DateTime.UtcNow - startTime;
        
        return Ok(new
        {
            QueueName = queueName,
            ItemCount = request.ItemCount,
            EnqueueTimeMs = enqueueTime.TotalMilliseconds,
            DequeueTimeMs = dequeueTime.TotalMilliseconds,
            AvgEnqueueTimeMs = enqueueTime.TotalMilliseconds / request.ItemCount,
            AvgDequeueTimeMs = dequeueTime.TotalMilliseconds / request.ItemCount,
            RemainingCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }
}

public record EnqueueRequest
{
    public object Item { get; init; } = string.Empty;
}

public record BatchEnqueueRequest
{
    public List<object> Items { get; init; } = new();
}

public record QueuePerformanceTestRequest
{
    public int ItemCount { get; init; } = 100;
}