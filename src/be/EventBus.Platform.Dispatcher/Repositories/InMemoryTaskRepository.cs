using System.Collections.Concurrent;
using System.Text.Json;
using EventBus.Platform.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.Dispatcher.Repositories;

public class InMemoryTaskRepository(ILogger<InMemoryTaskRepository> logger) : ITaskRepository
{
    private readonly ConcurrentDictionary<string, TaskEntity> _tasks = new();

    public Task CreateTaskAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default)
    {
        var taskEntity = MapToEntity(taskRequest);
        
        _tasks.TryAdd(taskEntity.Id, taskEntity);
        
        logger.LogInformation(
            "Task created in repository: {TaskId} with status {Status} - TraceId: {TraceId}",
            taskEntity.Id,
            taskEntity.Status,
            taskEntity.TraceId);

        return Task.CompletedTask;
    }

    public Task<TaskEntity?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task UpdateTaskStatusAsync(string taskId, MessageStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var existingTask))
        {
            var updatedTask = existingTask with 
            { 
                Status = status,
                ProcessedAt = DateTime.UtcNow,
                ErrorMessage = errorMessage
            };
            
            _tasks.TryUpdate(taskId, updatedTask, existingTask);
            
            logger.LogInformation(
                "Task status updated: {TaskId} from {OldStatus} to {NewStatus} - TraceId: {TraceId}",
                taskId,
                existingTask.Status,
                status,
                existingTask.TraceId);
        }
        else
        {
            logger.LogWarning("Task not found for status update: {TaskId}", taskId);
        }

        return Task.CompletedTask;
    }

    public Task<List<TaskEntity>> GetPendingTasksAsync(CancellationToken cancellationToken = default)
    {
        var pendingTasks = _tasks.Values
            .Where(t => t.Status == MessageStatus.Pending)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        return Task.FromResult(pendingTasks);
    }

    private static TaskEntity MapToEntity(TaskRequest request)
    {
        return new TaskEntity
        {
            Id = request.TaskId,
            CallbackUrl = request.CallbackUrl,
            Method = request.Method.Method,
            RequestPayload = JsonSerializer.Serialize(request.RequestPayload),
            Headers = JsonSerializer.Serialize(request.Headers),
            MaxRetries = request.MaxRetries,
            TimeoutSeconds = (int)request.Timeout.TotalSeconds,
            TraceId = request.TraceId,
            CreatedAt = request.CreatedAt,
            Status = MessageStatus.Pending
        };
    }
}