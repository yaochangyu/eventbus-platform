using System.Collections.Concurrent;
using EventBus.Platform.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace EventBus.Platform.Dispatcher.Services;

public class InMemoryQueueService(ILogger<InMemoryQueueService> logger) : IQueueService
{
    private readonly ConcurrentQueue<TaskRequest> _taskQueue = new();

    public Task EnqueueTaskAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default)
    {
        _taskQueue.Enqueue(taskRequest);
        
        logger.LogInformation(
            "Task enqueued: {TaskId} for callback {CallbackUrl} - TraceId: {TraceId}",
            taskRequest.TaskId,
            taskRequest.CallbackUrl,
            taskRequest.TraceId);

        return Task.CompletedTask;
    }

    public Task<TaskRequest?> DequeueTaskAsync(CancellationToken cancellationToken = default)
    {
        if (_taskQueue.TryDequeue(out var taskRequest))
        {
            logger.LogDebug(
                "Task dequeued: {TaskId} - TraceId: {TraceId}",
                taskRequest.TaskId,
                taskRequest.TraceId);

            return Task.FromResult<TaskRequest?>(taskRequest);
        }

        return Task.FromResult<TaskRequest?>(null);
    }

    public Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_taskQueue.Count);
    }
}