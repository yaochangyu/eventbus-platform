using Microsoft.AspNetCore.Http;

namespace EventBus.Infrastructure.TraceContext;

public class TraceContextGetter(IHttpContextAccessor httpContextAccessor) : IContextGetter<TraceContext?>
{
    public TraceContext? GetContext()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue("TraceContext", out var context) == true)
        {
            return context as TraceContext;
        }
        
        return null;
    }
}