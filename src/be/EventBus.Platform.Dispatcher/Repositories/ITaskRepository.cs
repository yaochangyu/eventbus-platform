using EventBus.Platform.Dispatcher.Models;

namespace EventBus.Platform.Dispatcher.Repositories;

public interface ITaskRepository
{
    Task CreateTaskAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default);
    Task<TaskEntity?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default);
    Task UpdateTaskStatusAsync(string taskId, MessageStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task<List<TaskEntity>> GetPendingTasksAsync(CancellationToken cancellationToken = default);
}