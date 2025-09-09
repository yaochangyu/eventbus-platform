using EventBus.Infrastructure.Queue;
using EventBus.Infrastructure.TraceContext;
using EventBus.Platform.WebAPI.Handlers;
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
        // 驗證請求參數
        if (string.IsNullOrWhiteSpace(queueName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Queue name is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        if (request.Item == null)
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Item is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var traceContext = traceContextGetter.GetContext();
        
        var enqueueResult = await queueService.EnqueueAsync(request.Item, queueName);
        
        // 轉換為統一的 Result 回應
        var responseResult = enqueueResult.IsSuccess 
            ? Result<object, Failure>.Ok(new
            {
                QueueName = queueName,
                Item = request.Item,
                QueueCount = queueService.GetCount(queueName),
                TraceId = traceContext?.TraceId,
                EnqueuedAt = DateTime.UtcNow
            })
            : Result<object, Failure>.Fail(enqueueResult.Failure!);

        return responseResult.ToActionResult();
    }

    [HttpPost("{queueName}/dequeue")]
    public async Task<IActionResult> Dequeue(string queueName)
    {
        // 驗證請求參數
        if (string.IsNullOrWhiteSpace(queueName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Queue name is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var traceContext = traceContextGetter.GetContext();
        
        var dequeueResult = await queueService.DequeueAsync<object>(queueName);
        
        // 轉換為統一的 Result 回應
        var responseResult = dequeueResult.IsSuccess 
            ? Result<object, Failure>.Ok(new
            {
                QueueName = queueName,
                Item = dequeueResult.Success,
                Found = dequeueResult.Success != null,
                RemainingCount = queueService.GetCount(queueName),
                TraceId = traceContext?.TraceId,
                DequeuedAt = DateTime.UtcNow
            })
            : Result<object, Failure>.Fail(dequeueResult.Failure!);

        return responseResult.ToActionResult();
    }

    [HttpGet("{queueName}/status")]
    public IActionResult GetStatus(string queueName)
    {
        // 驗證請求參數
        if (string.IsNullOrWhiteSpace(queueName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Queue name is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var traceContext = traceContextGetter.GetContext();
        
        var result = Result<object, Failure>.Ok(new
        {
            QueueName = queueName,
            Count = queueService.GetCount(queueName),
            IsEmpty = queueService.IsEmpty(queueName),
            TraceId = traceContext?.TraceId,
            Timestamp = DateTime.UtcNow
        });

        return result.ToActionResult();
    }

    /// <summary>
    /// Dequeue task request and store it in the database
    /// Used by Message Dispatcher Console to process task requests from queues
    /// Based on design.md Message Processing flow
    /// </summary>
    [HttpPost("dequeue-task")]
    public async Task<IActionResult> DequeueTask([FromQuery] string queueName = "immediate-tasks")
    {
        // 驗證請求參數
        if (string.IsNullOrWhiteSpace(queueName))
        {
            var validationFailure = new Failure(nameof(FailureCode.ValidationError), "Queue name is required");
            return Result<object, Failure>.Fail(validationFailure).ToActionResult();
        }

        var traceContext = traceContextGetter.GetContext();
        
        try
        {
            // Try to dequeue task request from specified queue
            var result = await queueService.TryDequeueAsync<CreateTaskRequest>(queueName);
            
            if (!result.IsSuccess || result.Success == null)
            {
                logger.LogDebug("No tasks available in queue: {QueueName} - TraceId: {TraceId}", 
                    queueName, traceContext?.TraceId);
                
                var emptyQueueResponse = Result<object, Failure>.Ok(new
                {
                    QueueName = queueName,
                    Success = false,
                    Message = "No tasks available in queue",
                    RemainingCount = queueService.GetCount(queueName),
                    TraceId = traceContext?.TraceId,
                    Timestamp = DateTime.UtcNow
                });

                return emptyQueueResponse.ToActionResult();
            }

            var taskRequest = result.Success!;
            logger.LogInformation("Dequeued task request from {QueueName} - TraceId: {TraceId}", 
                queueName, traceContext?.TraceId);

            // Store the task in the database
            var createResult = await taskHandler.CreateTaskAsync(taskRequest);
            
            if (!createResult.IsSuccess)
            {
                // If we fail to store, we should re-enqueue the task to avoid losing it
                var reEnqueueResult = await queueService.EnqueueAsync(taskRequest, queueName);
                if (!reEnqueueResult.IsSuccess)
                {
                    logger.LogError("Failed to re-enqueue task after database error: {Error}", reEnqueueResult.Failure?.Message);
                }
                
                // 返回統一的失敗格式
                return Result<object, Failure>.Fail(createResult.Failure!).ToActionResult();
            }

            var task = createResult.Success!;
            logger.LogInformation("Task stored successfully: {TaskId} with Status: {Status} - TraceId: {TraceId}", 
                task.Id, task.Status, traceContext?.TraceId);

            var successResponse = Result<object, Failure>.Ok(new
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

            return successResponse.ToActionResult();
        }
        catch (Exception ex)
        {
            var internalErrorFailure = new Failure(nameof(FailureCode.InternalServerError), "Internal server error")
            {
                TraceId = traceContext?.TraceId,
                Exception = ex
            };
            
            return Result<object, Failure>.Fail(internalErrorFailure).ToActionResult();
        }
    }
}

public record EnqueueRequest
{
    public object Item { get; init; } = string.Empty;
}

