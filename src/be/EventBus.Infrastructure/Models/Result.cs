namespace EventBus.Infrastructure.Models;

public record Result<TSuccess, TFailure>
{
    public bool IsSuccess { get; init; }
    public TSuccess? Success { get; init; }
    public TFailure? Failure { get; init; }

    public static Result<TSuccess, TFailure> Ok(TSuccess success) =>
        new() { IsSuccess = true, Success = success };

    public static Result<TSuccess, TFailure> Fail(TFailure failure) =>
        new() { IsSuccess = false, Failure = failure };
}

public record Failure(string Message, string? Code = null);