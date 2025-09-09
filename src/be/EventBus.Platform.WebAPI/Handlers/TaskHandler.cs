using EventBus.Infrastructure.Queue;
using EventBus.Infrastructure.TraceContext;
using EventBus.Infrastructure.Models;
using EventBus.Platform.WebAPI.Repositories;
using System.Text.Json;
using EventBus.Infrastructure;
using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Handlers;

public class TaskHandler(
    IContextGetter<TraceContext?> traceContextGetter,
    ITaskRepository taskRepository,
    IQueueProvider queueService,
    IIdGenerator idGenerator,
    ILogger<TaskHandler> logger) : ITaskHandler
{
    public async Task<Result<TaskEntity, Failure>> CreateTaskAsync(CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var taskId = idGenerator.Generate();

        var taskEntity = new TaskEntity
        {
            Id = taskId,
            TaskName = request.TaskName,
        };

        var enqueueResult = await queueService.EnqueueAsync(request.Data, request.TaskName, cancellationToken);
        if (enqueueResult.IsSuccess == false)
        {
            return Result<TaskEntity, Failure>.Fail(enqueueResult.Failure!);
        }

        var result = await taskRepository.CreateAsync(taskEntity, cancellationToken);
        return result;
    }

    public async Task<Result<TaskEntity, Failure>> GetTaskByIdAsync(string id,
        CancellationToken cancellationToken = default)
    {
        var result = await taskRepository.GetByIdAsync(id, cancellationToken);
        return result;
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetTasksByEventIdAsync(string eventId,
        CancellationToken cancellationToken = default)
    {
        return await taskRepository.GetByEventIdAsync(eventId, cancellationToken);
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetTasksByStatusAsync(string status, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await taskRepository.GetByStatusAsync(status, limit, cancellationToken);
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetPendingTasksAsync(int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await taskRepository.GetPendingTasksAsync(limit, cancellationToken);
    }

    public async Task<Result<TaskEntity, Failure>> UpdateTaskStatusAsync(string id, string status,
        string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var getResult = await taskRepository.GetByIdAsync(id, cancellationToken);
        if (getResult.IsSuccess == false)
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
            CompletedAt = (status == "Completed" || status == "Failed") && existingTask.CompletedAt == null
                ? now
                : existingTask.CompletedAt,
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

    public async Task<Result<bool, Failure>> ExecuteTaskAsync(string id, CancellationToken cancellationToken = default)
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

        // Note: Actual HTTP execution will be handled by TaskWorkerService
        // This method is for marking task as ready for execution
        return Result<bool, Failure>.Ok(true);
    }

    public async Task<Result<List<TaskEntity>, Failure>> GetScheduledTasksReadyForExecutionAsync(DateTime currentTime,
        int limit = 50, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();
        logger.LogInformation(
            "Getting scheduled tasks ready for execution. CurrentTime: {CurrentTime}, Limit: {Limit}, TraceId: {TraceId}",
            currentTime, limit, traceContext?.TraceId);

        // Query tasks that are scheduled and ready for execution
        var result = await taskRepository.GetByStatusAsync("Scheduled", limit, cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<List<TaskEntity>, Failure>.Fail(result.Failure!);
        }

        // Filter tasks that are ready for execution (scheduled time <= current time)
        var readyTasks = result.Success!
            .Where(task => task.ScheduledAt.HasValue && task.ScheduledAt.Value <= currentTime)
            .ToList();

        logger.LogInformation("Found {Count} scheduled tasks ready for execution", readyTasks.Count);
        return Result<List<TaskEntity>, Failure>.Ok(readyTasks);
    }

    public async Task<Result<TaskEntity, Failure>> UpdateScheduledTaskStatusAsync(string id,
        UpdateScheduledTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var traceContext = traceContextGetter.GetContext();
        logger.LogInformation(
            "Updating scheduled task {TaskId} status to {Status} with NextScheduledAt: {NextScheduledAt}. TraceId: {TraceId}",
            id, request.Status, request.NextScheduledAt, traceContext?.TraceId);

        // Get existing task first
        var taskResult = await taskRepository.GetByIdAsync(id, cancellationToken);
        if (!taskResult.IsSuccess)
        {
            return Result<TaskEntity, Failure>.Fail(taskResult.Failure!);
        }

        var task = taskResult.Success!;

        // Update task properties
        task.Status = request.Status;
        task.ErrorMessage = request.ErrorMessage;

        // Update scheduled time if provided (for retry logic)
        if (request.NextScheduledAt.HasValue)
        {
            task.ScheduledAt = request.NextScheduledAt.Value;
        }

        // Update timestamps based on status
        switch (request.Status.ToLowerInvariant())
        {
            case "processing":
                task.StartedAt = DateTime.UtcNow;
                break;
            case "completed":
            case "failed":
                task.CompletedAt = DateTime.UtcNow;
                break;
            case "scheduled":
                // For retry scenarios, increment retry count
                if (!string.IsNullOrEmpty(request.ErrorMessage) && request.ErrorMessage.StartsWith("Retry"))
                {
                    task.RetryCount++;
                }

                break;
        }

        // Update task in repository
        var updateResult = await taskRepository.UpdateAsync(task, cancellationToken);
        if (!updateResult.IsSuccess)
        {
            return Result<TaskEntity, Failure>.Fail(updateResult.Failure!);
        }

        return Result<TaskEntity, Failure>.Ok(task);
    }
}