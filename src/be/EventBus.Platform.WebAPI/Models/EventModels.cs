namespace EventBus.Platform.WebAPI.Models;

public record PublishEventRequest
{
    public string EventType { get; init; } = string.Empty;
    public string EventData { get; init; } = string.Empty;
    public string? CallbackUrl { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
}

public record PublishEventResponse
{
    public string EventId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Status { get; init; } = "Published";
}

public record SubscribeRequest
{
    public string EventType { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string SubscriberName { get; init; } = string.Empty;
}