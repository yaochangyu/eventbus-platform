using EventBus.Infrastructure.Models;
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
        // Validate request
        if (string.IsNullOrWhiteSpace(request.TaskName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "TaskName is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "CallbackUrl is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out _))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "CallbackUrl must be a valid URL");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result = await taskConfigHandler.CreateTaskConfigAsync(request, cancellationToken);

        return result.ToCreatedActionResult($"/api/task-configs/{request.TaskName}");
    }

    /// <summary>
    /// Get a task configuration by name
    /// </summary>
    [HttpGet("{taskName}")]
    public async Task<IActionResult> GetTaskConfigAsync(string taskName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Task name is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result = await taskConfigHandler.GetTaskConfigAsync(taskName, cancellationToken);

        return result.ToActionResult();
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
        if (string.IsNullOrWhiteSpace(taskName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Task name is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        // Validate CallbackUrl if provided
        if (!string.IsNullOrWhiteSpace(request.CallbackUrl) &&
            !Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out _))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "CallbackUrl must be a valid URL");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result = await taskConfigHandler.UpdateTaskConfigAsync(taskName, request, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a task configuration
    /// </summary>
    [HttpDelete("{taskName}")]
    public async Task<IActionResult> DeleteTaskConfigAsync(string taskName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Task name is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var result = await taskConfigHandler.DeleteTaskConfigAsync(taskName, cancellationToken);

        return result.ToNoContentActionResult();
    }

    /// <summary>
    /// Get all task configurations
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTaskConfigsAsync(CancellationToken cancellationToken = default)
    {
        var result = await taskConfigHandler.GetAllTaskConfigsAsync(cancellationToken);

        return result.ToActionResult();
    }
}
