using EventBus.Platform.WebAPI.Handlers;
using EventBus.Infrastructure.Models;
using EventBus.Infrastructure.Queue;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/tasks")]
public class TaskManagementController(
    ITaskHandler taskHandler,
    ILogger<TaskManagementController> logger) : ControllerBase
{
    /// <summary>
    /// Create new task (unified API for immediate and scheduled execution)
    /// Now uses TaskName to reference configuration and Data for execution payload
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTaskAsync(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        // 驗證請求參數
        if (string.IsNullOrWhiteSpace(request.TaskName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "TaskName is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result = await taskHandler.CreateTaskAsync(request, cancellationToken);
        
        // 轉換為統一的 Result 回應
        var responseResult = result.IsSuccess 
            ? Result<object, Failure>.Ok(new
            {
                Task = result.Success,
                request.TaskName,
                Type = "Immediate"
            })
            : Result<object, Failure>.Fail(result.Failure!);

        return responseResult.ToAcceptedActionResult();
    }

    /// <summary>
    /// Get pending tasks for worker processing
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingTasksAsync(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 驗證請求參數
            if (limit < 1 || limit > 100)
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Limit must be between 1 and 100");
                return Result<object, Failure>.Fail(validationFailure).ToActionResult();
            }

            var result = await taskHandler.GetPendingTasksAsync(limit, cancellationToken);

            // 轉換為統一的 Result 回應
            var responseResult = result.IsSuccess
                ? Result<object, Failure>.Ok(new
                {
                    tasks = result.Success!.Select(task => new TaskResponse
                    {
                        Id = task.Id,
                        Status = task.Status,
                        CreatedAt = task.CreatedAt,
                        StartedAt = task.StartedAt,
                        CompletedAt = task.CompletedAt,
                        RetryCount = task.RetryCount,
                        ErrorMessage = task.ErrorMessage,
                        TraceId = task.TraceId,
                        CallbackUrl = task.CallbackUrl,
                        Method = task.Method,
                        RequestPayload = task.RequestPayload,
                        Headers = task.Headers,
                        MaxRetries = task.MaxRetries,
                        TimeoutSeconds = task.TimeoutSeconds,
                        EventId = task.EventId,
                        SubscriberId = task.SubscriberId
                    }).ToList(),
                    count = result.Success!.Count(),
                    limit
                })
                : Result<object, Failure>.Fail(result.Failure!);

            if (responseResult.IsSuccess)
            {
                var tasksList = result.Success!.ToList();
                logger.LogInformation("Retrieved {Count} pending tasks", tasksList.Count);
            }

            return responseResult.ToActionResult();
        }
        catch (Exception ex)
        {
            var internalErrorFailure = new Failure(nameof(FailureCode.InternalServerError), "Internal server error")
            {
                Exception = ex
            };
            
            return Result<object, Failure>.Fail(internalErrorFailure).ToActionResult();
        }
    }

    /// <summary>
    /// Get scheduled tasks ready for execution (used by workers)
    /// Based on design.md requirements
    /// </summary>
    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduledTasksAsync(
        [FromQuery] DateTime currentTime,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 驗證請求參數
            if (limit < 1 || limit > 100)
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Limit must be between 1 and 100");
                return Result<object, Failure>.Fail(validationFailure).ToActionResult();
            }

            var result =
                await taskHandler.GetScheduledTasksReadyForExecutionAsync(currentTime, limit, cancellationToken);

            // 轉換為統一的 Result 回應
            var responseResult = result.IsSuccess
                ? Result<object, Failure>.Ok(new
                {
                    tasks = result.Success!.Select(task => new TaskResponse
                    {
                        Id = task.Id,
                        Status = task.Status,
                        CreatedAt = task.CreatedAt,
                        StartedAt = task.StartedAt,
                        CompletedAt = task.CompletedAt,
                        RetryCount = task.RetryCount,
                        ErrorMessage = task.ErrorMessage,
                        TraceId = task.TraceId,
                        CallbackUrl = task.CallbackUrl,
                        Method = task.Method,
                        RequestPayload = task.RequestPayload,
                        Headers = task.Headers,
                        MaxRetries = task.MaxRetries,
                        TimeoutSeconds = task.TimeoutSeconds,
                        EventId = task.EventId,
                        SubscriberId = task.SubscriberId
                    }).ToList(),
                    count = result.Success!.Count(),
                    limit,
                    currentTime
                })
                : Result<object, Failure>.Fail(result.Failure!);

            if (responseResult.IsSuccess)
            {
                var tasksList = result.Success!.ToList();
                logger.LogInformation("Retrieved {Count} scheduled tasks ready for execution at {CurrentTime}",
                    tasksList.Count, currentTime);
            }

            return responseResult.ToActionResult();
        }
        catch (Exception ex)
        {
            var internalErrorFailure = new Failure(nameof(FailureCode.InternalServerError), "Internal server error")
            {
                Exception = ex
            };
            
            return Result<object, Failure>.Fail(internalErrorFailure).ToActionResult();
        }
    }

    /// <summary>
    /// Update task status (used by workers)
    /// </summary>
    [HttpPut("{taskId}/status")]
    public async Task<IActionResult> UpdateTaskStatusAsync(
        string taskId,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 驗證請求參數
            if (string.IsNullOrWhiteSpace(taskId))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Task ID is required");
                return Result<object, Failure>.Fail(validationFailure).ToActionResult();
            }

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Status is required");
                return Result<object, Failure>.Fail(validationFailure).ToActionResult();
            }

            var validStatuses = new[] { "Pending", "Processing", "Completed", "Failed", "Cancelled" };
            if (!validStatuses.Contains(request.Status))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), 
                    $"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                return Result<object, Failure>.Fail(validationFailure).ToActionResult();
            }

            var result = await taskHandler.UpdateTaskStatusAsync(taskId, request.Status, request.ErrorMessage, cancellationToken);

            // 轉換為統一的 Result 回應
            var responseResult = result.IsSuccess
                ? Result<TaskResponse, Failure>.Ok(new TaskResponse
                {
                    Id = result.Success!.Id,
                    Status = result.Success.Status,
                    CreatedAt = result.Success.CreatedAt,
                    StartedAt = result.Success.StartedAt,
                    CompletedAt = result.Success.CompletedAt,
                    RetryCount = result.Success.RetryCount,
                    ErrorMessage = result.Success.ErrorMessage,
                    TraceId = result.Success.TraceId,
                    CallbackUrl = result.Success.CallbackUrl,
                    Method = result.Success.Method,
                    RequestPayload = result.Success.RequestPayload,
                    Headers = result.Success.Headers,
                    MaxRetries = result.Success.MaxRetries,
                    TimeoutSeconds = result.Success.TimeoutSeconds,
                    EventId = result.Success.EventId,
                    SubscriberId = result.Success.SubscriberId
                })
                : Result<TaskResponse, Failure>.Fail(result.Failure!);

            if (responseResult.IsSuccess)
            {
                logger.LogInformation("Task status updated: {TaskId} -> {Status} - TraceId: {TraceId}",
                    taskId, request.Status, responseResult.Success!.TraceId);
            }

            return responseResult.ToActionResult();
        }
        catch (Exception ex)
        {
            var internalErrorFailure = new Failure(nameof(FailureCode.InternalServerError), "Internal server error")
            {
                Exception = ex
            };
            
            return Result<TaskResponse, Failure>.Fail(internalErrorFailure).ToActionResult();
        }
    }

    /// <summary>
    /// Get task details by ID (used by workers)
    /// </summary>
    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 驗證請求參數
            if (string.IsNullOrWhiteSpace(taskId))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Task ID is required");
                return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
            }

            var result = await taskHandler.GetTaskByIdAsync(taskId, cancellationToken);

            // 轉換為統一的 Result 回應
            var responseResult = result.IsSuccess
                ? Result<TaskResponse, Failure>.Ok(new TaskResponse
                {
                    Id = result.Success!.Id,
                    Status = result.Success.Status,
                    CreatedAt = result.Success.CreatedAt,
                    StartedAt = result.Success.StartedAt,
                    CompletedAt = result.Success.CompletedAt,
                    RetryCount = result.Success.RetryCount,
                    ErrorMessage = result.Success.ErrorMessage,
                    TraceId = result.Success.TraceId,
                    CallbackUrl = result.Success.CallbackUrl,
                    Method = result.Success.Method,
                    RequestPayload = result.Success.RequestPayload,
                    Headers = result.Success.Headers,
                    MaxRetries = result.Success.MaxRetries,
                    TimeoutSeconds = result.Success.TimeoutSeconds,
                    EventId = result.Success.EventId,
                    SubscriberId = result.Success.SubscriberId
                })
                : Result<TaskResponse, Failure>.Fail(result.Failure!);

            return responseResult.ToActionResult();
        }
        catch (Exception ex)
        {
            var internalErrorFailure = new Failure(nameof(FailureCode.InternalServerError), "Internal server error")
            {
                Exception = ex
            };
            
            return Result<TaskResponse, Failure>.Fail(internalErrorFailure).ToActionResult();
        }
    }

    /// <summary>
    /// Store task from queue (used by dispatcher)
    /// Now accepts internal task request with complete configuration
    /// </summary>
    [HttpPost("store")]
    public async Task<IActionResult> StoreTaskAsync([FromBody] CreateTaskConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 驗證請求參數
            if (string.IsNullOrWhiteSpace(request.TaskName))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "TaskName is required");
                return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
            }

            if (string.IsNullOrWhiteSpace(request.CallbackUrl))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "CallbackUrl is required");
                return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
            }

            if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out _))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "CallbackUrl must be a valid URL");
                return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
            }

            // Convert internal request to original format for handler
            var handlerRequest = new CreateTaskRequest
            {
                TaskName = request.TaskName,
                Data = request.Data
            };

            var result = await taskHandler.CreateTaskAsync(handlerRequest, cancellationToken);

            // 轉換為統一的 Result 回應
            var responseResult = result.IsSuccess
                ? Result<TaskResponse, Failure>.Ok(new TaskResponse
                {
                    Id = result.Success!.Id,
                    Status = result.Success.Status,
                    CreatedAt = result.Success.CreatedAt,
                    StartedAt = result.Success.StartedAt,
                    CompletedAt = result.Success.CompletedAt,
                    RetryCount = result.Success.RetryCount,
                    ErrorMessage = result.Success.ErrorMessage,
                    TraceId = result.Success.TraceId,
                    // Execution details for TaskWorkerService (from internal request)
                    CallbackUrl = request.CallbackUrl,
                    Method = request.Method,
                    RequestPayload = request.Data,
                    Headers = request.Headers,
                    MaxRetries = request.MaxRetries,
                    TimeoutSeconds = request.TimeoutSeconds,
                    EventId = request.EventId,
                    SubscriberId = request.SubscriberId
                })
                : Result<TaskResponse, Failure>.Fail(result.Failure!);

            if (responseResult.IsSuccess)
            {
                logger.LogInformation("Task stored successfully: {TaskId} ({TaskName}) - TraceId: {TraceId}",
                    responseResult.Success!.Id, request.TaskName, responseResult.Success.TraceId);
            }

            return responseResult.ToCreatedActionResult($"/api/tasks/{responseResult.Success?.Id}");
        }
        catch (Exception ex)
        {
            var internalErrorFailure = new Failure(nameof(FailureCode.InternalServerError), "Internal server error")
            {
                Exception = ex
            };
            
            return Result<TaskResponse, Failure>.Fail(internalErrorFailure).ToActionResult();
        }
    }

    /// <summary>
    /// Update scheduled task status with retry time support (used by workers)
    /// Based on design.md scheduled task retry logic
    /// </summary>
    [HttpPut("scheduled/{taskId}/status")]
    public async Task<IActionResult> UpdateScheduledTaskStatusAsync(
        string taskId,
        [FromBody] UpdateScheduledTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 驗證請求參數
            if (string.IsNullOrWhiteSpace(taskId))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Task ID is required");
                return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
            }

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Status is required");
                return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
            }

            var validStatuses = new[] { "Scheduled", "Processing", "Completed", "Failed", "Cancelled" };
            if (!validStatuses.Contains(request.Status))
            {
                var validationFailure = new Failure(nameof(FailureCode.ValidationError),
                    $"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
            }

            var result = await taskHandler.UpdateScheduledTaskStatusAsync(taskId, request, cancellationToken);

            // 轉換為統一的 Result 回應
            var responseResult = result.IsSuccess
                ? Result<TaskResponse, Failure>.Ok(new TaskResponse
                {
                    Id = result.Success!.Id,
                    Status = result.Success.Status,
                    CreatedAt = result.Success.CreatedAt,
                    StartedAt = result.Success.StartedAt,
                    CompletedAt = result.Success.CompletedAt,
                    RetryCount = result.Success.RetryCount,
                    ErrorMessage = result.Success.ErrorMessage,
                    TraceId = result.Success.TraceId,
                    CallbackUrl = result.Success.CallbackUrl,
                    Method = result.Success.Method,
                    RequestPayload = result.Success.RequestPayload,
                    Headers = result.Success.Headers,
                    MaxRetries = result.Success.MaxRetries,
                    TimeoutSeconds = result.Success.TimeoutSeconds,
                    EventId = result.Success.EventId,
                    SubscriberId = result.Success.SubscriberId
                })
                : Result<TaskResponse, Failure>.Fail(result.Failure!);

            if (responseResult.IsSuccess)
            {
                logger.LogInformation(
                    "Scheduled task status updated: {TaskId} -> {Status} (NextScheduled: {NextScheduledAt}) - TraceId: {TraceId}",
                    taskId, request.Status, request.NextScheduledAt, responseResult.Success!.TraceId);
            }

            return responseResult.ToActionResult();
        }
        catch (Exception ex)
        {
            var internalErrorFailure = new Failure(nameof(FailureCode.InternalServerError), "Internal server error")
            {
                Exception = ex
            };
            
            return Result<TaskResponse, Failure>.Fail(internalErrorFailure).ToActionResult();
        }
    }
}
