using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EventBus.Infrastructure.TraceContext;

public class TraceContextMiddleware(RequestDelegate next, ILogger<TraceContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.Request.Headers["X-Trace-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        var userId = context.Request.Headers["X-User-ID"].FirstOrDefault();

        var traceContext = new TraceContext
        {
            TraceId = traceId,
            UserId = userId
        };

        context.Items["TraceContext"] = traceContext;

        context.Response.Headers.Append("X-Trace-ID", traceId);

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = traceId,
            ["UserId"] = userId ?? "Anonymous"
        }))
        {
            logger.LogInformation("Request started for {Method} {Path}", 
                context.Request.Method, context.Request.Path);

            await next(context);

            logger.LogInformation("Request completed with status {StatusCode}", 
                context.Response.StatusCode);
        }
    }
}