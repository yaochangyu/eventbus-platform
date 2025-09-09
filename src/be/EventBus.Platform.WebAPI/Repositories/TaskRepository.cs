using System.Collections.Concurrent;
using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.TraceContext;
using EventBus.Infrastructure.Models;
using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Repositories;

public class TaskRepository(
    ICacheService cacheService,
    IContextGetter<TraceContext?> traceContextGetter,
    ILogger<TaskRepository> logger) : ITaskRepository
{
    private static readonly ConcurrentDictionary<string, TaskEntity> _tasks = new();
    private const string CacheKeyPrefix = "Task_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<Result<TaskEntity, Failure>> CreateAsync(TaskEntity taskEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (_tasks.ContainsKey(taskEntity.Id))
            {
                return Result<TaskEntity, Failure>.Fail(new Failure("Task with this ID already exists", "DuplicateId"));
            }

            _tasks.TryAdd(taskEntity.Id, taskEntity);

            var cacheKey = $"{CacheKeyPrefix}{taskEntity.Id}";
            await cacheService.SetAsync(cacheKey, taskEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Successfully created task: {TaskId} - TraceId: {TraceId}",
                taskEntity.Id, traceContext?.TraceId);

            return Result<TaskEntity, Failure>.Ok(taskEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to create task - TraceId: {TraceId}", traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<TaskEntity, Failure>.Fail(new Failure("InternalError", "Failed to create task") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<TaskEntity, Failure>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();

        try
        {
            var cacheKey = $"{CacheKeyPrefix}{id}";
            var cachedTask = await cacheService.GetAsync<TaskEntity>(cacheKey, cancellationToken)
                .ConfigureAwait(false);

            if (cachedTask != null)
            {
                return Result<TaskEntity, Failure>.Ok(cachedTask);
            }

            if (_tasks.TryGetValue(id, out var taskEntity))
            {
                await cacheService.SetAsync(cacheKey, taskEntity, CacheDuration, cancellationToken)
                    .ConfigureAwait(false);
                return Result<TaskEntity, Failure>.Ok(taskEntity);
            }

            return Result<TaskEntity, Failure>.Fail(new Failure("Task not found", "NotFound"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get task by ID: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<TaskEntity, Failure>.Fail(new Failure("InternalError", "Failed to retrieve task") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<TaskEntity, Failure>> UpdateAsync(TaskEntity taskEntity, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();
        try
        {

            if (!_tasks.ContainsKey(taskEntity.Id))
            {
                return Result<TaskEntity, Failure>.Fail(new Failure("Task not found", "NotFound"));
            }

            _tasks.TryUpdate(taskEntity.Id, taskEntity, _tasks[taskEntity.Id]);

            var cacheKey = $"{CacheKeyPrefix}{taskEntity.Id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            await cacheService.SetAsync(cacheKey, taskEntity, CacheDuration, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Successfully updated task: {TaskId} - TraceId: {TraceId}",
                taskEntity.Id, traceContext?.TraceId);

            return Result<TaskEntity, Failure>.Ok(taskEntity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update task: {TaskId} - TraceId: {TraceId}", taskEntity.Id, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<TaskEntity, Failure>.Fail(new Failure("InternalError", "Failed to update task") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<bool, Failure>> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();
        try
        {

            if (!_tasks.TryRemove(id, out _))
            {
                return Result<bool, Failure>.Fail(new Failure("Task not found", "NotFound"));
            }

            var cacheKey = $"{CacheKeyPrefix}{id}";
            await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Successfully deleted task: {TaskId} - TraceId: {TraceId}",
                id, traceContext?.TraceId);

            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete task: {TaskId} - TraceId: {TraceId}", id, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<bool, Failure>.Fail(new Failure("InternalError", "Failed to delete task") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();

        try
        {
            await Task.CompletedTask;
            var tasks = _tasks.Values
                .Where(t => t.EventId.Equals(eventId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            return Result<List<TaskEntity>, Failure>.Ok(tasks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get tasks by event ID: {EventId} - TraceId: {TraceId}", eventId, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<List<TaskEntity>, Failure>.Fail(new Failure("InternalError", "Failed to retrieve tasks by event ID") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetByStatusAsync(string status, int limit = 100, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();

        try
        {
            await Task.CompletedTask;
            var tasks = _tasks.Values
                .Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToList();

            return Result<List<TaskEntity>, Failure>.Ok(tasks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get tasks by status: {Status} - TraceId: {TraceId}", status, traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<List<TaskEntity>, Failure>.Fail(new Failure("InternalError", "Failed to retrieve tasks by status") { Exception = ex, TraceId = traceId });
        }
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetPendingTasksAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();

        try
        {
            await Task.CompletedTask;
            var pendingTasks = _tasks.Values
                .Where(t => t.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.CreatedAt)
                .Take(limit)
                .ToList();

            return Result<List<TaskEntity>, Failure>.Ok(pendingTasks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pending tasks - TraceId: {TraceId}", traceContext?.TraceId);
            var traceId = traceContext?.TraceId;
            return Result<List<TaskEntity>, Failure>.Fail(new Failure("InternalError", "Failed to retrieve pending tasks") { Exception = ex, TraceId = traceId });
        }
    }
}
