using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventBus.Platform.WebAPI.Handlers;
using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Services;

/// <summary>
/// Background service that polls for pending tasks and executes HTTP callbacks
/// Based on design.md specifications - polls every 5 seconds
/// </summary>
public class TaskWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TaskWorkerService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public TaskWorkerService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<TaskWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskWorkerService started - polling interval: {PollingInterval}s", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var taskHandler = scope.ServiceProvider.GetRequiredService<ITaskHandler>();

                // Get pending tasks
                var pendingTasksResult = await taskHandler.GetPendingTasksAsync(50, stoppingToken);
                
                if (!pendingTasksResult.IsSuccess)
                {
                    _logger.LogError("Failed to retrieve pending tasks: {Error}", pendingTasksResult.Failure?.Message);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var pendingTasks = pendingTasksResult.Success!;

                if (pendingTasks.Any())
                {
                    _logger.LogDebug("Found {TaskCount} pending tasks", pendingTasks.Count);

                    // Process tasks in parallel (limited concurrency)
                    var semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent executions
                    var tasks = pendingTasks.Select(task => ProcessTaskAsync(task, taskHandler, semaphore, stoppingToken));
                    
                    await Task.WhenAll(tasks);
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TaskWorkerService execution failed");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Longer delay on error
            }
        }

        _logger.LogInformation("TaskWorkerService stopped");
    }

    private async Task ProcessTaskAsync(TaskEntity task, ITaskHandler taskHandler, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            await ExecuteTaskAsync(task, taskHandler, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task ExecuteTaskAsync(TaskEntity task, ITaskHandler taskHandler, CancellationToken cancellationToken)
    {
        var taskId = task.Id;
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting task execution: {TaskId}, CallbackUrl: {CallbackUrl} - TraceId: {TraceId}",
            taskId, task.CallbackUrl, task.TraceId);

        try
        {
            // Update task status to Processing
            var processingResult = await taskHandler.UpdateTaskStatusAsync(taskId, "Processing", cancellationToken: cancellationToken);
            if (!processingResult.IsSuccess)
            {
                _logger.LogError("Failed to update task status to Processing: {TaskId} - {Error}",
                    taskId, processingResult.Failure?.Message);
                return;
            }

            // Execute HTTP callback
            var success = await ExecuteHttpCallbackAsync(task, cancellationToken);

            if (success)
            {
                // Mark task as completed
                var completedResult = await taskHandler.UpdateTaskStatusAsync(taskId, "Completed", cancellationToken: cancellationToken);
                if (completedResult.IsSuccess)
                {
                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogInformation("Task completed successfully: {TaskId} in {Duration}ms - TraceId: {TraceId}",
                        taskId, duration.TotalMilliseconds, task.TraceId);
                }
            }
            else
            {
                // Handle failure and retry logic
                await HandleTaskFailureAsync(task, taskHandler, "HTTP callback failed", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during task execution: {TaskId} - TraceId: {TraceId}",
                taskId, task.TraceId);
            
            await HandleTaskFailureAsync(task, taskHandler, ex.Message, cancellationToken);
        }
    }

    private async Task<bool> ExecuteHttpCallbackAsync(TaskEntity task, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            // Set timeout based on task configuration
            httpClient.Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds);

            // Prepare request
            var httpMethod = GetHttpMethod(task.Method);
            using var request = new HttpRequestMessage(httpMethod, task.CallbackUrl);

            // Add custom headers
            if (task.Headers != null)
            {
                foreach (var header in task.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Add TraceId header if available
            if (!string.IsNullOrWhiteSpace(task.TraceId))
            {
                request.Headers.TryAddWithoutValidation("X-Trace-Id", task.TraceId);
            }

            // Add request payload for non-GET methods
            if (httpMethod != HttpMethod.Get && !string.IsNullOrWhiteSpace(task.RequestPayload))
            {
                try
                {
                    // Try to parse as JSON first
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(task.RequestPayload);
                    request.Content = JsonContent.Create(jsonElement);
                }
                catch (JsonException)
                {
                    // If not valid JSON, send as plain text
                    request.Content = new StringContent(task.RequestPayload, Encoding.UTF8, "text/plain");
                }
            }

            // Execute request
            using var response = await httpClient.SendAsync(request, cancellationToken);

            var isSuccess = response.IsSuccessStatusCode;
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("HTTP callback response: {TaskId} -> {StatusCode} - TraceId: {TraceId}",
                task.Id, (int)response.StatusCode, task.TraceId);

            if (!isSuccess)
            {
                _logger.LogWarning("HTTP callback failed: {TaskId} -> {StatusCode}: {ResponseContent} - TraceId: {TraceId}",
                    task.Id, (int)response.StatusCode, responseContent, task.TraceId);
            }

            return isSuccess;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed: {TaskId}, CallbackUrl: {CallbackUrl} - TraceId: {TraceId}",
                task.Id, task.CallbackUrl, task.TraceId);
            return false;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError("HTTP request timeout: {TaskId}, CallbackUrl: {CallbackUrl}, Timeout: {TimeoutSeconds}s - TraceId: {TraceId}",
                task.Id, task.CallbackUrl, task.TimeoutSeconds, task.TraceId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during HTTP callback: {TaskId} - TraceId: {TraceId}",
                task.Id, task.TraceId);
            return false;
        }
    }

    private async Task HandleTaskFailureAsync(TaskEntity task, ITaskHandler taskHandler, string errorMessage, CancellationToken cancellationToken)
    {
        var taskId = task.Id;
        var currentRetryCount = task.RetryCount + 1; // +1 because we're about to increment

        if (currentRetryCount <= task.MaxRetries)
        {
            // Mark as failed but allow retry
            var failedResult = await taskHandler.UpdateTaskStatusAsync(
                taskId, "Pending", $"Retry {currentRetryCount}/{task.MaxRetries}: {errorMessage}", cancellationToken);

            if (failedResult.IsSuccess)
            {
                _logger.LogWarning("Task marked for retry: {TaskId} ({RetryCount}/{MaxRetries}) - TraceId: {TraceId}",
                    taskId, currentRetryCount, task.MaxRetries, task.TraceId);
            }
        }
        else
        {
            // Max retries exceeded, mark as permanently failed
            var failedResult = await taskHandler.UpdateTaskStatusAsync(
                taskId, "Failed", $"Max retries exceeded: {errorMessage}", cancellationToken);

            if (failedResult.IsSuccess)
            {
                _logger.LogError("Task failed permanently: {TaskId} after {MaxRetries} retries - TraceId: {TraceId}",
                    taskId, task.MaxRetries, task.TraceId);
            }
        }
    }

    private static HttpMethod GetHttpMethod(string method)
    {
        return method?.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => HttpMethod.Post // Default to POST
        };
    }
}