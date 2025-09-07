using EventBus.Platform.Dispatcher.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.Dispatcher.Services;

public class DemoTaskGenerator(
    IQueueService queueService,
    ILogger<DemoTaskGenerator> logger) : BackgroundService
{
    private readonly TimeSpan _generateInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DemoTaskGenerator started");

        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        var counter = 1;
        while (!stoppingToken.IsCancellationRequested && counter <= 5)
        {
            try
            {
                await GenerateDemoTaskAsync(counter, stoppingToken);
                counter++;
                await Task.Delay(_generateInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("DemoTaskGenerator cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DemoTaskGenerator execution");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("DemoTaskGenerator completed all demo tasks");
    }

    private async Task GenerateDemoTaskAsync(int taskNumber, CancellationToken cancellationToken)
    {
        var taskRequest = new TaskRequest
        {
            TaskId = Guid.NewGuid().ToString(),
            CallbackUrl = $"https://httpbin.org/post?task={taskNumber}",
            Method = HttpMethod.Post,
            RequestPayload = new 
            { 
                message = $"Demo task {taskNumber}", 
                timestamp = DateTime.UtcNow,
                taskNumber = taskNumber
            },
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["X-Task-Source"] = "DemoTaskGenerator"
            },
            TraceId = Guid.NewGuid().ToString()[..8],
            MaxRetries = 3,
            Timeout = TimeSpan.FromSeconds(30)
        };

        await queueService.EnqueueTaskAsync(taskRequest, cancellationToken);

        logger.LogInformation(
            "Generated demo task {TaskNumber}: {TaskId} - TraceId: {TraceId}",
            taskNumber,
            taskRequest.TaskId,
            taskRequest.TraceId);
    }
}