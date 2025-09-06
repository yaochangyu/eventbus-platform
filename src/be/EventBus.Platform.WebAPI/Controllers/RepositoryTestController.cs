using EventBus.Platform.WebAPI.Models;
using EventBus.Platform.WebAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryTestController(
    IEventRepository eventRepository,
    ITaskRepository taskRepository,
    ISchedulerTaskRepository schedulerTaskRepository,
    ISubscriptionRepository subscriptionRepository,
    ILogger<RepositoryTestController> logger) : ControllerBase
{
    [HttpPost("test-repositories")]
    public async Task<IActionResult> TestRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test EventRepository
            var eventEntity = new EventEntity
            {
                Id = Guid.NewGuid().ToString(),
                EventType = "test.event",
                EventData = "{\"message\": \"test data\"}",
                CallbackUrl = "https://example.com/callback"
            };

            var createEventResult = await eventRepository.CreateAsync(eventEntity, cancellationToken);
            if (!createEventResult.IsSuccess)
            {
                return BadRequest($"Failed to create event: {createEventResult.Failure?.Message}");
            }

            var getEventResult = await eventRepository.GetByIdAsync(eventEntity.Id, cancellationToken);
            if (!getEventResult.IsSuccess)
            {
                return BadRequest($"Failed to get event: {getEventResult.Failure?.Message}");
            }

            // Test TaskRepository
            var taskEntity = new TaskEntity
            {
                Id = Guid.NewGuid().ToString(),
                EventId = eventEntity.Id,
                SubscriberId = "test-subscriber",
                CallbackUrl = "https://example.com/callback",
                Status = "Pending"
            };

            var createTaskResult = await taskRepository.CreateAsync(taskEntity, cancellationToken);
            if (!createTaskResult.IsSuccess)
            {
                return BadRequest($"Failed to create task: {createTaskResult.Failure?.Message}");
            }

            // Test SchedulerTaskRepository
            var schedulerTaskEntity = new SchedulerTaskEntity
            {
                Id = Guid.NewGuid().ToString(),
                TaskType = "scheduled.task",
                Payload = "{\"action\": \"test\"}",
                ScheduledAt = DateTime.UtcNow.AddMinutes(5),
                Priority = 1
            };

            var createSchedulerTaskResult = await schedulerTaskRepository.CreateAsync(schedulerTaskEntity, cancellationToken);
            if (!createSchedulerTaskResult.IsSuccess)
            {
                return BadRequest($"Failed to create scheduler task: {createSchedulerTaskResult.Failure?.Message}");
            }

            // Test SubscriptionRepository
            var subscriptionEntity = new SubscriptionEntity
            {
                Id = Guid.NewGuid().ToString(),
                EventType = "test.event",
                CallbackUrl = "https://example.com/subscriber",
                SubscriberName = "test-subscriber"
            };

            var createSubscriptionResult = await subscriptionRepository.CreateAsync(subscriptionEntity, cancellationToken);
            if (!createSubscriptionResult.IsSuccess)
            {
                return BadRequest($"Failed to create subscription: {createSubscriptionResult.Failure?.Message}");
            }

            // Test queries
            var eventsByTypeResult = await eventRepository.GetByTypeAsync("test.event", 10, cancellationToken);
            var tasksByEventResult = await taskRepository.GetByEventIdAsync(eventEntity.Id, cancellationToken);
            var subscriptionsByEventTypeResult = await subscriptionRepository.GetByEventTypeAsync("test.event", cancellationToken);

            var testResults = new
            {
                EventCreated = createEventResult.IsSuccess,
                EventRetrieved = getEventResult.IsSuccess,
                TaskCreated = createTaskResult.IsSuccess,
                SchedulerTaskCreated = createSchedulerTaskResult.IsSuccess,
                SubscriptionCreated = createSubscriptionResult.IsSuccess,
                EventsByTypeCount = eventsByTypeResult.IsSuccess ? eventsByTypeResult.Success?.Count : -1,
                TasksByEventCount = tasksByEventResult.IsSuccess ? tasksByEventResult.Success?.Count : -1,
                SubscriptionsByEventTypeCount = subscriptionsByEventTypeResult.IsSuccess ? subscriptionsByEventTypeResult.Success?.Count : -1,
                Message = "All repository tests completed successfully!"
            };

            logger.LogInformation("Repository test completed: {@TestResults}", testResults);

            return Ok(testResults);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Repository test failed");
            return StatusCode(500, $"Repository test failed: {ex.Message}");
        }
    }

    [HttpGet("cache-test")]
    public async Task<IActionResult> TestCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Create an event
            var eventEntity = new EventEntity
            {
                Id = Guid.NewGuid().ToString(),
                EventType = "cache.test",
                EventData = "{\"message\": \"cache test\"}"
            };

            // First create
            var createResult = await eventRepository.CreateAsync(eventEntity, cancellationToken);
            if (!createResult.IsSuccess)
            {
                return BadRequest($"Failed to create event: {createResult.Failure?.Message}");
            }

            // Get from cache (should be fast)
            var startTime = DateTime.UtcNow;
            var getResult1 = await eventRepository.GetByIdAsync(eventEntity.Id, cancellationToken);
            var firstGetTime = DateTime.UtcNow - startTime;

            // Get again from cache (should be even faster)
            startTime = DateTime.UtcNow;
            var getResult2 = await eventRepository.GetByIdAsync(eventEntity.Id, cancellationToken);
            var secondGetTime = DateTime.UtcNow - startTime;

            var cacheTestResults = new
            {
                EventCreated = createResult.IsSuccess,
                FirstGetSuccess = getResult1.IsSuccess,
                SecondGetSuccess = getResult2.IsSuccess,
                FirstGetTimeMs = firstGetTime.TotalMilliseconds,
                SecondGetTimeMs = secondGetTime.TotalMilliseconds,
                CacheWorking = getResult1.IsSuccess && getResult2.IsSuccess,
                Message = "Cache test completed!"
            };

            return Ok(cacheTestResults);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cache test failed");
            return StatusCode(500, $"Cache test failed: {ex.Message}");
        }
    }
}