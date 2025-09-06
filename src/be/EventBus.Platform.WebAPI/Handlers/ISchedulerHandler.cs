using EventBus.Platform.WebAPI.Models;

namespace EventBus.Platform.WebAPI.Handlers;

public interface ISchedulerHandler
{
    Task<Result<SchedulerTaskEntity, Failure>> CreateSchedulerTaskAsync(CreateSchedulerTaskRequest request, CancellationToken cancellationToken = default);
    Task<Result<List<SchedulerTaskEntity>, Failure>> GetScheduledTasksAsync(DateTime beforeTime, int limit = 100, CancellationToken cancellationToken = default);
    Task<Result<SchedulerTaskEntity, Failure>> GetSchedulerTaskByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<SchedulerTaskEntity, Failure>> UpdateSchedulerTaskAsync(string id, UpdateSchedulerTaskRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool, Failure>> DeleteSchedulerTaskAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<bool, Failure>> ExecuteSchedulerTaskAsync(string id, CancellationToken cancellationToken = default);
}

public record CreateSchedulerTaskRequest
{
    public string TaskType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; } = DateTime.UtcNow;
    public int Priority { get; init; } = 0;
}

public record UpdateSchedulerTaskRequest
{
    public string? TaskType { get; init; }
    public string? Payload { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public int? Priority { get; init; }
    public string? Status { get; init; }
}