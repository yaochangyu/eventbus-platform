using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Handlers;

public interface ITaskHandler
{
    Task<Result<TaskEntity, Failure>> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaskEntity, Failure>> GetTaskByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<List<TaskEntity>, Failure>> GetTasksByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<Result<List<TaskEntity>, Failure>> GetTasksByStatusAsync(string status, int limit = 100, CancellationToken cancellationToken = default);
    Task<Result<List<TaskEntity>, Failure>> GetPendingTasksAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<Result<TaskEntity, Failure>> UpdateTaskStatusAsync(string id, string status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task<Result<bool, Failure>> ExecuteTaskAsync(string id, CancellationToken cancellationToken = default);
    
    // Scheduled task methods
    Task<Result<List<TaskEntity>, Failure>> GetScheduledTasksReadyForExecutionAsync(DateTime currentTime, int limit = 50, CancellationToken cancellationToken = default);
    Task<Result<TaskEntity, Failure>> UpdateScheduledTaskStatusAsync(string id, UpdateScheduledTaskStatusRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request model for creating a new task
/// Only contains task name and execution data
/// All configuration details are managed through Task Config API
/// </summary>
public record CreateTaskRequest
{
    public string TaskName { get; init; } = string.Empty;
    public string Data { get; init; } = string.Empty;
}

/// <summary>
/// Generic request model for creating a new task with strongly-typed data
/// Provides type safety and better developer experience
/// </summary>
public record CreateTaskRequest<T>
{
    public string TaskName { get; init; } = string.Empty;
    public T Data { get; init; } = default!;
}


/// <summary>
/// Response model for task operations
/// Extended to include execution details for TaskWorkerService
/// </summary>
public record TaskResponse
{
    public string Id { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TraceId { get; init; }
    
    // Execution details needed by TaskWorkerService
    public string CallbackUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public string RequestPayload { get; init; } = string.Empty;
    public Dictionary<string, string>? Headers { get; init; }
    public int MaxRetries { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 30;
    public string? EventId { get; init; }
    public string? SubscriberId { get; init; }
}

/// <summary>
/// Request model for updating task status
/// </summary>
public record UpdateTaskStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Request model for updating scheduled task status
/// Includes next scheduled time for retry logic
/// </summary>
public record UpdateScheduledTaskStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public DateTime? NextScheduledAt { get; init; }
}