namespace EventBus.Infrastructure.TraceContext;

public record TraceContext
{
    public string TraceId { get; init; } = Guid.NewGuid().ToString();
    public string? UserId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}