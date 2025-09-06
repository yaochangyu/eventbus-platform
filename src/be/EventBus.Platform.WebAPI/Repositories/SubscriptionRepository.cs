using System.Collections.Concurrent;
using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.TraceContext;
using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Repositories;

public class SubscriptionRepository(
    ICacheService cacheService,
    IContextGetter<TraceContext?> traceContextGetter,
    ILogger<SubscriptionRepository> logger) : ISubscriptionRepository
{
    private static readonly ConcurrentDictionary<string, SubscriptionEntity> _subscriptions = new();
    private const string CacheKeyPrefix = "Subscription_";
    private const string CacheEventTypePrefix = "SubscriptionsByEventType_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<Result<SubscriptionEntity, Failure>> CreateAsync(SubscriptionEntity subscriptionEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();
            
            if (_subscriptions.ContainsKey(subscriptionEntity.Id))
            {
                return Result<SubscriptionEntity, Failure>.Fail(new Failure("Subscription with this ID already exists", "DuplicateId"));
            }

            _subscriptions.TryAdd(subscriptionEntity.Id, subscriptionEntity);
            
            var cacheKey = $"{CacheKeyPrefix}{subscriptionEntity.Id}";
            await cacheService.SetAsync(cacheKey, subscriptionEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            var eventTypeCacheKey = $"{CacheEventTypePrefix}{subscriptionEntity.EventType}";
            await cacheService.RemoveAsync(eventTypeCacheKey, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Successfully created subscription: {SubscriptionId} for event type: {EventType} - TraceId: {TraceId}", 
                subscriptionEntity.Id, subscriptionEntity.EventType, traceContext?.TraceId);

            return Result<SubscriptionEntity, Failure>.Ok(subscriptionEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to create subscription - TraceId: {TraceId}", traceContext?.TraceId);
            return Result<SubscriptionEntity, Failure>.Fail(new Failure("Failed to create subscription", "InternalError"));
        }
    }

    public async Task<Result<SubscriptionEntity, Failure>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{id}";
            var cachedSubscription = await cacheService.GetAsync<SubscriptionEntity>(cacheKey, cancellationToken)
                .ConfigureAwait(false);

            if (cachedSubscription != null)
            {
                return Result<SubscriptionEntity, Failure>.Ok(cachedSubscription);
            }

            if (_subscriptions.TryGetValue(id, out var subscriptionEntity))
            {
                await cacheService.SetAsync(cacheKey, subscriptionEntity, CacheDuration, cancellationToken)
                    .ConfigureAwait(false);
                return Result<SubscriptionEntity, Failure>.Ok(subscriptionEntity);
            }

            return Result<SubscriptionEntity, Failure>.Fail(new Failure("Subscription not found", "NotFound"));
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get subscription by ID: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<SubscriptionEntity, Failure>.Fail(new Failure("Failed to retrieve subscription", "InternalError"));
        }
    }

    public async Task<Result<SubscriptionEntity, Failure>> UpdateAsync(SubscriptionEntity subscriptionEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (!_subscriptions.ContainsKey(subscriptionEntity.Id))
            {
                return Result<SubscriptionEntity, Failure>.Fail(new Failure("Subscription not found", "NotFound"));
            }

            var oldSubscription = _subscriptions[subscriptionEntity.Id];
            _subscriptions.TryUpdate(subscriptionEntity.Id, subscriptionEntity, oldSubscription);

            var cacheKey = $"{CacheKeyPrefix}{subscriptionEntity.Id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            await cacheService.SetAsync(cacheKey, subscriptionEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            if (!oldSubscription.EventType.Equals(subscriptionEntity.EventType, StringComparison.OrdinalIgnoreCase))
            {
                var oldEventTypeCacheKey = $"{CacheEventTypePrefix}{oldSubscription.EventType}";
                await cacheService.RemoveAsync(oldEventTypeCacheKey, cancellationToken).ConfigureAwait(false);
            }

            var eventTypeCacheKey = $"{CacheEventTypePrefix}{subscriptionEntity.EventType}";
            await cacheService.RemoveAsync(eventTypeCacheKey, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Successfully updated subscription: {SubscriptionId} - TraceId: {TraceId}", 
                subscriptionEntity.Id, traceContext?.TraceId);

            return Result<SubscriptionEntity, Failure>.Ok(subscriptionEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to update subscription: {SubscriptionId} - TraceId: {TraceId}", subscriptionEntity.Id, traceContext?.TraceId);
            return Result<SubscriptionEntity, Failure>.Fail(new Failure("Failed to update subscription", "InternalError"));
        }
    }

    public async Task<Result<bool, Failure>> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (!_subscriptions.TryGetValue(id, out var subscription))
            {
                return Result<bool, Failure>.Fail(new Failure("Subscription not found", "NotFound"));
            }

            if (!_subscriptions.TryRemove(id, out _))
            {
                return Result<bool, Failure>.Fail(new Failure("Failed to remove subscription", "InternalError"));
            }

            var cacheKey = $"{CacheKeyPrefix}{id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

            var eventTypeCacheKey = $"{CacheEventTypePrefix}{subscription.EventType}";
            await cacheService.RemoveAsync(eventTypeCacheKey, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Successfully deleted subscription: {SubscriptionId} - TraceId: {TraceId}", 
                id, traceContext?.TraceId);

            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to delete subscription: {SubscriptionId} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<bool, Failure>.Fail(new Failure("Failed to delete subscription", "InternalError"));
        }
    }

    public async Task<Result<List<SubscriptionEntity>, Failure>> GetByEventTypeAsync(string eventType, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{CacheEventTypePrefix}{eventType}";
            var cachedSubscriptions = await cacheService.GetAsync<List<SubscriptionEntity>>(cacheKey, cancellationToken)
                .ConfigureAwait(false);

            if (cachedSubscriptions != null)
            {
                return Result<List<SubscriptionEntity>, Failure>.Ok(cachedSubscriptions);
            }

            var subscriptions = _subscriptions.Values
                .Where(s => s.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase) && s.IsActive)
                .OrderBy(s => s.CreatedAt)
                .ToList();

            await cacheService.SetAsync(cacheKey, subscriptions, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            return Result<List<SubscriptionEntity>, Failure>.Ok(subscriptions);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get subscriptions by event type: {EventType} - TraceId: {TraceId}", eventType, traceContext?.TraceId);
            return Result<List<SubscriptionEntity>, Failure>.Fail(new Failure("Failed to retrieve subscriptions by event type", "InternalError"));
        }
    }

    public async Task<Result<List<SubscriptionEntity>, Failure>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            var activeSubscriptions = _subscriptions.Values
                .Where(s => s.IsActive)
                .OrderBy(s => s.CreatedAt)
                .ToList();

            return Result<List<SubscriptionEntity>, Failure>.Ok(activeSubscriptions);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get active subscriptions - TraceId: {TraceId}", traceContext?.TraceId);
            return Result<List<SubscriptionEntity>, Failure>.Fail(new Failure("Failed to retrieve active subscriptions", "InternalError"));
        }
    }

    public async Task<Result<SubscriptionEntity, Failure>> GetByEventTypeAndSubscriberAsync(string eventType, string subscriberName, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            var subscription = _subscriptions.Values
                .FirstOrDefault(s => s.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase) && 
                                    s.SubscriberName.Equals(subscriberName, StringComparison.OrdinalIgnoreCase) &&
                                    s.IsActive);

            if (subscription == null)
            {
                return Result<SubscriptionEntity, Failure>.Fail(new Failure("Subscription not found", "NotFound"));
            }

            return Result<SubscriptionEntity, Failure>.Ok(subscription);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get subscription by event type and subscriber: {EventType}, {SubscriberName} - TraceId: {TraceId}", 
                eventType, subscriberName, traceContext?.TraceId);
            return Result<SubscriptionEntity, Failure>.Fail(new Failure("Failed to retrieve subscription", "InternalError"));
        }
    }
}