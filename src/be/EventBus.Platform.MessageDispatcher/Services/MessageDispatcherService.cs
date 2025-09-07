using EventBus.Platform.MessageDispatcher.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.MessageDispatcher.Services;

public class MessageDispatcherService(
    IQueueService queueService,
    ITaskRepository taskRepository,
    ILogger<MessageDispatcherService> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MessageDispatcherService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedTasksAsync(stoppingToken);
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("MessageDispatcherService cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in MessageDispatcherService execution");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("MessageDispatcherService stopped");
    }

    private async Task ProcessQueuedTasksAsync(CancellationToken cancellationToken)
    {
        var queueCount = await queueService.GetQueueCountAsync(cancellationToken);
        if (queueCount == 0)
        {
            return;
        }

        logger.LogDebug("Processing {QueueCount} queued tasks", queueCount);

        while (await queueService.DequeueTaskAsync(cancellationToken) is { } taskRequest)
        {
            try
            {
                await taskRepository.CreateTaskAsync(taskRequest, cancellationToken);
                
                logger.LogInformation(
                    "Task moved from queue to repository: {TaskId} - TraceId: {TraceId}",
                    taskRequest.TaskId,
                    taskRequest.TraceId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to move task from queue to repository: {TaskId} - TraceId: {TraceId}",
                    taskRequest.TaskId,
                    taskRequest.TraceId);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("MessageDispatcherService is stopping...");
        await base.StopAsync(cancellationToken);
        logger.LogInformation("MessageDispatcherService stopped gracefully");
    }
}