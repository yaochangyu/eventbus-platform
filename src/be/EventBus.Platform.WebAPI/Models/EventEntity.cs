namespace EventBus.Platform.WebAPI.Models;

public record EventEntity
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string EventType { get; init; } = string.Empty;
    public string EventData { get; init; } = string.Empty;
    public string? CallbackUrl { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
    public string Status { get; init; } = "Published";
    public int RetryCount { get; init; } = 0;
    public DateTime? LastRetryAt { get; init; }
    public string? ErrorMessage { get; init; }
}

public record TaskEntity
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string EventId { get; init; } = string.Empty;
    public string SubscriberId { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string Status { get; init; } = "Pending";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; } = 0;
    public string? ErrorMessage { get; init; }
}

public record SchedulerTaskEntity
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TaskType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; } = DateTime.UtcNow;
    public DateTime? ExecutedAt { get; init; }
    public string Status { get; init; } = "Scheduled";
    public int Priority { get; init; } = 0;
    public int RetryCount { get; init; } = 0;
    public string? ErrorMessage { get; init; }
}

public record SubscriptionEntity
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string EventType { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string SubscriberName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool IsActive { get; init; } = true;
    public Dictionary<string, string>? Headers { get; init; }
}