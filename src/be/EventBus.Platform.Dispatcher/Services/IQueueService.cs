using EventBus.Platform.Dispatcher.Models;

namespace EventBus.Platform.Dispatcher.Services;

public interface IQueueService
{
    Task EnqueueTaskAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default);
    Task<TaskRequest?> DequeueTaskAsync(CancellationToken cancellationToken = default);
    Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default);
}