using System.Collections.Concurrent;
using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.TraceContext;
using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Repositories;

public class SchedulerTaskRepository(
    ICacheService cacheService,
    IContextGetter<TraceContext?> traceContextGetter,
    ILogger<SchedulerTaskRepository> logger) : ISchedulerTaskRepository
{
    private static readonly ConcurrentDictionary<string, SchedulerTaskEntity> _schedulerTasks = new();
    private const string CacheKeyPrefix = "SchedulerTask_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<Result<SchedulerTaskEntity, Failure>> CreateAsync(SchedulerTaskEntity taskEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();
            
            if (_schedulerTasks.ContainsKey(taskEntity.Id))
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Scheduler task with this ID already exists", "DuplicateId"));
            }

            _schedulerTasks.TryAdd(taskEntity.Id, taskEntity);
            
            var cacheKey = $"{CacheKeyPrefix}{taskEntity.Id}";
            await cacheService.SetAsync(cacheKey, taskEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Successfully created scheduler task: {TaskId} - TraceId: {TraceId}", 
                taskEntity.Id, traceContext?.TraceId);

            return Result<SchedulerTaskEntity, Failure>.Ok(taskEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to create scheduler task - TraceId: {TraceId}", traceContext?.TraceId);
            return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Failed to create scheduler task", "InternalError"));
        }
    }

    public async Task<Result<SchedulerTaskEntity, Failure>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{id}";
            var cachedTask = await cacheService.GetAsync<SchedulerTaskEntity>(cacheKey, cancellationToken)
                .ConfigureAwait(false);

            if (cachedTask != null)
            {
                return Result<SchedulerTaskEntity, Failure>.Ok(cachedTask);
            }

            if (_schedulerTasks.TryGetValue(id, out var taskEntity))
            {
                await cacheService.SetAsync(cacheKey, taskEntity, CacheDuration, cancellationToken)
                    .ConfigureAwait(false);
                return Result<SchedulerTaskEntity, Failure>.Ok(taskEntity);
            }

            return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Scheduler task not found", "NotFound"));
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get scheduler task by ID: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Failed to retrieve scheduler task", "InternalError"));
        }
    }

    public async Task<Result<SchedulerTaskEntity, Failure>> UpdateAsync(SchedulerTaskEntity taskEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (!_schedulerTasks.ContainsKey(taskEntity.Id))
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Scheduler task not found", "NotFound"));
            }

            _schedulerTasks.TryUpdate(taskEntity.Id, taskEntity, _schedulerTasks[taskEntity.Id]);

            var cacheKey = $"{CacheKeyPrefix}{taskEntity.Id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            await cacheService.SetAsync(cacheKey, taskEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Successfully updated scheduler task: {TaskId} - TraceId: {TraceId}", 
                taskEntity.Id, traceContext?.TraceId);

            return Result<SchedulerTaskEntity, Failure>.Ok(taskEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to update scheduler task: {TaskId} - TraceId: {TraceId}", taskEntity.Id, traceContext?.TraceId);
            return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Failed to update scheduler task", "InternalError"));
        }
    }

    public async Task<Result<bool, Failure>> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (!_schedulerTasks.TryRemove(id, out _))
            {
                return Result<bool, Failure>.Fail(new Failure("Scheduler task not found", "NotFound"));
            }

            var cacheKey = $"{CacheKeyPrefix}{id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Successfully deleted scheduler task: {TaskId} - TraceId: {TraceId}", 
                id, traceContext?.TraceId);

            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to delete scheduler task: {TaskId} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<bool, Failure>.Fail(new Failure("Failed to delete scheduler task", "InternalError"));
        }
    }

    public async Task<Result<List<SchedulerTaskEntity>, Failure>> GetScheduledTasksAsync(DateTime beforeTime, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            var scheduledTasks = _schedulerTasks.Values
                .Where(t => t.ScheduledAt <= beforeTime && 
                           (t.Status.Equals("Scheduled", StringComparison.OrdinalIgnoreCase) ||
                            t.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.ScheduledAt)
                .Take(limit)
                .ToList();

            return Result<List<SchedulerTaskEntity>, Failure>.Ok(scheduledTasks);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get scheduled tasks before: {BeforeTime} - TraceId: {TraceId}", beforeTime, traceContext?.TraceId);
            return Result<List<SchedulerTaskEntity>, Failure>.Fail(new Failure("Failed to retrieve scheduled tasks", "InternalError"));
        }
    }

    public async Task<Result<List<SchedulerTaskEntity>, Failure>> GetByStatusAsync(string status, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            var tasks = _schedulerTasks.Values
                .Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.ScheduledAt)
                .Take(limit)
                .ToList();

            return Result<List<SchedulerTaskEntity>, Failure>.Ok(tasks);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get scheduler tasks by status: {Status} - TraceId: {TraceId}", status, traceContext?.TraceId);
            return Result<List<SchedulerTaskEntity>, Failure>.Fail(new Failure("Failed to retrieve scheduler tasks by status", "InternalError"));
        }
    }
}