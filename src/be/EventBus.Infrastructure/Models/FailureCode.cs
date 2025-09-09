namespace EventBus.Infrastructure.Models
{
    public enum FailureCode
    {
        Unauthorized,
        DbError,
        DuplicateEmail,
        DbConcurrency,
        ValidationError,
        InvalidOperation,
        Timeout,
        InternalServerError,
        Unknown
    }
}
