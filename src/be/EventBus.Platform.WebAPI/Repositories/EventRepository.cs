using System.Collections.Concurrent;
using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.TraceContext;
using EventBus.Infrastructure.Models;
using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Repositories;

public class EventRepository(
    ICacheService cacheService,
    IContextGetter<TraceContext?> traceContextGetter,
    ILogger<EventRepository> logger) : IEventRepository
{
    private static readonly ConcurrentDictionary<string, EventEntity> _events = new();
    private const string CacheKeyPrefix = "Event_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<Result<EventEntity, Failure>> CreateAsync(EventEntity eventEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();
            
            if (_events.ContainsKey(eventEntity.Id))
            {
                return Result<EventEntity, Failure>.Fail(new Failure(FailureCode.DuplicateId.ToString(), "Event with this ID already exists"));
            }

            _events.TryAdd(eventEntity.Id, eventEntity);
            
            var cacheKey = $"{CacheKeyPrefix}{eventEntity.Id}";
            await cacheService.SetAsync(cacheKey, eventEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Successfully created event: {EventId} - TraceId: {TraceId}", 
                eventEntity.Id, traceContext?.TraceId);

            return Result<EventEntity, Failure>.Ok(eventEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to create event - TraceId: {TraceId}", traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<EventEntity, Failure>.Fail(new Failure(FailureCode.InternalServerError.ToString(), "Failed to create event") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<EventEntity, Failure>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{id}";
            var cachedEvent = await cacheService.GetAsync<EventEntity>(cacheKey, cancellationToken)
                .ConfigureAwait(false);

            if (cachedEvent != null)
            {
                return Result<EventEntity, Failure>.Ok(cachedEvent);
            }

            if (_events.TryGetValue(id, out var eventEntity))
            {
                await cacheService.SetAsync(cacheKey, eventEntity, CacheDuration, cancellationToken)
                    .ConfigureAwait(false);
                return Result<EventEntity, Failure>.Ok(eventEntity);
            }

            return Result<EventEntity, Failure>.Fail(new Failure(FailureCode.NotFound.ToString(), "Event not found"));
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get event by ID: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<EventEntity, Failure>.Fail(new Failure(FailureCode.InternalServerError.ToString(), "Failed to retrieve event") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<EventEntity, Failure>> UpdateAsync(EventEntity eventEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (!_events.ContainsKey(eventEntity.Id))
            {
                return Result<EventEntity, Failure>.Fail(new Failure(FailureCode.NotFound.ToString(), "Event not found"));
            }

            var updatedEntity = eventEntity with { UpdatedAt = DateTime.UtcNow };
            _events.TryUpdate(eventEntity.Id, updatedEntity, _events[eventEntity.Id]);

            var cacheKey = $"{CacheKeyPrefix}{eventEntity.Id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            await cacheService.SetAsync(cacheKey, updatedEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Successfully updated event: {EventId} - TraceId: {TraceId}", 
                eventEntity.Id, traceContext?.TraceId);

            return Result<EventEntity, Failure>.Ok(updatedEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to update event: {EventId} - TraceId: {TraceId}", eventEntity.Id, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<EventEntity, Failure>.Fail(new Failure(FailureCode.InternalServerError.ToString(), "Failed to update event") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<bool, Failure>> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (!_events.TryRemove(id, out _))
            {
                return Result<bool, Failure>.Fail(new Failure("Event not found", "NotFound"));
            }

            var cacheKey = $"{CacheKeyPrefix}{id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Successfully deleted event: {EventId} - TraceId: {TraceId}", 
                id, traceContext?.TraceId);

            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to delete event: {EventId} - TraceId: {TraceId}", id, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<bool, Failure>.Fail(new Failure(FailureCode.InternalServerError.ToString(), "Failed to delete event") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<List<EventEntity>, Failure>> GetByTypeAsync(string eventType, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            var events = _events.Values
                .Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToList();

            return Result<List<EventEntity>, Failure>.Ok(events);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get events by type: {EventType} - TraceId: {TraceId}", eventType, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<List<EventEntity>, Failure>.Fail(new Failure(FailureCode.InternalServerError.ToString(), "Failed to retrieve events by type") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<List<EventEntity>, Failure>> GetByStatusAsync(string status, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            var events = _events.Values
                .Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToList();

            return Result<List<EventEntity>, Failure>.Ok(events);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get events by status: {Status} - TraceId: {TraceId}", status, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<List<EventEntity>, Failure>.Fail(new Failure(FailureCode.InternalServerError.ToString(), "Failed to retrieve events by status") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<List<EventEntity>, Failure>> GetPendingEventsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            var pendingEvents = _events.Values
                .Where(e => e.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) || 
                           e.Status.Equals("Published", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.CreatedAt)
                .Take(limit)
                .ToList();

            return Result<List<EventEntity>, Failure>.Ok(pendingEvents);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get pending events - TraceId: {TraceId}", traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<List<EventEntity>, Failure>.Fail(new Failure(FailureCode.InternalServerError.ToString(), "Failed to retrieve pending events") { Exception = ex, TraceId = traceId });
        }
    }
}