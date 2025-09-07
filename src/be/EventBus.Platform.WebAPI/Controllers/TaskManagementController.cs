using EventBus.Platform.WebAPI.Handlers;
using EventBus.Platform.WebAPI.Models;
using EventBus.Infrastructure.Queue;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/tasks")]
public class TaskManagementController(
    ITaskHandler taskHandler,
    IQueueService queueService,
    ILogger<TaskManagementController> logger) : ControllerBase
{
    /// <summary>
    /// Create new task (unified API for immediate and scheduled execution)
    /// Based on design.md Task execution flow requirements
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTaskAsync(
        [FromBody] CreateTaskRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.CallbackUrl))
            {
                return BadRequest(new { error = "CallbackUrl is required", code = "ValidationError" });
            }

            if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out _))
            {
                return BadRequest(new { error = "CallbackUrl must be a valid URL", code = "ValidationError" });
            }

            var taskId = Guid.NewGuid().ToString();
            
            // Determine execution type: immediate vs scheduled
            if (request.ScheduledAt.HasValue || request.Delay.HasValue)
            {
                // Scheduled execution task
                var scheduledAt = request.ScheduledAt ?? DateTime.UtcNow.Add(request.Delay ?? TimeSpan.Zero);
                
                // Create task request with scheduled time
                var taskRequest = request with { ScheduledAt = scheduledAt };
                
                // Enqueue to scheduled task queue
                await queueService.EnqueueAsync(taskRequest, "scheduled-tasks", cancellationToken);
                
                logger.LogInformation("Scheduled task created: {TaskId} at {ScheduledAt} - TraceId: {TraceId}", 
                    taskId, scheduledAt, request.TraceId);
                
                return Accepted(new { 
                    TaskId = taskId, 
                    ScheduledAt = scheduledAt, 
                    Type = "Scheduled",
                    TraceId = request.TraceId 
                });
            }
            else
            {
                // Immediate execution task
                var taskRequest = request with { };
                
                // Enqueue to immediate task queue
                await queueService.EnqueueAsync(taskRequest, "immediate-tasks", cancellationToken);
                
                logger.LogInformation("Immediate task created: {TaskId} - TraceId: {TraceId}", 
                    taskId, request.TraceId);
                
                return Accepted(new { 
                    TaskId = taskId, 
                    Type = "Immediate",
                    TraceId = request.TraceId 
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in CreateTaskAsync");
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
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
            if (limit < 1 || limit > 100)
            {
                return BadRequest(new { error = "Limit must be between 1 and 100", code = "ValidationError" });
            }

            var result = await taskHandler.GetPendingTasksAsync(limit, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed to get pending tasks: {Error}", result.Failure?.Message);
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var tasks = result.Success!;
            var responses = tasks.Select(task => new TaskResponse
            {
                Id = task.Id,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                RetryCount = task.RetryCount,
                ErrorMessage = task.ErrorMessage,
                TraceId = task.TraceId,
                // Execution details for TaskWorkerService
                CallbackUrl = task.CallbackUrl,
                Method = task.Method,
                RequestPayload = task.RequestPayload,
                Headers = task.Headers,
                MaxRetries = task.MaxRetries,
                TimeoutSeconds = task.TimeoutSeconds,
                EventId = task.EventId,
                SubscriberId = task.SubscriberId
            }).ToList();

            logger.LogInformation("Retrieved {Count} pending tasks", responses.Count);

            return Ok(new
            {
                tasks = responses,
                count = responses.Count,
                limit
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GetPendingTasksAsync");
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
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
            if (limit < 1 || limit > 100)
            {
                return BadRequest(new { error = "Limit must be between 1 and 100", code = "ValidationError" });
            }

            var result = await taskHandler.GetScheduledTasksReadyForExecutionAsync(currentTime, limit, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed to get scheduled tasks: {Error}", result.Failure?.Message);
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var tasks = result.Success!;
            var responses = tasks.Select(task => new TaskResponse
            {
                Id = task.Id,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                RetryCount = task.RetryCount,
                ErrorMessage = task.ErrorMessage,
                TraceId = task.TraceId,
                // Execution details for TaskWorkerService
                CallbackUrl = task.CallbackUrl,
                Method = task.Method,
                RequestPayload = task.RequestPayload,
                Headers = task.Headers,
                MaxRetries = task.MaxRetries,
                TimeoutSeconds = task.TimeoutSeconds,
                EventId = task.EventId,
                SubscriberId = task.SubscriberId
            }).ToList();

            logger.LogInformation("Retrieved {Count} scheduled tasks ready for execution at {CurrentTime}", 
                responses.Count, currentTime);

            return Ok(new
            {
                tasks = responses,
                count = responses.Count,
                limit,
                currentTime
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GetScheduledTasksAsync");
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
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
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return BadRequest(new { error = "Task ID is required", code = "ValidationError" });
            }

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new { error = "Status is required", code = "ValidationError" });
            }

            var validStatuses = new[] { "Pending", "Processing", "Completed", "Failed", "Cancelled" };
            if (!validStatuses.Contains(request.Status))
            {
                return BadRequest(new { error = $"Invalid status. Must be one of: {string.Join(", ", validStatuses)}", code = "ValidationError" });
            }

            var result = await taskHandler.UpdateTaskStatusAsync(taskId, request.Status, request.ErrorMessage, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Failure?.Code == "NotFound")
                {
                    return NotFound(new { error = result.Failure?.Message });
                }
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var task = result.Success!;
            var response = new TaskResponse
            {
                Id = task.Id,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                RetryCount = task.RetryCount,
                ErrorMessage = task.ErrorMessage,
                TraceId = task.TraceId,
                // Execution details for TaskWorkerService
                CallbackUrl = task.CallbackUrl,
                Method = task.Method,
                RequestPayload = task.RequestPayload,
                Headers = task.Headers,
                MaxRetries = task.MaxRetries,
                TimeoutSeconds = task.TimeoutSeconds,
                EventId = task.EventId,
                SubscriberId = task.SubscriberId
            };

            logger.LogInformation("Task status updated: {TaskId} -> {Status} - TraceId: {TraceId}", 
                taskId, request.Status, task.TraceId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in UpdateTaskStatusAsync: {TaskId}", taskId);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
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
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return BadRequest(new { error = "Task ID is required", code = "ValidationError" });
            }

            var result = await taskHandler.GetTaskByIdAsync(taskId, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Failure?.Code == "NotFound")
                {
                    return NotFound(new { error = result.Failure?.Message });
                }
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var task = result.Success!;
            var response = new TaskResponse
            {
                Id = task.Id,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                RetryCount = task.RetryCount,
                ErrorMessage = task.ErrorMessage,
                TraceId = task.TraceId,
                // Execution details for TaskWorkerService
                CallbackUrl = task.CallbackUrl,
                Method = task.Method,
                RequestPayload = task.RequestPayload,
                Headers = task.Headers,
                MaxRetries = task.MaxRetries,
                TimeoutSeconds = task.TimeoutSeconds,
                EventId = task.EventId,
                SubscriberId = task.SubscriberId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GetTaskAsync: {TaskId}", taskId);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    /// <summary>
    /// Store task from queue (used by dispatcher)
    /// </summary>
    [HttpPost("store")]
    public async Task<IActionResult> StoreTaskAsync([FromBody] CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.CallbackUrl))
            {
                return BadRequest(new { error = "CallbackUrl is required", code = "ValidationError" });
            }

            if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out _))
            {
                return BadRequest(new { error = "CallbackUrl must be a valid URL", code = "ValidationError" });
            }

            var result = await taskHandler.CreateTaskAsync(request, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed to store task: {Error}", result.Failure?.Message);
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var task = result.Success!;
            var response = new TaskResponse
            {
                Id = task.Id,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                RetryCount = task.RetryCount,
                ErrorMessage = task.ErrorMessage,
                TraceId = task.TraceId,
                // Execution details for TaskWorkerService
                CallbackUrl = task.CallbackUrl,
                Method = task.Method,
                RequestPayload = task.RequestPayload,
                Headers = task.Headers,
                MaxRetries = task.MaxRetries,
                TimeoutSeconds = task.TimeoutSeconds,
                EventId = task.EventId,
                SubscriberId = task.SubscriberId
            };

            logger.LogInformation("Task stored successfully: {TaskId} - TraceId: {TraceId}", 
                task.Id, task.TraceId);

            return Created($"/api/tasks/{task.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in StoreTaskAsync");
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
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
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return BadRequest(new { error = "Task ID is required", code = "ValidationError" });
            }

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new { error = "Status is required", code = "ValidationError" });
            }

            var validStatuses = new[] { "Scheduled", "Processing", "Completed", "Failed", "Cancelled" };
            if (!validStatuses.Contains(request.Status))
            {
                return BadRequest(new { error = $"Invalid status. Must be one of: {string.Join(", ", validStatuses)}", code = "ValidationError" });
            }

            var result = await taskHandler.UpdateScheduledTaskStatusAsync(taskId, request, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Failure?.Code == "NotFound")
                {
                    return NotFound(new { error = result.Failure?.Message });
                }
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var task = result.Success!;
            var response = new TaskResponse
            {
                Id = task.Id,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                RetryCount = task.RetryCount,
                ErrorMessage = task.ErrorMessage,
                TraceId = task.TraceId,
                // Execution details for TaskWorkerService
                CallbackUrl = task.CallbackUrl,
                Method = task.Method,
                RequestPayload = task.RequestPayload,
                Headers = task.Headers,
                MaxRetries = task.MaxRetries,
                TimeoutSeconds = task.TimeoutSeconds,
                EventId = task.EventId,
                SubscriberId = task.SubscriberId
            };

            logger.LogInformation("Scheduled task status updated: {TaskId} -> {Status} (NextScheduled: {NextScheduledAt}) - TraceId: {TraceId}", 
                taskId, request.Status, request.NextScheduledAt, task.TraceId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in UpdateScheduledTaskStatusAsync: {TaskId}", taskId);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }
}