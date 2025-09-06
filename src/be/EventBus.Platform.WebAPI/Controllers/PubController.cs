using EventBus.Platform.WebAPI.Handlers;
using EventBus.Platform.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PubController(
    ITaskHandler taskHandler,
    ISchedulerHandler schedulerHandler,
    ILogger<PubController> logger) : ControllerBase
{
    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTaskAsync([FromBody] CreateTaskRequest request, CancellationToken cancellationToken = default)
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

            if (request.MaxRetries < 0 || request.MaxRetries > 10)
            {
                return BadRequest(new { error = "MaxRetries must be between 0 and 10", code = "ValidationError" });
            }

            if (request.TimeoutSeconds < 1 || request.TimeoutSeconds > 300)
            {
                return BadRequest(new { error = "TimeoutSeconds must be between 1 and 300", code = "ValidationError" });
            }

            var result = await taskHandler.CreateTaskAsync(request, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed to create task: {Error}", result.Failure?.Message);
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
                TraceId = task.TraceId
            };

            logger.LogInformation("Task created successfully: {TaskId} - TraceId: {TraceId}", 
                task.Id, task.TraceId);

            return CreatedAtAction(nameof(GetTaskAsync), 
                new { id = task.Id }, 
                response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in CreateTaskAsync");
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    [HttpGet("tasks/{id}")]
    public async Task<IActionResult> GetTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { error = "Task ID is required", code = "ValidationError" });
            }

            var result = await taskHandler.GetTaskByIdAsync(id, cancellationToken);

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
                TraceId = task.TraceId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GetTaskAsync: {TaskId}", id);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    [HttpPut("tasks/{id}/status")]
    public async Task<IActionResult> UpdateTaskStatusAsync(string id, [FromBody] UpdateTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
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

            var result = await taskHandler.UpdateTaskStatusAsync(id, request.Status, request.ErrorMessage, cancellationToken);

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
                TraceId = task.TraceId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in UpdateTaskStatusAsync: {TaskId}", id);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    [HttpPost("tasks/{id}/execute")]
    public async Task<IActionResult> ExecuteTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { error = "Task ID is required", code = "ValidationError" });
            }

            var result = await taskHandler.ExecuteTaskAsync(id, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Failure?.Code == "NotFound")
                {
                    return NotFound(new { error = result.Failure?.Message });
                }
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            return Ok(new { message = "Task execution initiated", executed = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in ExecuteTaskAsync: {TaskId}", id);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasksAsync(
        [FromQuery] string? status = null,
        [FromQuery] string? eventId = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (limit < 1 || limit > 1000)
            {
                return BadRequest(new { error = "Limit must be between 1 and 1000", code = "ValidationError" });
            }

            Result<List<TaskEntity>, Failure> result;

            if (!string.IsNullOrWhiteSpace(eventId))
            {
                result = await taskHandler.GetTasksByEventIdAsync(eventId, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(status))
            {
                result = await taskHandler.GetTasksByStatusAsync(status, limit, cancellationToken);
            }
            else
            {
                result = await taskHandler.GetPendingTasksAsync(limit, cancellationToken);
            }

            if (!result.IsSuccess)
            {
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
                TraceId = task.TraceId
            }).ToList();

            return Ok(new
            {
                tasks = responses,
                count = responses.Count,
                filters = new
                {
                    status,
                    eventId,
                    limit
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GetTasksAsync");
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    [HttpPost("scheduler/tasks")]
    public async Task<IActionResult> CreateSchedulerTaskAsync([FromBody] CreateSchedulerTaskRequest request, CancellationToken cancellationToken = default)
    {
        var result = await schedulerHandler.CreateSchedulerTaskAsync(request, cancellationToken);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return CreatedAtAction(nameof(GetSchedulerTaskAsync), 
            new { id = result.Success!.Id }, 
            result.Success);
    }

    [HttpGet("scheduler/tasks/{id}")]
    public async Task<IActionResult> GetSchedulerTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await schedulerHandler.GetSchedulerTaskByIdAsync(id, cancellationToken);
        
        if (!result.IsSuccess)
        {
            if (result.Failure?.Code == "NotFound")
            {
                return NotFound(new { error = result.Failure?.Message });
            }
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return Ok(result.Success);
    }
}