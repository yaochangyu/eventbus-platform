using Microsoft.Extensions.Logging;

namespace EventBus.Infrastructure.Caching;

public class MemoryCacheService(ICacheProvider cacheProvider, ILogger<MemoryCacheService> logger) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Getting cache value for key: {Key}", key);
            return await cacheProvider.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MemoryCacheService getting cache value for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Setting cache value for key: {Key}", key);
            await cacheProvider.SetAsync(key, value, expiry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MemoryCacheService setting cache value for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Removing cache value for key: {Key}", key);
            await cacheProvider.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MemoryCacheService removing cache value for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Checking cache existence for key: {Key}", key);
            return await cacheProvider.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MemoryCacheService checking cache existence for key: {Key}", key);
            return false;
        }
    }
}