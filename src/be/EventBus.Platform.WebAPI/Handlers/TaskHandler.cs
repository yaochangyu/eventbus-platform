using EventBus.Infrastructure.TraceContext;
using EventBus.Platform.WebAPI.Models;
using EventBus.Platform.WebAPI.Repositories;

namespace EventBus.Platform.WebAPI.Handlers;

public class TaskHandler(
    ITaskRepository taskRepository,
    IContextGetter<TraceContext?> traceContextGetter,
    ILogger<TaskHandler> logger) : ITaskHandler
{
    public async Task<Result<TaskEntity, Failure>> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var traceContext = traceContextGetter.GetContext();
            var taskId = Guid.NewGuid().ToString();

            var taskEntity = new TaskEntity
            {
                Id = taskId,
                EventId = request.EventId ?? string.Empty,
                SubscriberId = request.SubscriberId ?? string.Empty,
                CallbackUrl = request.CallbackUrl,
                Method = request.Method,
                RequestPayload = request.RequestPayload,
                Headers = request.Headers,
                MaxRetries = request.MaxRetries,
                TimeoutSeconds = request.TimeoutSeconds,
                TraceId = request.TraceId ?? traceContext?.TraceId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            var result = await taskRepository.CreateAsync(taskEntity, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Task created successfully: {TaskId}, CallbackUrl: {CallbackUrl} - TraceId: {TraceId}",
                    taskId, request.CallbackUrl, taskEntity.TraceId);
            }
            else
            {
                logger.LogError("Failed to create task: {Error} - TraceId: {TraceId}",
                    result.Failure?.Message, taskEntity.TraceId);
            }

            return result;
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Exception in CreateTaskAsync - TraceId: {TraceId}", traceContext?.TraceId);
            return Result<TaskEntity, Failure>.Fail(new Failure("Failed to create task", "InternalError"));
        }
    }

    public async Task<Result<TaskEntity, Failure>> GetTaskByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await taskRepository.GetByIdAsync(id, cancellationToken);
            
            if (!result.IsSuccess)
            {
                logger.LogWarning("Task not found: {TaskId}", id);
            }

            return result;
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Exception in GetTaskByIdAsync: {TaskId} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<TaskEntity, Failure>.Fail(new Failure("Failed to retrieve task", "InternalError"));
        }
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetTasksByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await taskRepository.GetByEventIdAsync(eventId, cancellationToken);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Exception in GetTasksByEventIdAsync: {EventId} - TraceId: {TraceId}", eventId, traceContext?.TraceId);
            return Result<List<TaskEntity>, Failure>.Fail(new Failure("Failed to retrieve tasks by event ID", "InternalError"));
        }
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetTasksByStatusAsync(string status, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await taskRepository.GetByStatusAsync(status, limit, cancellationToken);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Exception in GetTasksByStatusAsync: {Status} - TraceId: {TraceId}", status, traceContext?.TraceId);
            return Result<List<TaskEntity>, Failure>.Fail(new Failure("Failed to retrieve tasks by status", "InternalError"));
        }
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetPendingTasksAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await taskRepository.GetPendingTasksAsync(limit, cancellationToken);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Exception in GetPendingTasksAsync - TraceId: {TraceId}", traceContext?.TraceId);
            return Result<List<TaskEntity>, Failure>.Fail(new Failure("Failed to retrieve pending tasks", "InternalError"));
        }
    }

    public async Task<Result<TaskEntity, Failure>> UpdateTaskStatusAsync(string id, string status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var getResult = await taskRepository.GetByIdAsync(id, cancellationToken);
            if (!getResult.IsSuccess)
            {
                return Result<TaskEntity, Failure>.Fail(getResult.Failure!);
            }

            var existingTask = getResult.Success!;
            var now = DateTime.UtcNow;
            
            var updatedTask = existingTask with
            {
                Status = status,
                ErrorMessage = errorMessage ?? existingTask.ErrorMessage,
                StartedAt = status == "Processing" && existingTask.StartedAt == null ? now : existingTask.StartedAt,
                CompletedAt = (status == "Completed" || status == "Failed") && existingTask.CompletedAt == null ? now : existingTask.CompletedAt,
                RetryCount = status == "Failed" ? existingTask.RetryCount + 1 : existingTask.RetryCount
            };

            var updateResult = await taskRepository.UpdateAsync(updatedTask, cancellationToken);
            
            if (updateResult.IsSuccess)
            {
                logger.LogInformation("Task status updated: {TaskId} -> {Status} - TraceId: {TraceId}",
                    id, status, updatedTask.TraceId);
            }

            return updateResult;
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Exception in UpdateTaskStatusAsync: {TaskId} - TraceId: {TraceId}", id, traceContext?.TraceId);
            return Result<TaskEntity, Failure>.Fail(new Failure("Failed to update task status", "InternalError"));
        }
    }

    public async Task<Result<bool, Failure>> ExecuteTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var getResult = await taskRepository.GetByIdAsync(id, cancellationToken);
            if (!getResult.IsSuccess)
            {
                return Result<bool, Failure>.Fail(getResult.Failure!);
            }

            var task = getResult.Success!;
            
            // Mark task as processing
            var processingResult = await UpdateTaskStatusAsync(id, "Processing", cancellationToken: cancellationToken);
            if (!processingResult.IsSuccess)
            {
                return Result<bool, Failure>.Fail(processingResult.Failure!);
            }

            logger.LogInformation("Task execution initiated: {TaskId}, CallbackUrl: {CallbackUrl} - TraceId: {TraceId}",
                id, task.CallbackUrl, task.TraceId);

            // Note: Actual HTTP execution will be handled by TaskWorkerService
            // This method is for marking task as ready for execution
            return Result<bool, Failure>.Ok(true);
        }
        catch (Exception ex)
        {
            var traceContext = traceContextGetter.GetContext();
            logger.LogError(ex, "Exception in ExecuteTaskAsync: {TaskId} - TraceId: {TraceId}", id, traceContext?.TraceId);
            
            // Try to mark task as failed
            try
            {
                await UpdateTaskStatusAsync(id, "Failed", ex.Message, cancellationToken);
            }
            catch
            {
                // Ignore update errors during exception handling
            }
            
            return Result<bool, Failure>.Fail(new Failure("Failed to execute task", "InternalError"));
        }
    }
}