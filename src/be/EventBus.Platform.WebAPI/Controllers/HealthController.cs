using EventBus.Infrastructure.TraceContext;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(IContextGetter<TraceContext?> traceContextGetter) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var traceContext = traceContextGetter.GetContext();
        
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0-MVP",
            TraceId = traceContext?.TraceId,
            UserId = traceContext?.UserId
        });
    }

    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        return Ok(new
        {
            Version = "1.0.0-MVP",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Framework = ".NET 9.0"
        });
    }
}