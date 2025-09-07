using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.TaskWorker.Services;

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
/// Task response model with all execution details
/// </summary>
public record TaskResponse
{
    public string Id { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TraceId { get; init; }
    
    // Execution details
    public string CallbackUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public string RequestPayload { get; init; } = string.Empty;
    public Dictionary<string, string>? Headers { get; init; }
    public int MaxRetries { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 30;
    public string? EventId { get; init; }
    public string? SubscriberId { get; init; }
    
    // Scheduled execution properties
    public DateTime? ScheduledAt { get; init; }
    public bool IsRecurring { get; init; }
    public string? CronExpression { get; init; }
}

/// <summary>
/// Request model for updating task status
/// </summary>
public record UpdateTaskStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Request model for updating scheduled task status
/// </summary>
public record UpdateScheduledTaskStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public DateTime? NextScheduledAt { get; init; }
}

/// <summary>
/// TaskWorker Console Application Service
/// Polls Task Management API for pending tasks and executes HTTP callbacks
/// </summary>
public class TaskWorkerService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TaskWorkerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly string _taskApiBaseUrl;

    public TaskWorkerService(
        IHttpClientFactory httpClientFactory,
        ILogger<TaskWorkerService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        _taskApiBaseUrl = configuration.GetValue<string>("TaskApi:BaseUrl") ?? "http://localhost:5000";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskWorker started with unified execution support - polling interval: {PollingInterval}s, API: {ApiUrl}", 
            _pollingInterval.TotalSeconds, _taskApiBaseUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 並發查詢立即執行和延遲執行的任務
                var pendingTasksTask = GetPendingTasksViaApiAsync(stoppingToken);
                var scheduledTasksTask = GetScheduledTasksViaApiAsync(DateTime.UtcNow, stoppingToken);

                await Task.WhenAll(pendingTasksTask, scheduledTasksTask);

                var pendingTasks = await pendingTasksTask;
                var scheduledTasks = await scheduledTasksTask;

                var allTasks = new List<TaskResponse>();
                if (pendingTasks?.Any() == true) allTasks.AddRange(pendingTasks);
                if (scheduledTasks?.Any() == true) allTasks.AddRange(scheduledTasks);

                if (allTasks.Any())
                {
                    _logger.LogDebug("Found {PendingCount} pending tasks and {ScheduledCount} scheduled tasks ready to execute", 
                        pendingTasks?.Count ?? 0, scheduledTasks?.Count ?? 0);

                    // 並發處理任務（限制並發數）
                    var semaphore = new SemaphoreSlim(5, 5);
                    var tasks = allTasks.Select(task => ProcessTaskViaApiAsync(task, semaphore, stoppingToken));
                    
                    await Task.WhenAll(tasks);
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TaskWorker execution failed");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Longer delay on error
            }
        }

        _logger.LogInformation("TaskWorker stopped");
    }

    private async Task<List<TaskResponse>?> GetPendingTasksViaApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_taskApiBaseUrl);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TaskWorker/1.0");
            
            var response = await httpClient.GetFromJsonAsync<TaskApiResponse>(
                "/api/tasks/pending?limit=50", cancellationToken);
                
            return response?.Tasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve pending tasks via API");
            return null;
        }
    }

    private async Task<List<TaskResponse>?> GetScheduledTasksViaApiAsync(DateTime currentTime, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_taskApiBaseUrl);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TaskWorker/1.0");
            
            var response = await httpClient.GetFromJsonAsync<TaskApiResponse>(
                $"/api/tasks/scheduled?currentTime={currentTime:yyyy-MM-ddTHH:mm:ss.fffZ}&limit=50", cancellationToken);
                
            return response?.Tasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve scheduled tasks via API");
            return null;
        }
    }
    
    private async Task ProcessTaskViaApiAsync(TaskResponse task, SemaphoreSlim semaphore, CancellationToken cancellationToken)
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

    private async Task ExecuteTaskViaApiAsync(TaskResponse task, CancellationToken cancellationToken)
    {
        var taskId = task.Id;
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting task execution: {TaskId}, CallbackUrl: {CallbackUrl} - TraceId: {TraceId}",
            taskId, task.CallbackUrl, task.TraceId);

        try
        {
            // 透過 API 更新任務狀態為處理中
            var processingSuccess = await UpdateTaskStatusViaApiAsync(taskId, "Processing", null, cancellationToken);
            if (!processingSuccess)
            {
                _logger.LogError("Failed to update task status to Processing: {TaskId}", taskId);
                return;
            }

            // 執行 HTTP 回調
            var success = await ExecuteHttpCallbackAsync(task, cancellationToken);

            if (success)
            {
                // 標記任務完成
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
                // 處理任務失敗情況
                await HandleTaskFailureViaApiAsync(task, "HTTP callback failed", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during task execution: {TaskId} - TraceId: {TraceId}",
                taskId, task.TraceId);
            
            await HandleTaskFailureViaApiAsync(task, ex.Message, cancellationToken);
        }
    }

    private async Task<bool> ExecuteHttpCallbackAsync(TaskResponse task, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            // 設定超時時間
            httpClient.Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds);

            // 準備 HTTP 請求
            var httpMethod = GetHttpMethod(task.Method);
            using var request = new HttpRequestMessage(httpMethod, task.CallbackUrl);

            // 添加自訂 Headers
            if (task.Headers != null)
            {
                foreach (var header in task.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // 添加 TraceId Header
            if (!string.IsNullOrWhiteSpace(task.TraceId))
            {
                request.Headers.TryAddWithoutValidation("X-Trace-Id", task.TraceId);
            }

            // 為非 GET 方法添加請求內容
            if (httpMethod != HttpMethod.Get && !string.IsNullOrWhiteSpace(task.RequestPayload))
            {
                try
                {
                    // 嘗試解析為 JSON
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(task.RequestPayload);
                    request.Content = JsonContent.Create(jsonElement);
                }
                catch (JsonException)
                {
                    // 如果不是有效的 JSON，當作純文字發送
                    request.Content = new StringContent(task.RequestPayload, Encoding.UTF8, "text/plain");
                }
            }

            // 執行請求
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
    
    private async Task<bool> UpdateTaskStatusViaApiAsync(string taskId, string status, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_taskApiBaseUrl);
            
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

    private async Task<bool> UpdateScheduledTaskStatusViaApiAsync(string taskId, string status, string? errorMessage, DateTime? nextScheduledAt, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_taskApiBaseUrl);
            
            var request = new UpdateScheduledTaskStatusRequest
            {
                Status = status,
                ErrorMessage = errorMessage,
                NextScheduledAt = nextScheduledAt
            };
            
            var response = await httpClient.PutAsJsonAsync(
                $"/api/tasks/scheduled/{taskId}/status", request, cancellationToken);
                
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scheduled task status via API: {TaskId} -> {Status}", taskId, status);
            return false;
        }
    }
    
    private async Task HandleTaskFailureViaApiAsync(TaskResponse task, string errorMessage, CancellationToken cancellationToken)
    {
        var taskId = task.Id;
        var currentRetryCount = task.RetryCount + 1;

        if (currentRetryCount <= task.MaxRetries)
        {
            var retryMessage = $"Retry {currentRetryCount}/{task.MaxRetries}: {errorMessage}";
            
            // 處理延遲任務和立即任務的重試邏輯
            if (task.ScheduledAt.HasValue)
            {
                // 延遲任務重試：計算下次重試時間
                var nextRetryTime = DateTime.UtcNow.AddMinutes(currentRetryCount * 5);
                var success = await UpdateScheduledTaskStatusViaApiAsync(taskId, "Scheduled", retryMessage, nextRetryTime, cancellationToken);

                if (success)
                {
                    _logger.LogWarning("Scheduled task marked for retry: {TaskId} ({RetryCount}/{MaxRetries}) at {NextRetry} - TraceId: {TraceId}",
                        taskId, currentRetryCount, task.MaxRetries, nextRetryTime, task.TraceId);
                }
            }
            else
            {
                // 立即任務重試：直接設為 Pending
                var success = await UpdateTaskStatusViaApiAsync(taskId, "Pending", retryMessage, cancellationToken);

                if (success)
                {
                    _logger.LogWarning("Task marked for retry: {TaskId} ({RetryCount}/{MaxRetries}) - TraceId: {TraceId}",
                        taskId, currentRetryCount, task.MaxRetries, task.TraceId);
                }
            }
        }
        else
        {
            // 超過最大重試次數，標記為永久失敗
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