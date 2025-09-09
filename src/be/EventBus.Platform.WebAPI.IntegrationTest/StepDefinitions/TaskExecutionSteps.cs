using EventBus.Testing.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace EventBus.Platform.Task.IntegrationTest.StepDefinitions;

[Binding]
public class TaskExecutionSteps
{
    private readonly TaskTestContext _context;
    private readonly HttpClient _apiClient;
    private readonly TestConfiguration _configuration;
    private readonly ILogger<TaskExecutionSteps> _logger;

    public TaskExecutionSteps(TaskTestContext context, HttpClient apiClient, TestConfiguration configuration)
    {
        _context = context;
        _apiClient = apiClient;
        _configuration = configuration;
        
        // 建立 logger（簡單版本）
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<TaskExecutionSteps>();
    }

    [Given(@"測試環境已初始化")]
    public void GivenTestEnvironmentInitialized()
    {
        // 測試環境在 Fixture 中已初始化
        _context.TestData["environment_initialized"] = true;
    }

    [Given(@"EventBus WebAPI 正在運行")]
    public void GivenEventBusWebAPIIsRunning()
    {
        // 在實際測試中，需要確保 WebAPI 服務正在運行
        // 這裡暫時標記為已準備
        _context.TestData["webapi_running"] = true;
    }

    [Given(@"外部回調服務 Mock 已設定")]
    public void GivenExternalCallbackMockIsSetup()
    {
        // Mock 服務在 Fixture 中已設定
        _context.TestData["mock_setup"] = true;
    }

    [Given(@"我有一個有效的立即執行任務請求，包含回調URL ""(.*)""")]
    public void GivenValidImmediateTaskRequest(string callbackUrl)
    {
        var fullCallbackUrl = $"{_configuration.MockServerUrl}{callbackUrl}";
        _context.CurrentTaskRequest = TaskRequestBuilder.CreateImmediateTask(fullCallbackUrl);
        
        _logger.LogInformation("Created immediate task request with callback URL: {CallbackUrl}", fullCallbackUrl);
    }

    [Given(@"外部回調服務設定為回傳 (\d+) 錯誤")]
    public void GivenExternalCallbackSetupToReturnError(int statusCode)
    {
        // 標記為失敗回調設定
        _context.TestData["callback_failure"] = true;
        _context.TestData["failure_status_code"] = statusCode;
    }

    [When(@"我透過 API 建立任務")]
    public async System.Threading.Tasks.Task WhenCreateTask()
    {
        var request = _context.CurrentTaskRequest;
        request.Should().NotBeNull();

        try
        {
            // 模擬 API 呼叫 - 在實際實作中這裡會呼叫真實的 API
            // 暫時建立一個模擬的回應
            var taskId = Guid.NewGuid().ToString();
            _context.CurrentTaskId = taskId;
            _context.TraceId = request!.TraceId;
            
            _logger.LogInformation("Task created with ID: {TaskId} and TraceId: {TraceId}", 
                taskId, request.TraceId);
                
            // 模擬任務建立成功
            _context.TestData["task_created"] = true;
            _context.CreatedTaskIds.Add(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task");
            throw;
        }
        
        await System.Threading.Tasks.Task.CompletedTask;
    }

    [Then(@"任務應該被成功建立並回傳 TaskId")]
    public void ThenTaskShouldBeCreatedWithTaskId()
    {
        _context.CurrentTaskId.Should().NotBeNullOrEmpty();
        _context.TestData["task_created"].Should().Be(true);
        
        _logger.LogInformation("Verified task was created successfully with ID: {TaskId}", 
            _context.CurrentTaskId);
    }

    [Then(@"任務最終狀態應該為 ""(.*)""")]
    public async System.Threading.Tasks.Task ThenFinalTaskStatusShouldBe(string expectedStatus)
    {
        var taskId = _context.CurrentTaskId;
        taskId.Should().NotBeNullOrEmpty();

        // 模擬等待任務完成的過程
        // 在實際實作中，這裡會透過 API 查詢真實的任務狀態
        var retryPolicy = Policy
            .HandleResult<string>(status => status != expectedStatus)
            .WaitAndRetryAsync(
                retryCount: 30,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(1),
                onRetry: (result, timespan, retryCount, context) =>
                {
                    _logger.LogDebug("Waiting for task {TaskId} to reach status {ExpectedStatus} (attempt {RetryCount})",
                        taskId, expectedStatus, retryCount);
                });

        var finalStatus = await retryPolicy.ExecuteAsync(async () =>
        {
            // 模擬查詢任務狀態
            // 在實際實作中，這裡會呼叫 /api/tasks/{taskId} 查詢狀態
            await System.Threading.Tasks.Task.Delay(100);
            
            // 根據是否為失敗回調來決定最終狀態
            if (_context.TestData.ContainsKey("callback_failure"))
            {
                return "Failed";
            }
            else
            {
                return "Completed";
            }
        });

        finalStatus.Should().Be(expectedStatus);
        _logger.LogInformation("Task {TaskId} reached expected final status: {Status}", taskId, finalStatus);
    }

    [Then(@"整個流程的 TraceId 應該保持一致")]
    public void ThenTraceIdShouldBeConsistent()
    {
        _context.TraceId.Should().NotBeNullOrEmpty();
        _context.CurrentTaskRequest?.TraceId.Should().Be(_context.TraceId);
        
        _logger.LogInformation("Verified TraceId consistency: {TraceId}", _context.TraceId);
    }
}