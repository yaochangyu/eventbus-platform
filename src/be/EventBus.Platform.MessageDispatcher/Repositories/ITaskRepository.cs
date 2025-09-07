using EventBus.Platform.MessageDispatcher.Models;

namespace EventBus.Platform.MessageDispatcher.Repositories;

public interface ITaskRepository
{
    Task CreateTaskAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default);
    Task<TaskEntity?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default);
    Task UpdateTaskStatusAsync(string taskId, MessageStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task<List<TaskEntity>> GetPendingTasksAsync(CancellationToken cancellationToken = default);
}