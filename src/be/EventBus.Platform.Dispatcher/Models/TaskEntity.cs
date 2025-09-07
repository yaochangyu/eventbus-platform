namespace EventBus.Platform.Dispatcher.Models;

public record TaskEntity
{
    public string Id { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public string RequestPayload { get; init; } = string.Empty;
    public string Headers { get; init; } = string.Empty;
    public int MaxRetries { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 30;
    public string? TraceId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; init; }
    public MessageStatus Status { get; init; } = MessageStatus.Pending;
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; } = 0;
}

public enum MessageStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Timeout,
    Cancelled
}