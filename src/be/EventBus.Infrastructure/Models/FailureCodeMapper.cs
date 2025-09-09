using System.Net;

namespace EventBus.Infrastructure.Models;

/// <summary>
/// 將 FailureCode 映射至對應的 HTTP 狀態碼
/// </summary>
public static class FailureCodeMapper
{
    private static readonly Dictionary<FailureCode, HttpStatusCode> _mappings = new()
    {
        { FailureCode.Unknown, HttpStatusCode.InternalServerError },
        { FailureCode.Unauthorized, HttpStatusCode.Unauthorized },
        { FailureCode.NotFound, HttpStatusCode.NotFound },
        { FailureCode.ValidationError, HttpStatusCode.BadRequest },
        { FailureCode.InvalidOperation, HttpStatusCode.BadRequest },
        { FailureCode.DuplicateId, HttpStatusCode.Conflict },
        { FailureCode.DuplicateEmail, HttpStatusCode.Conflict },
        { FailureCode.DbError, HttpStatusCode.InternalServerError },
        { FailureCode.DbConcurrency, HttpStatusCode.Conflict },
        { FailureCode.Timeout, HttpStatusCode.RequestTimeout },
        { FailureCode.InternalServerError, HttpStatusCode.InternalServerError },
        { FailureCode.ConfigExists, HttpStatusCode.Conflict },
        { FailureCode.CreateConfigFailed, HttpStatusCode.InternalServerError },
        { FailureCode.GetConfigFailed, HttpStatusCode.InternalServerError },
        { FailureCode.UpdateConfigFailed, HttpStatusCode.InternalServerError },
        { FailureCode.DeleteConfigFailed, HttpStatusCode.InternalServerError },
        { FailureCode.GetAllConfigsFailed, HttpStatusCode.InternalServerError },
        { FailureCode.EnqueueError, HttpStatusCode.InternalServerError },
        { FailureCode.DequeueError, HttpStatusCode.InternalServerError },
        { FailureCode.TryDequeueError, HttpStatusCode.InternalServerError },
        { FailureCode.PeekError, HttpStatusCode.InternalServerError },
        { FailureCode.ClearError, HttpStatusCode.InternalServerError },
        { FailureCode.ClearQueueError, HttpStatusCode.InternalServerError },
        { FailureCode.ScheduledTaskQueryFailed, HttpStatusCode.InternalServerError },
        { FailureCode.ScheduledTaskUpdateFailed, HttpStatusCode.InternalServerError }
    };

    /// <summary>
    /// 將 FailureCode 轉換為對應的 HTTP 狀態碼
    /// </summary>
    /// <param name="failureCode">錯誤代碼</param>
    /// <returns>對應的 HTTP 狀態碼</returns>
    public static HttpStatusCode MapToHttpStatusCode(FailureCode failureCode)
    {
        return _mappings.TryGetValue(failureCode, out var statusCode) 
            ? statusCode 
            : HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// 從錯誤代碼字串轉換為 HTTP 狀態碼
    /// </summary>
    /// <param name="failureCodeString">錯誤代碼字串</param>
    /// <returns>對應的 HTTP 狀態碼</returns>
    public static HttpStatusCode MapToHttpStatusCode(string failureCodeString)
    {
        if (Enum.TryParse<FailureCode>(failureCodeString, out var failureCode))
        {
            return MapToHttpStatusCode(failureCode);
        }
        
        return HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// 取得所有支援的錯誤代碼映射
    /// </summary>
    /// <returns>錯誤代碼與 HTTP 狀態碼的映射字典</returns>
    public static IReadOnlyDictionary<FailureCode, HttpStatusCode> GetAllMappings()
    {
        return _mappings.AsReadOnly();
    }
}