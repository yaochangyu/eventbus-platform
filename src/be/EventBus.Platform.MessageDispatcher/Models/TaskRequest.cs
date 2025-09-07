namespace EventBus.Platform.MessageDispatcher.Models;

public record TaskRequest
{
    public string TaskId { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public HttpMethod Method { get; init; } = HttpMethod.Post;
    public object RequestPayload { get; init; } = new();
    public Dictionary<string, string> Headers { get; init; } = new();
    public int MaxRetries { get; init; } = 3;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public string? TraceId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}