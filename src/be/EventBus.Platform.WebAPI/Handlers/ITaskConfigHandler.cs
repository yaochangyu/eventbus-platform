using EventBus.Infrastructure.Models;

namespace EventBus.Platform.WebAPI.Handlers;

/// <summary>
/// Interface for managing task configurations
/// Handles the separation of task execution from task configuration
/// </summary>
public interface ITaskConfigHandler
{
    Task<Result<TaskConfig, Failure>> CreateTaskConfigAsync(CreateTaskConfigRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaskConfig, Failure>> CreateTaskConfigAsync<T>(CreateTaskConfigRequest<T> request, CancellationToken cancellationToken = default);
    Task<Result<TaskConfig, Failure>> GetTaskConfigAsync(string taskName, CancellationToken cancellationToken = default);
    Task<Result<TaskConfig, Failure>> UpdateTaskConfigAsync(string taskName, UpdateTaskConfigRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool, Failure>> DeleteTaskConfigAsync(string taskName, CancellationToken cancellationToken = default);
    Task<Result<List<TaskConfig>, Failure>> GetAllTaskConfigsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Task configuration entity containing all task execution settings
/// Separated from task execution to allow reusable configurations
/// </summary>
public record TaskConfig
{
    public string TaskName { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public Dictionary<string, string>? Headers { get; init; }
    public int MaxRetries { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 30;
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    
    // Tracing and context defaults
    public string? DefaultTraceId { get; init; }
    public string? DefaultEventId { get; init; }
    public string? DefaultSubscriberId { get; init; }
    
    // Scheduled execution defaults
    public TimeSpan? DefaultDelay { get; init; }
    public bool AllowScheduling { get; init; } = true;
    public string? DefaultCronExpression { get; init; }
    public bool DefaultIsRecurring { get; init; } = false;
}

/// <summary>
/// Request model for creating a new task configuration
/// Contains all execution and scheduling configuration details
/// Enhanced to include task execution properties from merged request models
/// </summary>
public record CreateTaskConfigRequest
{
    // Task basic properties
    public string TaskId { get; init; } = string.Empty;
    public string TaskName { get; init; } = string.Empty;
    public string Data { get; init; } = string.Empty;
    public string? TraceId { get; init; }
    public string? EventId { get; init; }
    public string? SubscriberId { get; init; }
    
    // Scheduled execution properties
    public DateTime? ScheduledAt { get; init; }
    public TimeSpan? Delay { get; init; }
    public bool IsRecurring { get; init; } = false;
    public string? CronExpression { get; init; }
    
    // Configuration properties
    public string CallbackUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public Dictionary<string, string>? Headers { get; init; }
    public int MaxRetries { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 30;
    public bool IsActive { get; init; } = true;
    
    // Legacy tracing and context fields (for backwards compatibility)
    public string? DefaultTraceId { get; init; }
    public string? DefaultEventId { get; init; }
    public string? DefaultSubscriberId { get; init; }
    
    // Legacy scheduled execution configuration (for backwards compatibility)
    public TimeSpan? DefaultDelay { get; init; }
    public bool AllowScheduling { get; init; } = true;
    public string? DefaultCronExpression { get; init; }
    public bool DefaultIsRecurring { get; init; } = false;
}

/// <summary>
/// Generic request model for creating a new task configuration with strongly-typed data
/// Provides type safety and better developer experience
/// </summary>
public record CreateTaskConfigRequest<T>
{
    // Task basic properties
    public string TaskId { get; init; } = string.Empty;
    public string TaskName { get; init; } = string.Empty;
    public T Data { get; init; } = default!;
    public string? TraceId { get; init; }
    public string? EventId { get; init; }
    public string? SubscriberId { get; init; }
    
    // Scheduled execution properties
    public DateTime? ScheduledAt { get; init; }
    public TimeSpan? Delay { get; init; }
    public bool IsRecurring { get; init; } = false;
    public string? CronExpression { get; init; }
    
    // Configuration properties
    public string CallbackUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public Dictionary<string, string>? Headers { get; init; }
    public int MaxRetries { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 30;
    public bool IsActive { get; init; } = true;
    
    // Legacy tracing and context fields (for backwards compatibility)
    public string? DefaultTraceId { get; init; }
    public string? DefaultEventId { get; init; }
    public string? DefaultSubscriberId { get; init; }
    
    // Legacy scheduled execution configuration (for backwards compatibility)
    public TimeSpan? DefaultDelay { get; init; }
    public bool AllowScheduling { get; init; } = true;
    public string? DefaultCronExpression { get; init; }
    public bool DefaultIsRecurring { get; init; } = false;
}

/// <summary>
/// Request model for updating an existing task configuration
/// </summary>
public record UpdateTaskConfigRequest
{
    public string? CallbackUrl { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public int? MaxRetries { get; init; }
    public int? TimeoutSeconds { get; init; }
    public bool? IsActive { get; init; }
    
    // Scheduled execution defaults
    public TimeSpan? DefaultDelay { get; init; }
    public bool? AllowScheduling { get; init; }
    public string? DefaultCronExpression { get; init; }
}

/// <summary>
/// Response model for task configuration operations
/// </summary>
public record TaskConfigResponse
{
    public string TaskName { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public Dictionary<string, string>? Headers { get; init; }
    public int MaxRetries { get; init; }
    public int TimeoutSeconds { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    
    // Scheduled execution defaults
    public TimeSpan? DefaultDelay { get; init; }
    public bool AllowScheduling { get; init; }
    public string? DefaultCronExpression { get; init; }
}