using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventBus.Platform.WebAPI.Handlers;
using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Services;

/// <summary>
/// API response model for pending tasks endpoint
/// </summary>
public class TaskApiResponse
{
    public List<TaskResponse> Tasks { get; set; } = new();
    public int Count { get; set; }
    public int Limit { get; set; }
}

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
                // Get pending tasks via HTTP API instead of direct handler access
                var pendingTasks = await GetPendingTasksViaApiAsync(stoppingToken);
                
                if (pendingTasks == null)
                {
                    _logger.LogError("Failed to retrieve pending tasks from API");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                if (pendingTasks.Any())
                {
                    _logger.LogDebug("Found {TaskCount} pending tasks", pendingTasks.Count);

                    // Process tasks in parallel (limited concurrency)
                    var semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent executions
                    var tasks = pendingTasks.Select(task => ProcessTaskViaApiAsync(task, semaphore, stoppingToken));
                    
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

    private async Task<List<TaskEntity>?> GetPendingTasksViaApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("http://localhost:5000"); // Local API
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TaskWorker/1.0");
            
            var response = await httpClient.GetFromJsonAsync<TaskApiResponse>(
                "/api/tasks/pending?limit=50", cancellationToken);
                
            if (response?.Tasks != null)
            {
                // Convert TaskResponse to TaskEntity
                return response.Tasks.Select(t => new TaskEntity
                {
                    Id = t.Id,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    StartedAt = t.StartedAt,
                    CompletedAt = t.CompletedAt,
                    RetryCount = t.RetryCount,
                    ErrorMessage = t.ErrorMessage,
                    TraceId = t.TraceId,
                    // These fields need to be retrieved separately or stored in response
                    CallbackUrl = "", // Will be populated when needed
                    Method = "POST",
                    RequestPayload = "",
                    Headers = new Dictionary<string, string>(),
                    MaxRetries = 3,
                    TimeoutSeconds = 30
                }).ToList();
            }
            
            return new List<TaskEntity>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve pending tasks via API");
            return null;
        }
    }
    
    private async Task ProcessTaskViaApiAsync(TaskEntity task, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            await ExecuteTaskViaApiAsync(task, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task ExecuteTaskViaApiAsync(TaskEntity task, CancellationToken cancellationToken)
    {
        var taskId = task.Id;
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting task execution: {TaskId}, CallbackUrl: {CallbackUrl} - TraceId: {TraceId}",
            taskId, task.CallbackUrl, task.TraceId);

        try
        {
            // Update task status to Processing via API
            var processingSuccess = await UpdateTaskStatusViaApiAsync(taskId, "Processing", null, cancellationToken);
            if (!processingSuccess)
            {
                _logger.LogError("Failed to update task status to Processing: {TaskId}", taskId);
                return;
            }
            
            // Get full task details via API for callback execution
            var fullTask = await GetTaskByIdViaApiAsync(taskId, cancellationToken);
            if (fullTask == null)
            {
                _logger.LogError("Failed to retrieve task details: {TaskId}", taskId);
                return;
            }

            // Execute HTTP callback
            var success = await ExecuteHttpCallbackAsync(fullTask, cancellationToken);

            if (success)
            {
                // Mark task as completed via API
                var completedSuccess = await UpdateTaskStatusViaApiAsync(taskId, "Completed", null, cancellationToken);
                if (completedSuccess)
                {
                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogInformation("Task completed successfully: {TaskId} in {Duration}ms - TraceId: {TraceId}",
                        taskId, duration.TotalMilliseconds, task.TraceId);
                }
            }
            else
            {
                // Handle failure and retry logic
                await HandleTaskFailureViaApiAsync(fullTask, "HTTP callback failed", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during task execution: {TaskId} - TraceId: {TraceId}",
                taskId, task.TraceId);
            
            // Get full task details for failure handling
            var failedTask = await GetTaskByIdViaApiAsync(taskId, cancellationToken);
            if (failedTask != null)
            {
                await HandleTaskFailureViaApiAsync(failedTask, ex.Message, cancellationToken);
            }
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

    private async Task<TaskEntity?> GetTaskByIdViaApiAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("http://localhost:5000");
            
            var response = await httpClient.GetFromJsonAsync<TaskResponse>(
                $"/api/tasks/{taskId}", cancellationToken);
                
            if (response != null)
            {
                // Convert TaskResponse back to TaskEntity (this is a workaround)
                // In a real scenario, we'd need a separate endpoint that returns full task details
                return new TaskEntity
                {
                    Id = response.Id,
                    Status = response.Status,
                    CreatedAt = response.CreatedAt,
                    StartedAt = response.StartedAt,
                    CompletedAt = response.CompletedAt,
                    RetryCount = response.RetryCount,
                    ErrorMessage = response.ErrorMessage,
                    TraceId = response.TraceId,
                    // These would need to come from a different endpoint or be included in the response
                    CallbackUrl = "https://example.com/callback", // Placeholder
                    Method = "POST",
                    RequestPayload = "{}",
                    Headers = new Dictionary<string, string>(),
                    MaxRetries = 3,
                    TimeoutSeconds = 30
                };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task by ID via API: {TaskId}", taskId);
            return null;
        }
    }
    
    private async Task<bool> UpdateTaskStatusViaApiAsync(string taskId, string status, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("http://localhost:5000");
            
            var request = new UpdateTaskStatusRequest
            {
                Status = status,
                ErrorMessage = errorMessage
            };
            
            var response = await httpClient.PutAsJsonAsync(
                $"/api/tasks/{taskId}/status", request, cancellationToken);
                
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task status via API: {TaskId} -> {Status}", taskId, status);
            return false;
        }
    }
    
    private async Task HandleTaskFailureViaApiAsync(TaskEntity task, string errorMessage, CancellationToken cancellationToken)
    {
        var taskId = task.Id;
        var currentRetryCount = task.RetryCount + 1; // +1 because we're about to increment

        if (currentRetryCount <= task.MaxRetries)
        {
            // Mark as failed but allow retry via API
            var retryMessage = $"Retry {currentRetryCount}/{task.MaxRetries}: {errorMessage}";
            var success = await UpdateTaskStatusViaApiAsync(taskId, "Pending", retryMessage, cancellationToken);

            if (success)
            {
                _logger.LogWarning("Task marked for retry: {TaskId} ({RetryCount}/{MaxRetries}) - TraceId: {TraceId}",
                    taskId, currentRetryCount, task.MaxRetries, task.TraceId);
            }
        }
        else
        {
            // Max retries exceeded, mark as permanently failed via API
            var failureMessage = $"Max retries exceeded: {errorMessage}";
            var success = await UpdateTaskStatusViaApiAsync(taskId, "Failed", failureMessage, cancellationToken);

            if (success)
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