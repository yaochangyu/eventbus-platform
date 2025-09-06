using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.TraceContext;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CacheController(
    ICacheService cacheService, 
    ICacheProvider cacheProvider,
    IContextGetter<TraceContext?> traceContextGetter) : ControllerBase
{
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var traceContext = traceContextGetter.GetContext();
        var value = await cacheService.GetAsync<object>(key);
        
        return Ok(new
        {
            Key = key,
            Value = value,
            Exists = value != null,
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] SetCacheRequest request)
    {
        var traceContext = traceContextGetter.GetContext();
        
        await cacheService.SetAsync(key, request.Value, request.ExpiryInMinutes.HasValue 
            ? TimeSpan.FromMinutes(request.ExpiryInMinutes.Value) 
            : null);
        
        return Ok(new
        {
            Key = key,
            Value = request.Value,
            ExpiryInMinutes = request.ExpiryInMinutes,
            TraceId = traceContext?.TraceId,
            SetAt = DateTime.UtcNow
        });
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        var traceContext = traceContextGetter.GetContext();
        await cacheService.RemoveAsync(key);
        
        return Ok(new
        {
            Key = key,
            Removed = true,
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpHead("{key}")]
    public async Task<IActionResult> Exists(string key)
    {
        var exists = await cacheService.ExistsAsync(key);
        return exists ? Ok() : NotFound();
    }

    [HttpPost("batch")]
    public async Task<IActionResult> SetBatch([FromBody] BatchCacheRequest request)
    {
        var traceContext = traceContextGetter.GetContext();
        
        await cacheProvider.SetManyAsync(
            request.Items, 
            request.ExpiryInMinutes.HasValue 
                ? TimeSpan.FromMinutes(request.ExpiryInMinutes.Value) 
                : null);
        
        return Ok(new
        {
            Count = request.Items.Count,
            ExpiryInMinutes = request.ExpiryInMinutes,
            TraceId = traceContext?.TraceId,
            SetAt = DateTime.UtcNow
        });
    }

    [HttpPost("batch/get")]
    public async Task<IActionResult> GetBatch([FromBody] string[] keys)
    {
        var traceContext = traceContextGetter.GetContext();
        var results = await cacheProvider.GetManyAsync<object>(keys);
        
        return Ok(new
        {
            Results = results,
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("test-performance")]
    public async Task<IActionResult> TestPerformance([FromBody] PerformanceTestRequest request)
    {
        var traceContext = traceContextGetter.GetContext();
        var startTime = DateTime.UtcNow;
        
        // 寫入測試
        var writeTasks = new List<Task>();
        for (int i = 0; i < request.ItemCount; i++)
        {
            var key = $"perf-test-{i}";
            var value = $"test-data-{i}-{Guid.NewGuid()}";
            writeTasks.Add(cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5)));
        }
        
        await Task.WhenAll(writeTasks);
        var writeTime = DateTime.UtcNow - startTime;
        
        // 讀取測試
        startTime = DateTime.UtcNow;
        var readTasks = new List<Task<object?>>();
        for (int i = 0; i < request.ItemCount; i++)
        {
            var key = $"perf-test-{i}";
            readTasks.Add(cacheService.GetAsync<object>(key));
        }
        
        await Task.WhenAll(readTasks);
        var readTime = DateTime.UtcNow - startTime;
        
        return Ok(new
        {
            ItemCount = request.ItemCount,
            WriteTimeMs = writeTime.TotalMilliseconds,
            ReadTimeMs = readTime.TotalMilliseconds,
            AvgWriteTimeMs = writeTime.TotalMilliseconds / request.ItemCount,
            AvgReadTimeMs = readTime.TotalMilliseconds / request.ItemCount,
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }
}

public record SetCacheRequest
{
    public object Value { get; init; } = string.Empty;
    public int? ExpiryInMinutes { get; init; }
}

public record BatchCacheRequest
{
    public Dictionary<string, object> Items { get; init; } = new();
    public int? ExpiryInMinutes { get; init; }
}

public record PerformanceTestRequest
{
    public int ItemCount { get; init; } = 100;
}