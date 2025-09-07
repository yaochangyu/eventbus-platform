using EventBus.Platform.MessageDispatcher.Models;

namespace EventBus.Platform.MessageDispatcher.Services;

public interface IQueueService
{
    Task EnqueueTaskAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default);
    Task<TaskRequest?> DequeueTaskAsync(CancellationToken cancellationToken = default);
    Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default);
}