using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Collections.Concurrent;

namespace EventBus.Infrastructure.Caching;

public class MemoryCacheProvider(
    IMemoryCache memoryCache, 
    ILogger<MemoryCacheProvider> logger,
    IOptions<CacheOptions> cacheOptions) : ICacheProvider
{
    private readonly ConcurrentDictionary<string, DateTime> _keyTracker = new();
    private readonly CacheOptions _options = cacheOptions.Value;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (memoryCache.TryGetValue(key, out var value))
            {
                logger.LogDebug("Cache hit for key: {Key}", key);
                
                if (value is T directValue)
                    return Task.FromResult<T?>(directValue);

                if (value is string json && typeof(T) != typeof(string))
                {
                    var deserializedValue = JsonSerializer.Deserialize<T>(json);
                    return Task.FromResult(deserializedValue);
                }
                
                return Task.FromResult((T?)value);
            }

            logger.LogDebug("Cache miss for key: {Key}", key);
            return Task.FromResult<T?>(default);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            
            if (expiry.HasValue)
                options.AbsoluteExpirationRelativeToNow = expiry.Value;
            else
                options.AbsoluteExpirationRelativeToNow = _options.DefaultExpiration;

            options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
            {
                EvictionCallback = (key, value, reason, state) =>
                {
                    _keyTracker.TryRemove(key.ToString() ?? string.Empty, out _);
                }
            });

            memoryCache.Set(key, value, options);
            _keyTracker.TryAdd(key, DateTime.UtcNow.Add(options.AbsoluteExpirationRelativeToNow ?? _options.DefaultExpiration));
            
            if (_options.EnableLogging)
                logger.LogDebug("Cache set for key: {Key}, expiry: {Expiry}", key, expiry ?? _options.DefaultExpiration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            memoryCache.Remove(key);
            _keyTracker.TryRemove(key, out _);
            logger.LogDebug("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = memoryCache.TryGetValue(key, out _);
            logger.LogDebug("Cache existence check for key: {Key} = {Exists}", key, exists);
            return Task.FromResult(exists);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
            return Task.FromResult(false);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var keysToRemove = _keyTracker.Keys.ToList();
            foreach (var key in keysToRemove)
            {
                memoryCache.Remove(key);
                _keyTracker.TryRemove(key, out _);
            }
            
            logger.LogInformation("Cache cleared, removed {Count} keys", keysToRemove.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing cache");
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var cachedValue = await GetAsync<T>(key, cancellationToken);
        if (cachedValue != null)
            return cachedValue;

        logger.LogDebug("Cache miss for key: {Key}, executing factory", key);
        
        var value = await factory().ConfigureAwait(false);
        await SetAsync(key, value, expiry, cancellationToken);
        
        return value;
    }

    public Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var tasks = items.Select(kvp => SetAsync(kvp.Key, kvp.Value, expiry, cancellationToken));
        return Task.WhenAll(tasks);
    }

    public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, T?>();
        var tasks = keys.Select(async key => new { Key = key, Value = await GetAsync<T>(key, cancellationToken) });
        
        var results = await Task.WhenAll(tasks);
        
        foreach (var item in results)
        {
            result[item.Key] = item.Value;
        }
        
        return result;
    }

    public Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var tasks = keys.Select(key => RemoveAsync(key, cancellationToken));
        return Task.WhenAll(tasks);
    }
}