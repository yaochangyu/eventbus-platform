namespace EventBus.Infrastructure;

public class GuidIdGenerator : IIdGenerator
{
    public string Generate()
    {
        return Guid.NewGuid().ToString();
    }
}