using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.Dispatcher.Services;

public class DispatcherService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DispatcherService> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly string _webApiBaseUrl;
    private readonly string _defaultQueueName;

    public DispatcherService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DispatcherService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        
        var pollingSeconds = configuration.GetValue<int>("MessageDispatcher:PollingIntervalSeconds", 1);
        _pollingInterval = TimeSpan.FromSeconds(pollingSeconds);
        _webApiBaseUrl = configuration.GetValue<string>("WebAPI:BaseUrl") ?? "http://localhost:5000";
        _defaultQueueName = configuration.GetValue<string>("MessageDispatcher:DefaultQueueName") ?? "tasks";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageDispatcherService started with WebAPI integration - BaseUrl: {BaseUrl}, Queue: {QueueName}", 
            _webApiBaseUrl, _defaultQueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedTasksAsync(stoppingToken);
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("MessageDispatcherService cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MessageDispatcherService execution");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("MessageDispatcherService stopped");
    }

    private async Task ProcessQueuedTasksAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Process immediate tasks
            await ProcessTaskQueueAsync("immediate-tasks", cancellationToken);
            
            // Process scheduled tasks
            await ProcessTaskQueueAsync("scheduled-tasks", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process queued tasks via WebAPI");
        }
    }
    
    private async Task ProcessTaskQueueAsync(string queueName, CancellationToken cancellationToken)
    {
        try
        {
            // 使用新的 dequeue-task API，它會自動從隊列取出任務並儲存到資料庫
            var dequeueResponse = await DequeueAndStoreTaskAsync(queueName, cancellationToken);
            
            if (dequeueResponse?.Success == true)
            {
                _logger.LogInformation(
                    "Task processed successfully - Queue: {QueueName}, TaskId: {TaskId}, Status: {Status}, TraceId: {TraceId}",
                    queueName, dequeueResponse.TaskId, dequeueResponse.Status, dequeueResponse.TraceId);
            }
            else if (dequeueResponse?.Success == false)
            {
                _logger.LogDebug("No tasks available in queue: {QueueName}", queueName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process tasks from queue: {QueueName}", queueName);
        }
    }
    
    private async Task<DequeueTaskResponse?> DequeueAndStoreTaskAsync(string queueName, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_webApiBaseUrl);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MessageDispatcher/1.0");
            
            var response = await httpClient.PostAsync(
                $"/api/queue/dequeue-task?queueName={queueName}", 
                null, 
                cancellationToken);
                
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var dequeueResponse = JsonSerializer.Deserialize<DequeueTaskResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return dequeueResponse;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Dequeue-task API returned {StatusCode}: {ReasonPhrase}, Content: {Content}", 
                    response.StatusCode, response.ReasonPhrase, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue and store task from WebAPI for queue: {QueueName}", queueName);
        }
        
        return null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MessageDispatcherService is stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("MessageDispatcherService stopped gracefully");
    }
}

// DTO models for HTTP API communication with new dequeue-task API
public class DequeueTaskResponse
{
    public string QueueName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? TaskId { get; set; }
    public string? Status { get; set; }
    public string? CallbackUrl { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? Message { get; set; }
    public int RemainingCount { get; set; }
    public string? TraceId { get; set; }
    public DateTime DequeuedAt { get; set; }
}

