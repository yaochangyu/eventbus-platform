namespace EventBus.Infrastructure.TraceContext;

public interface IContextGetter<T>
{
    T GetContext();
}