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

        return result.ToAcceptedActionResult();
    }

    /// <summary>
    /// Get pending tasks for worker processing
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingTasksAsync(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // 驗證請求參數
        if (limit < 1 || limit > 100)
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Limit must be between 1 and 100");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result = await taskHandler.GetPendingTasksAsync(limit, cancellationToken);

        return result.ToActionResult();
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
        // 驗證請求參數
        if (limit < 1 || limit > 100)
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Limit must be between 1 and 100");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result =
            await taskHandler.GetScheduledTasksReadyForExecutionAsync(currentTime, limit, cancellationToken);
        return result.ToActionResult();
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

    /// <summary>
    /// Get task details by ID (used by workers)
    /// </summary>
    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        // 驗證請求參數
        if (string.IsNullOrWhiteSpace(taskId))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Task ID is required");
            return Result<TaskResponse, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result = await taskHandler.GetTaskByIdAsync(taskId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Store task from queue (used by dispatcher)
    /// Now accepts internal task request with complete configuration
    /// </summary>
    [HttpPost("store")]
    public async Task<IActionResult> StoreTaskAsync([FromBody] CreateTaskConfigRequest request,
        CancellationToken cancellationToken = default)
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
        return result.ToCreatedActionResult($"/api/tasks/{result.Success?.Id}");
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
        return result.ToActionResult();
    }
}
