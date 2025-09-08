using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Repositories;

public interface IEventRepository
{
    Task<Result<EventEntity, Failure>> CreateAsync(EventEntity eventEntity, CancellationToken cancellationToken = default);
    Task<Result<EventEntity, Failure>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<EventEntity, Failure>> UpdateAsync(EventEntity eventEntity, CancellationToken cancellationToken = default);
    Task<Result<bool, Failure>> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<List<EventEntity>, Failure>> GetByTypeAsync(string eventType, int limit = 100, CancellationToken cancellationToken = default);
    Task<Result<List<EventEntity>, Failure>> GetByStatusAsync(string status, int limit = 100, CancellationToken cancellationToken = default);
    Task<Result<List<EventEntity>, Failure>> GetPendingEventsAsync(int limit = 100, CancellationToken cancellationToken = default);
}

public interface ITaskRepository
{
    Task<Result<TaskEntity, Failure>> CreateAsync(TaskEntity taskEntity, CancellationToken cancellationToken = default);
    Task<Result<TaskEntity, Failure>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<TaskEntity, Failure>> UpdateAsync(TaskEntity taskEntity, CancellationToken cancellationToken = default);
    Task<Result<bool, Failure>> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<List<TaskEntity>, Failure>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<Result<List<TaskEntity>, Failure>> GetByStatusAsync(string status, int limit = 100, CancellationToken cancellationToken = default);
    Task<Result<List<TaskEntity>, Failure>> GetPendingTasksAsync(int limit = 100, CancellationToken cancellationToken = default);
}