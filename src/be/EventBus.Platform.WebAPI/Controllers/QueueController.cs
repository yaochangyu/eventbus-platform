using EventBus.Infrastructure.Queue;
using EventBus.Infrastructure.TraceContext;
using EventBus.Platform.WebAPI.Handlers;
using EventBus.Infrastructure.Models;
using EventBus.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueueController(
    IQueueProvider queueService,
    IContextGetter<TraceContext?> traceContextGetter,
    ITaskHandler taskHandler,
    ILogger<QueueController> logger) : ControllerBase
{
    [HttpPost("{queueName}/enqueue")]
    public async Task<IActionResult> Enqueue(string queueName, [FromBody] EnqueueRequest request)
    {
        var traceContext = traceContextGetter.GetContext();
        
        var enqueueResult = await queueService.EnqueueAsync(request.Item, queueName);
        
        if (!enqueueResult.IsSuccess)
        {
            logger.LogError("Failed to enqueue item to queue {QueueName}: {Error} - Exception: {Exception}", 
                queueName, enqueueResult.Failure?.Message, enqueueResult.Failure?.Exception);
            return BadRequest(new 
            {
                Error = enqueueResult.Failure?.Message,
                Code = enqueueResult.Failure?.Code
            });
        }
        
        return Ok(new
        {
            QueueName = queueName,
            Item = request.Item,
            QueueCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            EnqueuedAt = DateTime.UtcNow
        });
    }

    [HttpPost("{queueName}/dequeue")]
    public async Task<IActionResult> Dequeue(string queueName)
    {
        var traceContext = traceContextGetter.GetContext();
        
        var dequeueResult = await queueService.DequeueAsync<object>(queueName);
        
        if (!dequeueResult.IsSuccess)
        {
            logger.LogError("Failed to dequeue item from queue {QueueName}: {Error} - Exception: {Exception}", 
                queueName, dequeueResult.Failure?.Message, dequeueResult.Failure?.Exception);
            return BadRequest(new 
            {
                Error = dequeueResult.Failure?.Message,
                Code = dequeueResult.Failure?.Code
            });
        }
        
        var item = dequeueResult.Success;
        
        return Ok(new
        {
            QueueName = queueName,
            Item = item,
            Found = item != null,
            RemainingCount = queueService.GetCount(queueName),
            TraceId = traceContext?.TraceId,
            DequeuedAt = DateTime.UtcNow
        });
    }

    [HttpGet("{queueName}/status")]
    public IActionResult GetStatus(string queueName)
    {
        var traceContext = traceContextGetter.GetContext();
        
        return Ok(new
        {
            QueueName = queueName,
            Count = queueService.GetCount(queueName),
            IsEmpty = queueService.IsEmpty(queueName),
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Dequeue task request and store it in the database
    /// Used by Message Dispatcher Console to process task requests from queues
    /// Based on design.md Message Processing flow
    /// </summary>
    [HttpPost("dequeue-task")]
    public async Task<IActionResult> DequeueTask([FromQuery] string queueName = "immediate-tasks")
    {
        var traceContext = traceContextGetter.GetContext();
        
        try
        {
            // Try to dequeue task request from specified queue
            var result = await queueService.TryDequeueAsync<CreateTaskRequest>(queueName);
            
            if (!result.IsSuccess || result.Success == null)
            {
                logger.LogDebug("No tasks available in queue: {QueueName} - TraceId: {TraceId}", 
                    queueName, traceContext?.TraceId);
                
                return Ok(new
                {
                    QueueName = queueName,
                    Success = false,
                    Message = "No tasks available in queue",
                    RemainingCount = queueService.GetCount(queueName),
                    TraceId = traceContext?.TraceId,
                    Timestamp = DateTime.UtcNow
                });
            }

            var taskRequest = result.Success!;
            logger.LogInformation("Dequeued task request from {QueueName} - TraceId: {TraceId}", 
                queueName, traceContext?.TraceId);

            // Store the task in the database
            var createResult = await taskHandler.CreateTaskAsync(taskRequest);
            
            if (!createResult.IsSuccess)
            {
                logger.LogError("Failed to store dequeued task in database: {Error} - TraceId: {TraceId}", 
                    createResult.Failure?.Message, traceContext?.TraceId);
                
                // If we fail to store, we should re-enqueue the task to avoid losing it
                var reEnqueueResult = await queueService.EnqueueAsync(taskRequest, queueName);
                if (!reEnqueueResult.IsSuccess)
                {
                    logger.LogError("Failed to re-enqueue task after database error: {Error}", reEnqueueResult.Failure?.Message);
                }
                
                return StatusCode(500, new
                {
                    error = "Failed to store task in database",
                    details = createResult.Failure?.Message,
                    code = createResult.Failure?.Code,
                    TraceId = traceContext?.TraceId
                });
            }

            var task = createResult.Success!;
            logger.LogInformation("Task stored successfully: {TaskId} with Status: {Status} - TraceId: {TraceId}", 
                task.Id, task.Status, traceContext?.TraceId);

            return Ok(new
            {
                QueueName = queueName,
                Success = true,
                TaskId = task.Id,
                Status = task.Status,
                CallbackUrl = task.CallbackUrl,
                ScheduledAt = task.ScheduledAt,
                RemainingCount = queueService.GetCount(queueName),
                TraceId = traceContext?.TraceId,
                DequeuedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in DequeueTask for queue: {QueueName} - TraceId: {TraceId}", 
                queueName, traceContext?.TraceId);
            
            return StatusCode(500, new
            {
                error = "Internal server error",
                code = "InternalError",
                TraceId = traceContext?.TraceId
            });
        }
    }
}

public record EnqueueRequest
{
    public object Item { get; init; } = string.Empty;
}

