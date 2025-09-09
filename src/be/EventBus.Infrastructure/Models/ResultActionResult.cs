using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace EventBus.Infrastructure.Models;

/// <summary>
/// 統一處理 Result 到 ActionResult 的轉換
/// </summary>
/// <typeparam name="T">成功回傳的資料類型</typeparam>
public class ResultActionResult<T> : IActionResult
{
    private readonly Result<T, Failure> _result;

    public ResultActionResult(Result<T, Failure> result)
    {
        _result = result;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var controller = context.ActionDescriptor.RouteValues["controller"];
        var action = context.ActionDescriptor.RouteValues["action"];

        if (_result.IsSuccess)
        {
            var objectResult = new ObjectResult(_result.Success)
            {
                StatusCode = (int)HttpStatusCode.OK
            };
            await objectResult.ExecuteResultAsync(context);
        }
        else
        {
            var failure = _result.Failure!;
            var httpStatusCode = FailureCodeMapper.MapToHttpStatusCode(failure.Code);
            
            var errorResponse = new
            {
                error = failure.Message,
                code = failure.Code,
                traceId = failure.TraceId,
                data = failure.Data,
                details = failure.Details.Any() ? failure.Details.Select(d => new
                {
                    error = d.Message,
                    code = d.Code,
                    data = d.Data
                }) : null
            };

            var objectResult = new ObjectResult(errorResponse)
            {
                StatusCode = (int)httpStatusCode
            };
            
            await objectResult.ExecuteResultAsync(context);
        }
    }
}

/// <summary>
/// Result 擴充方法，提供統一的 ActionResult 轉換
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// 將 Result 轉換為 ActionResult
    /// </summary>
    /// <typeparam name="T">成功回傳的資料類型</typeparam>
    /// <param name="result">Result 物件</param>
    /// <returns>ActionResult</returns>
    public static IActionResult ToActionResult<T>(this Result<T, Failure> result)
    {
        return new ResultActionResult<T>(result);
    }

    /// <summary>
    /// 將 Result 轉換為 ActionResult，成功時使用 Created 狀態碼
    /// </summary>
    /// <typeparam name="T">成功回傳的資料類型</typeparam>
    /// <param name="result">Result 物件</param>
    /// <param name="location">資源位置 URI</param>
    /// <returns>ActionResult</returns>
    public static IActionResult ToCreatedActionResult<T>(this Result<T, Failure> result, string location)
    {
        if (result.IsSuccess)
        {
            return new CreatedResult(location, result.Success);
        }
        
        return result.ToActionResult();
    }

    /// <summary>
    /// 將 Result 轉換為 ActionResult，成功時使用 NoContent 狀態碼
    /// </summary>
    /// <typeparam name="T">成功回傳的資料類型</typeparam>
    /// <param name="result">Result 物件</param>
    /// <returns>ActionResult</returns>
    public static IActionResult ToNoContentActionResult<T>(this Result<T, Failure> result)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }
        
        return result.ToActionResult();
    }

    /// <summary>
    /// 將 Result 轉換為 ActionResult，成功時使用 Accepted 狀態碼
    /// </summary>
    /// <typeparam name="T">成功回傳的資料類型</typeparam>
    /// <param name="result">Result 物件</param>
    /// <param name="location">資源位置 URI</param>
    /// <returns>ActionResult</returns>
    public static IActionResult ToAcceptedActionResult<T>(this Result<T, Failure> result, string? location = null)
    {
        if (result.IsSuccess)
        {
            return new AcceptedResult(location, result.Success);
        }
        
        return result.ToActionResult();
    }
}