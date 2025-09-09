using EventBus.Infrastructure.Models;
using System.Text.Json;

namespace EventBus.Platform.WebAPI.Handlers;

/// <summary>
/// Handler for task configuration management
/// Implements CRUD operations for task configurations
/// </summary>
public class TaskConfigHandler : ITaskConfigHandler
{
    private readonly ILogger<TaskConfigHandler> _logger;
    private readonly Dictionary<string, TaskConfig> _taskConfigs = new(); // In-memory storage for MVP

    public TaskConfigHandler(ILogger<TaskConfigHandler> logger)
    {
        _logger = logger;
    }

    public async Task<Result<TaskConfig, Failure>> CreateTaskConfigAsync(CreateTaskConfigRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_taskConfigs.ContainsKey(request.TaskName))
            {
                return Result<TaskConfig, Failure>.Fail(new Failure($"Task configuration already exists: {request.TaskName}", "ConfigExists"));
            }

            var taskConfig = new TaskConfig
            {
                TaskName = request.TaskName,
                CallbackUrl = request.CallbackUrl,
                Method = request.Method,
                Headers = request.Headers,
                MaxRetries = request.MaxRetries,
                TimeoutSeconds = request.TimeoutSeconds,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                DefaultTraceId = request.DefaultTraceId,
                DefaultEventId = request.DefaultEventId,
                DefaultSubscriberId = request.DefaultSubscriberId,
                DefaultDelay = request.DefaultDelay,
                AllowScheduling = request.AllowScheduling,
                DefaultCronExpression = request.DefaultCronExpression,
                DefaultIsRecurring = request.DefaultIsRecurring
            };

            _taskConfigs[request.TaskName] = taskConfig;

            _logger.LogInformation("Task configuration created: {TaskName}", request.TaskName);

            return Result<TaskConfig, Failure>.Ok(taskConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task configuration: {TaskName}", request.TaskName);
            return Result<TaskConfig, Failure>.Fail(new Failure("CreateConfigFailed", ex.Message) { Exception = ex });
        }
    }

    public async Task<Result<TaskConfig, Failure>> CreateTaskConfigAsync<T>(CreateTaskConfigRequest<T> request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert generic request to string-based request for internal processing
            var stringData = request.Data is string str ? str : JsonSerializer.Serialize(request.Data);
            var stringRequest = new CreateTaskConfigRequest
            {
                TaskId = request.TaskId,
                TaskName = request.TaskName,
                Data = stringData,
                TraceId = request.TraceId,
                EventId = request.EventId,
                SubscriberId = request.SubscriberId,
                ScheduledAt = request.ScheduledAt,
                Delay = request.Delay,
                IsRecurring = request.IsRecurring,
                CronExpression = request.CronExpression,
                CallbackUrl = request.CallbackUrl,
                Method = request.Method,
                Headers = request.Headers,
                MaxRetries = request.MaxRetries,
                TimeoutSeconds = request.TimeoutSeconds,
                IsActive = request.IsActive,
                DefaultTraceId = request.DefaultTraceId,
                DefaultEventId = request.DefaultEventId,
                DefaultSubscriberId = request.DefaultSubscriberId,
                DefaultDelay = request.DefaultDelay,
                AllowScheduling = request.AllowScheduling,
                DefaultCronExpression = request.DefaultCronExpression,
                DefaultIsRecurring = request.DefaultIsRecurring
            };

            // Delegate to the existing string-based method
            return await CreateTaskConfigAsync(stringRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task configuration (generic): {TaskName}", request.TaskName);
            return Result<TaskConfig, Failure>.Fail(new Failure("CreateConfigFailed", ex.Message) { Exception = ex });
        }
    }

    public async Task<Result<TaskConfig, Failure>> GetTaskConfigAsync(string taskName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_taskConfigs.TryGetValue(taskName, out var taskConfig))
            {
                return Result<TaskConfig, Failure>.Fail(new Failure($"Task configuration not found: {taskName}", "NotFound"));
            }

            return Result<TaskConfig, Failure>.Ok(taskConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task configuration: {TaskName}", taskName);
            return Result<TaskConfig, Failure>.Fail(new Failure("GetConfigFailed", ex.Message) { Exception = ex });
        }
    }

    public async Task<Result<TaskConfig, Failure>> UpdateTaskConfigAsync(string taskName, UpdateTaskConfigRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_taskConfigs.TryGetValue(taskName, out var existingConfig))
            {
                return Result<TaskConfig, Failure>.Fail(new Failure($"Task configuration not found: {taskName}", "NotFound"));
            }

            var updatedConfig = existingConfig with
            {
                CallbackUrl = request.CallbackUrl ?? existingConfig.CallbackUrl,
                Method = request.Method ?? existingConfig.Method,
                Headers = request.Headers ?? existingConfig.Headers,
                MaxRetries = request.MaxRetries ?? existingConfig.MaxRetries,
                TimeoutSeconds = request.TimeoutSeconds ?? existingConfig.TimeoutSeconds,
                IsActive = request.IsActive ?? existingConfig.IsActive,
                UpdatedAt = DateTime.UtcNow,
                DefaultDelay = request.DefaultDelay ?? existingConfig.DefaultDelay,
                AllowScheduling = request.AllowScheduling ?? existingConfig.AllowScheduling,
                DefaultCronExpression = request.DefaultCronExpression ?? existingConfig.DefaultCronExpression
            };

            _taskConfigs[taskName] = updatedConfig;

            _logger.LogInformation("Task configuration updated: {TaskName}", taskName);

            return Result<TaskConfig, Failure>.Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task configuration: {TaskName}", taskName);
            return Result<TaskConfig, Failure>.Fail(new Failure("UpdateConfigFailed", ex.Message) { Exception = ex });
        }
    }

    public async Task<Result<bool, Failure>> DeleteTaskConfigAsync(string taskName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_taskConfigs.ContainsKey(taskName))
            {
                return Result<bool, Failure>.Fail(new Failure($"Task configuration not found: {taskName}", "NotFound"));
            }

            var removed = _taskConfigs.Remove(taskName);

            _logger.LogInformation("Task configuration deleted: {TaskName}", taskName);

            return Result<bool, Failure>.Ok(removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete task configuration: {TaskName}", taskName);
            return Result<bool, Failure>.Fail(new Failure("DeleteConfigFailed", ex.Message) { Exception = ex });
        }
    }

    public async Task<Result<List<TaskConfig>, Failure>> GetAllTaskConfigsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var taskConfigs = _taskConfigs.Values.ToList();

            _logger.LogInformation("Retrieved {Count} task configurations", taskConfigs.Count);

            return Result<List<TaskConfig>, Failure>.Ok(taskConfigs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all task configurations");
            return Result<List<TaskConfig>, Failure>.Fail(new Failure("GetAllConfigsFailed", ex.Message) { Exception = ex });
        }
    }
}