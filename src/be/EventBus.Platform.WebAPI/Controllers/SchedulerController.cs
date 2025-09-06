using EventBus.Platform.WebAPI.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.Platform.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulerController(
    ISchedulerHandler schedulerHandler,
    ILogger<SchedulerController> logger) : ControllerBase
{
    [HttpPost("tasks")]
    public async Task<IActionResult> CreateSchedulerTaskAsync([FromBody] CreateSchedulerTaskRequest request, CancellationToken cancellationToken = default)
    {
        var result = await schedulerHandler.CreateSchedulerTaskAsync(request, cancellationToken);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return CreatedAtAction(nameof(GetSchedulerTaskAsync), 
            new { id = result.Success!.Id }, 
            result.Success);
    }

    [HttpGet("tasks/{id}")]
    public async Task<IActionResult> GetSchedulerTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await schedulerHandler.GetSchedulerTaskByIdAsync(id, cancellationToken);
        
        if (!result.IsSuccess)
        {
            if (result.Failure?.Code == "NotFound")
            {
                return NotFound(new { error = result.Failure?.Message });
            }
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return Ok(result.Success);
    }

    [HttpPut("tasks/{id}")]
    public async Task<IActionResult> UpdateSchedulerTaskAsync(string id, [FromBody] UpdateSchedulerTaskRequest request, CancellationToken cancellationToken = default)
    {
        var result = await schedulerHandler.UpdateSchedulerTaskAsync(id, request, cancellationToken);
        
        if (!result.IsSuccess)
        {
            if (result.Failure?.Code == "NotFound")
            {
                return NotFound(new { error = result.Failure?.Message });
            }
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return Ok(result.Success);
    }

    [HttpDelete("tasks/{id}")]
    public async Task<IActionResult> DeleteSchedulerTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await schedulerHandler.DeleteSchedulerTaskAsync(id, cancellationToken);
        
        if (!result.IsSuccess)
        {
            if (result.Failure?.Code == "NotFound")
            {
                return NotFound(new { error = result.Failure?.Message });
            }
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return NoContent();
    }

    [HttpPost("tasks/{id}/execute")]
    public async Task<IActionResult> ExecuteSchedulerTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await schedulerHandler.ExecuteSchedulerTaskAsync(id, cancellationToken);
        
        if (!result.IsSuccess)
        {
            if (result.Failure?.Code == "NotFound")
            {
                return NotFound(new { error = result.Failure?.Message });
            }
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return Ok(new { message = "Task executed successfully", executed = true });
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetScheduledTasksAsync([FromQuery] DateTime? beforeTime, [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var queryTime = beforeTime ?? DateTime.UtcNow;
        var result = await schedulerHandler.GetScheduledTasksAsync(queryTime, limit, cancellationToken);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Failure?.Message, code = result.Failure?.Code });
        }

        return Ok(new { 
            tasks = result.Success, 
            count = result.Success?.Count ?? 0,
            beforeTime = queryTime 
        });
    }

    [HttpPost("test-scheduler")]
    public async Task<IActionResult> TestSchedulerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test 1: Create a task scheduled for 5 seconds from now
            var request1 = new CreateSchedulerTaskRequest
            {
                TaskType = "test.immediate",
                Payload = "{\"message\": \"immediate test\"}",
                ScheduledAt = DateTime.UtcNow.AddSeconds(5),
                Priority = 1
            };

            var createResult1 = await schedulerHandler.CreateSchedulerTaskAsync(request1, cancellationToken);
            if (!createResult1.IsSuccess)
            {
                return BadRequest($"Failed to create immediate task: {createResult1.Failure?.Message}");
            }

            // Test 2: Create a task scheduled for 10 seconds from now
            var request2 = new CreateSchedulerTaskRequest
            {
                TaskType = "test.delayed",
                Payload = "{\"message\": \"delayed test\"}",
                ScheduledAt = DateTime.UtcNow.AddSeconds(10),
                Priority = 2
            };

            var createResult2 = await schedulerHandler.CreateSchedulerTaskAsync(request2, cancellationToken);
            if (!createResult2.IsSuccess)
            {
                return BadRequest($"Failed to create delayed task: {createResult2.Failure?.Message}");
            }

            // Test 3: Get scheduled tasks
            var getResult = await schedulerHandler.GetScheduledTasksAsync(DateTime.UtcNow.AddMinutes(1), 10, cancellationToken);
            if (!getResult.IsSuccess)
            {
                return BadRequest($"Failed to get scheduled tasks: {getResult.Failure?.Message}");
            }

            var testResults = new
            {
                ImmediateTaskCreated = createResult1.IsSuccess,
                ImmediateTaskId = createResult1.Success?.Id,
                ImmediateTaskScheduledAt = createResult1.Success?.ScheduledAt,
                DelayedTaskCreated = createResult2.IsSuccess,
                DelayedTaskId = createResult2.Success?.Id,
                DelayedTaskScheduledAt = createResult2.Success?.ScheduledAt,
                ScheduledTasksCount = getResult.Success?.Count ?? 0,
                Message = "Scheduler test completed! Tasks will execute automatically at their scheduled times."
            };

            logger.LogInformation("Scheduler test completed: {@TestResults}", testResults);

            return Ok(testResults);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduler test failed");
            return StatusCode(500, $"Scheduler test failed: {ex.Message}");
        }
    }
}