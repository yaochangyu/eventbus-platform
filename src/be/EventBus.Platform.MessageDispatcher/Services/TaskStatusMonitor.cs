using EventBus.Platform.MessageDispatcher.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.MessageDispatcher.Services;

public class TaskStatusMonitor(
    ITaskRepository taskRepository,
    ILogger<TaskStatusMonitor> logger) : BackgroundService
{
    private readonly TimeSpan _monitorInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TaskStatusMonitor started");

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportTaskStatusAsync(stoppingToken);
                await Task.Delay(_monitorInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("TaskStatusMonitor cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in TaskStatusMonitor execution");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("TaskStatusMonitor stopped");
    }

    private async Task ReportTaskStatusAsync(CancellationToken cancellationToken)
    {
        var pendingTasks = await taskRepository.GetPendingTasksAsync(cancellationToken);
        
        if (pendingTasks.Count > 0)
        {
            logger.LogInformation("=== Task Status Report ===");
            logger.LogInformation("Total pending tasks: {PendingCount}", pendingTasks.Count);
            
            foreach (var task in pendingTasks.Take(5))
            {
                logger.LogInformation(
                    "Task {TaskId}: Status={Status}, Created={CreatedAt}, CallbackUrl={CallbackUrl}, TraceId={TraceId}",
                    task.Id[..8] + "...",
                    task.Status,
                    task.CreatedAt.ToString("HH:mm:ss"),
                    task.CallbackUrl,
                    task.TraceId);
            }
            
            if (pendingTasks.Count > 5)
            {
                logger.LogInformation("... and {MoreCount} more tasks", pendingTasks.Count - 5);
            }
            
            logger.LogInformation("=========================");
        }
        else
        {
            logger.LogInformation("No pending tasks in repository");
        }
    }
}