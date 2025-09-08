using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.Dispatcher.Services;

public class MessageDispatcherService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MessageDispatcherService> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly string _webApiBaseUrl;
    private readonly string _defaultQueueName;

    public MessageDispatcherService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MessageDispatcherService> logger)
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
            // 透過 HTTP API 從 WebAPI 的 dequeue 端點取得任務
            var dequeueResponse = await DequeueTaskFromWebApiAsync(cancellationToken);
            
            if (dequeueResponse?.Item != null)
            {
                _logger.LogDebug("Dequeued task from WebAPI: {QueueName}, Found: {Found}", 
                    _defaultQueueName, dequeueResponse.Found);
                
                if (dequeueResponse.Found)
                {
                    // 將任務透過 HTTP API 存回 WebAPI 的 store 端點
                    await StoreTaskToWebApiAsync(dequeueResponse.Item, cancellationToken);
                    
                    _logger.LogInformation(
                        "Task moved from queue to repository via WebAPI - TraceId: {TraceId}",
                        dequeueResponse.TraceId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process queued tasks via WebAPI");
        }
    }
    
    private async Task<DequeueResponse?> DequeueTaskFromWebApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_webApiBaseUrl);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MessageDispatcher/1.0");
            
            var response = await httpClient.PostAsync(
                $"/api/queue/{_defaultQueueName}/dequeue", 
                null, 
                cancellationToken);
                
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var dequeueResponse = JsonSerializer.Deserialize<DequeueResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return dequeueResponse;
            }
            else
            {
                _logger.LogWarning("Dequeue API returned {StatusCode}: {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue task from WebAPI");
        }
        
        return null;
    }
    
    private async Task StoreTaskToWebApiAsync(object taskData, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_webApiBaseUrl);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MessageDispatcher/1.0");
            
            // Convert taskData directly to CreateTaskRequest format expected by /api/tasks/store
            var jsonContent = JsonSerializer.Serialize(taskData);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync("/api/tasks/store", content, cancellationToken);
                
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully stored task to WebAPI");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Store task API returned {StatusCode}: {ReasonPhrase}, Content: {Content}", 
                    response.StatusCode, response.ReasonPhrase, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store task to WebAPI");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MessageDispatcherService is stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("MessageDispatcherService stopped gracefully");
    }
}

// DTO models for HTTP API communication
public class DequeueResponse
{
    public string QueueName { get; set; } = string.Empty;
    public object? Item { get; set; }
    public bool Found { get; set; }
    public int RemainingCount { get; set; }
    public string? TraceId { get; set; }
    public DateTime DequeuedAt { get; set; }
}

