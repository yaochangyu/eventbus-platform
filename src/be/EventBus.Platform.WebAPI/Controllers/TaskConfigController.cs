using EventBus.Platform.WebAPI.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

/// <summary>
/// Controller for managing task configurations
/// Handles CRUD operations for task configuration settings
/// </summary>
[ApiController]
[Route("api/task-configs")]
public class TaskConfigController(
    ITaskConfigHandler taskConfigHandler,
    ILogger<TaskConfigController> logger) : ControllerBase
{
    /// <summary>
    /// Create a new task configuration
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTaskConfigAsync(
        [FromBody] CreateTaskConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.TaskName))
            {
                return BadRequest(new { error = "TaskName is required", code = "ValidationError" });
            }

            if (string.IsNullOrWhiteSpace(request.CallbackUrl))
            {
                return BadRequest(new { error = "CallbackUrl is required", code = "ValidationError" });
            }

            if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out _))
            {
                return BadRequest(new { error = "CallbackUrl must be a valid URL", code = "ValidationError" });
            }

            var result = await taskConfigHandler.CreateTaskConfigAsync(request, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogError("Failed to create task config: {TaskName} - {Error} - Exception: {Exception}", 
                    request.TaskName, result.Failure?.Message, result.Failure?.Exception);
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var taskConfig = result.Success!;
            var response = new TaskConfigResponse
            {
                TaskName = taskConfig.TaskName,
                CallbackUrl = taskConfig.CallbackUrl,
                Method = taskConfig.Method,
                Headers = taskConfig.Headers,
                MaxRetries = taskConfig.MaxRetries,
                TimeoutSeconds = taskConfig.TimeoutSeconds,
                IsActive = taskConfig.IsActive,
                CreatedAt = taskConfig.CreatedAt,
                UpdatedAt = taskConfig.UpdatedAt,
                DefaultDelay = taskConfig.DefaultDelay,
                AllowScheduling = taskConfig.AllowScheduling,
                DefaultCronExpression = taskConfig.DefaultCronExpression
            };

            logger.LogInformation("Task config created successfully: {TaskName}", request.TaskName);

            return Created($"/api/task-configs/{taskConfig.TaskName}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in CreateTaskConfigAsync: {TaskName}", request.TaskName);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    /// <summary>
    /// Get a task configuration by name
    /// </summary>
    [HttpGet("{taskName}")]
    public async Task<IActionResult> GetTaskConfigAsync(string taskName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return BadRequest(new { error = "Task name is required", code = "ValidationError" });
            }

            var result = await taskConfigHandler.GetTaskConfigAsync(taskName, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Failure?.Code == "NotFound")
                {
                    return NotFound(new { error = result.Failure?.Message });
                }
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var taskConfig = result.Success!;
            var response = new TaskConfigResponse
            {
                TaskName = taskConfig.TaskName,
                CallbackUrl = taskConfig.CallbackUrl,
                Method = taskConfig.Method,
                Headers = taskConfig.Headers,
                MaxRetries = taskConfig.MaxRetries,
                TimeoutSeconds = taskConfig.TimeoutSeconds,
                IsActive = taskConfig.IsActive,
                CreatedAt = taskConfig.CreatedAt,
                UpdatedAt = taskConfig.UpdatedAt,
                DefaultDelay = taskConfig.DefaultDelay,
                AllowScheduling = taskConfig.AllowScheduling,
                DefaultCronExpression = taskConfig.DefaultCronExpression
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GetTaskConfigAsync: {TaskName}", taskName);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    /// <summary>
    /// Update an existing task configuration
    /// </summary>
    [HttpPut("{taskName}")]
    public async Task<IActionResult> UpdateTaskConfigAsync(
        string taskName,
        [FromBody] UpdateTaskConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return BadRequest(new { error = "Task name is required", code = "ValidationError" });
            }

            // Validate CallbackUrl if provided
            if (!string.IsNullOrWhiteSpace(request.CallbackUrl) && 
                !Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out _))
            {
                return BadRequest(new { error = "CallbackUrl must be a valid URL", code = "ValidationError" });
            }

            var result = await taskConfigHandler.UpdateTaskConfigAsync(taskName, request, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Failure?.Code == "NotFound")
                {
                    return NotFound(new { error = result.Failure?.Message });
                }
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var taskConfig = result.Success!;
            var response = new TaskConfigResponse
            {
                TaskName = taskConfig.TaskName,
                CallbackUrl = taskConfig.CallbackUrl,
                Method = taskConfig.Method,
                Headers = taskConfig.Headers,
                MaxRetries = taskConfig.MaxRetries,
                TimeoutSeconds = taskConfig.TimeoutSeconds,
                IsActive = taskConfig.IsActive,
                CreatedAt = taskConfig.CreatedAt,
                UpdatedAt = taskConfig.UpdatedAt,
                DefaultDelay = taskConfig.DefaultDelay,
                AllowScheduling = taskConfig.AllowScheduling,
                DefaultCronExpression = taskConfig.DefaultCronExpression
            };

            logger.LogInformation("Task config updated successfully: {TaskName}", taskName);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in UpdateTaskConfigAsync: {TaskName}", taskName);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    /// <summary>
    /// Delete a task configuration
    /// </summary>
    [HttpDelete("{taskName}")]
    public async Task<IActionResult> DeleteTaskConfigAsync(string taskName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return BadRequest(new { error = "Task name is required", code = "ValidationError" });
            }

            var result = await taskConfigHandler.DeleteTaskConfigAsync(taskName, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Failure?.Code == "NotFound")
                {
                    return NotFound(new { error = result.Failure?.Message });
                }
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            logger.LogInformation("Task config deleted successfully: {TaskName}", taskName);

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in DeleteTaskConfigAsync: {TaskName}", taskName);
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }

    /// <summary>
    /// Get all task configurations
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTaskConfigsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await taskConfigHandler.GetAllTaskConfigsAsync(cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogError("Failed to get task configs: {Error} - Exception: {Exception}", 
                    result.Failure?.Message, result.Failure?.Exception);
                return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
            }

            var taskConfigs = result.Success!;
            var responses = taskConfigs.Select(taskConfig => new TaskConfigResponse
            {
                TaskName = taskConfig.TaskName,
                CallbackUrl = taskConfig.CallbackUrl,
                Method = taskConfig.Method,
                Headers = taskConfig.Headers,
                MaxRetries = taskConfig.MaxRetries,
                TimeoutSeconds = taskConfig.TimeoutSeconds,
                IsActive = taskConfig.IsActive,
                CreatedAt = taskConfig.CreatedAt,
                UpdatedAt = taskConfig.UpdatedAt,
                DefaultDelay = taskConfig.DefaultDelay,
                AllowScheduling = taskConfig.AllowScheduling,
                DefaultCronExpression = taskConfig.DefaultCronExpression
            }).ToList();

            logger.LogInformation("Retrieved {Count} task configurations", responses.Count);

            return Ok(new
            {
                taskConfigs = responses,
                count = responses.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GetAllTaskConfigsAsync");
            return StatusCode(500, new { error = "Internal server error", code = "InternalError" });
        }
    }
}