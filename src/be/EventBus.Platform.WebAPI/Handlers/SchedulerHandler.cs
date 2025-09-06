using EventBus.Infrastructure.TraceContext;
using EventBus.Platform.WebAPI.Models;
using EventBus.Platform.WebAPI.Repositories;
using System.Collections.Concurrent;

namespace EventBus.Platform.WebAPI.Handlers;

public class SchedulerHandler(
    ISchedulerTaskRepository schedulerTaskRepository,
    IContextGetter<TraceContext?> traceContextGetter,
    ILogger<SchedulerHandler> logger) : ISchedulerHandler
{
    private static readonly ConcurrentDictionary<string, Timer> _scheduledTimers = new();

    public async Task<Result<SchedulerTaskEntity, Failure>> CreateSchedulerTaskAsync(CreateSchedulerTaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();
            
            // Validate request
            if (string.IsNullOrWhiteSpace(request.TaskType))
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("TaskType is required", "ValidationError"));
            }

            if (string.IsNullOrWhiteSpace(request.Payload))
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Payload is required", "ValidationError"));
            }

            if (request.ScheduledAt <= DateTime.UtcNow)
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("ScheduledAt must be in the future", "ValidationError"));
            }

            // Create scheduler task entity
            var schedulerTaskEntity = new SchedulerTaskEntity
            {
                Id = Guid.NewGuid().ToString(),
                TaskType = request.TaskType,
                Payload = request.Payload,
                ScheduledAt = request.ScheduledAt,
                Priority = request.Priority,
                Status = "Scheduled"
            };

            // Save to repository
            var createResult = await schedulerTaskRepository.CreateAsync(schedulerTaskEntity, cancellationToken);
            if (!createResult.IsSuccess)
            {
                logger.LogError("Failed to create scheduler task - TraceId: {TraceId}, Error: {Error}", 
                    traceContext?.TraceId, createResult.Failure?.Message);
                return createResult;
            }

            // Schedule the task for execution
            ScheduleTaskExecution(schedulerTaskEntity);

            logger.LogInformation("Successfully created scheduler task: {TaskId}, scheduled for: {ScheduledAt} - TraceId: {TraceId}", 
                schedulerTaskEntity.Id, schedulerTaskEntity.ScheduledAt, traceContext?.TraceId);

            return Result<SchedulerTaskEntity, Failure>.Ok(schedulerTaskEntity);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to create scheduler task - TraceId: {TraceId}", traceContext?.TraceId);
            return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Failed to create scheduler task", "InternalError"));
        }
    }

    public async Task<Result<List<SchedulerTaskEntity>, Failure>> GetScheduledTasksAsync(DateTime beforeTime, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await schedulerTaskRepository.GetScheduledTasksAsync(beforeTime, limit, cancellationToken);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get scheduled tasks - TraceId: {TraceId}", traceContext?.TraceId);
            return Result<List<SchedulerTaskEntity>, Failure>.Fail(new Failure("Failed to get scheduled tasks", "InternalError"));
        }
    }

    public async Task<Result<SchedulerTaskEntity, Failure>> GetSchedulerTaskByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Task ID is required", "ValidationError"));
            }

            return await schedulerTaskRepository.GetByIdAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to get scheduler task by ID: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Failed to get scheduler task", "InternalError"));
        }
    }

    public async Task<Result<SchedulerTaskEntity, Failure>> UpdateSchedulerTaskAsync(string id, UpdateSchedulerTaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Task ID is required", "ValidationError"));
            }

            // Get existing task
            var getResult = await schedulerTaskRepository.GetByIdAsync(id, cancellationToken);
            if (!getResult.IsSuccess)
            {
                return Result<SchedulerTaskEntity, Failure>.Fail(getResult.Failure!);
            }

            var existingTask = getResult.Success!;

            // Update only provided fields
            var updatedTask = existingTask with
            {
                TaskType = request.TaskType ?? existingTask.TaskType,
                Payload = request.Payload ?? existingTask.Payload,
                ScheduledAt = request.ScheduledAt ?? existingTask.ScheduledAt,
                Priority = request.Priority ?? existingTask.Priority,
                Status = request.Status ?? existingTask.Status
            };

            // Update in repository
            var updateResult = await schedulerTaskRepository.UpdateAsync(updatedTask, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                return updateResult;
            }

            // Reschedule if needed
            if (request.ScheduledAt.HasValue && updatedTask.Status == "Scheduled")
            {
                CancelScheduledTask(id);
                ScheduleTaskExecution(updatedTask);
            }

            logger.LogInformation("Successfully updated scheduler task: {TaskId} - TraceId: {TraceId}", 
                id, traceContext?.TraceId);

            return updateResult;
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to update scheduler task: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<SchedulerTaskEntity, Failure>.Fail(new Failure("Failed to update scheduler task", "InternalError"));
        }
    }

    public async Task<Result<bool, Failure>> DeleteSchedulerTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<bool, Failure>.Fail(new Failure("Task ID is required", "ValidationError"));
            }

            // Cancel scheduled timer if exists
            CancelScheduledTask(id);

            // Delete from repository
            var deleteResult = await schedulerTaskRepository.DeleteAsync(id, cancellationToken);
            if (!deleteResult.IsSuccess)
            {
                return deleteResult;
            }

            logger.LogInformation("Successfully deleted scheduler task: {TaskId} - TraceId: {TraceId}", 
                id, traceContext?.TraceId);

            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to delete scheduler task: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<bool, Failure>.Fail(new Failure("Failed to delete scheduler task", "InternalError"));
        }
    }

    public async Task<Result<bool, Failure>> ExecuteSchedulerTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();

            // Get the task
            var getResult = await schedulerTaskRepository.GetByIdAsync(id, cancellationToken);
            if (!getResult.IsSuccess)
            {
                return Result<bool, Failure>.Fail(getResult.Failure!);
            }

            var task = getResult.Success!;

            if (task.Status != "Scheduled")
            {
                logger.LogWarning("Attempted to execute non-scheduled task: {TaskId}, Status: {Status} - TraceId: {TraceId}", 
                    id, task.Status, traceContext?.TraceId);
                return Result<bool, Failure>.Fail(new Failure($"Task is not in scheduled status: {task.Status}", "InvalidOperation"));
            }

            // Update task status to executing
            var executingTask = task with 
            { 
                Status = "Executing", 
                ExecutedAt = DateTime.UtcNow 
            };

            var updateResult = await schedulerTaskRepository.UpdateAsync(executingTask, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                return Result<bool, Failure>.Fail(updateResult.Failure!);
            }

            logger.LogInformation("Started executing scheduler task: {TaskId}, Type: {TaskType} - TraceId: {TraceId}", 
                id, task.TaskType, traceContext?.TraceId);

            // TODO: Here you would implement the actual task execution logic
            // For now, we'll just simulate execution and mark as completed
            await Task.Delay(100, cancellationToken); // Simulate some work

            // Update task status to completed
            var completedTask = executingTask with { Status = "Completed" };
            var completeResult = await schedulerTaskRepository.UpdateAsync(completedTask, cancellationToken);
            if (!completeResult.IsSuccess)
            {
                // Log error but don't fail the execution
                logger.LogError("Failed to mark task as completed: {TaskId} - TraceId: {TraceId}", 
                    id, traceContext?.TraceId);
            }

            // Remove from scheduled timers
            CancelScheduledTask(id);

            logger.LogInformation("Successfully executed scheduler task: {TaskId} - TraceId: {TraceId}", 
                id, traceContext?.TraceId);

            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Failed to execute scheduler task: {Id} - TraceId: {TraceId}", id, traceContext?.TraceId);

            // Try to mark task as failed
            try
            {
                var getResult = await schedulerTaskRepository.GetByIdAsync(id, cancellationToken);
                if (getResult.IsSuccess)
                {
                    var failedTask = getResult.Success! with 
                    { 
                        Status = "Failed", 
                        ErrorMessage = ex.Message 
                    };
                    await schedulerTaskRepository.UpdateAsync(failedTask, cancellationToken);
                }
            }
            catch (Exception updateEx)
            {
                logger.LogError(updateEx, "Failed to mark task as failed: {Id} - TraceId: {TraceId}", 
                    id, traceContext?.TraceId);
            }

            return Result<bool, Failure>.Fail(new Failure("Failed to execute scheduler task", "InternalError"));
        }
    }

    private void ScheduleTaskExecution(SchedulerTaskEntity task)
    {
        var delay = task.ScheduledAt - DateTime.UtcNow;
        if (delay <= TimeSpan.Zero)
        {
            // Execute immediately if the time has already passed
            _ = Task.Run(async () => await ExecuteSchedulerTaskAsync(task.Id));
            return;
        }

        var timer = new Timer(async _ =>
        {
            try
            {
                await ExecuteSchedulerTaskAsync(task.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Timer-triggered execution failed for task: {TaskId}", task.Id);
            }
        }, null, delay, Timeout.InfiniteTimeSpan);

        _scheduledTimers.TryAdd(task.Id, timer);

        logger.LogDebug("Scheduled task {TaskId} for execution in {DelayMs}ms", 
            task.Id, delay.TotalMilliseconds);
    }

    private void CancelScheduledTask(string taskId)
    {
        if (_scheduledTimers.TryRemove(taskId, out var timer))
        {
            timer.Dispose();
            logger.LogDebug("Cancelled scheduled execution for task: {TaskId}", taskId);
        }
    }
}