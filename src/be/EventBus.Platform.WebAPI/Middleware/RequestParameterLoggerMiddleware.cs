using System.Text.Json;
using EventBus.Infrastructure.TraceContext;
using EventBus.Platform.WebAPI.Infrastructure;

namespace EventBus.Platform.WebAPI.Middleware;

/// <summary>
/// 請求參數記錄中介軟體
/// 當請求成功完成時記錄請求資訊，避免與例外處理中介軟體重複記錄
/// </summary>
public class RequestParameterLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestParameterLoggerMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RequestParameterLoggerMiddleware(
        RequestDelegate next,
        ILogger<RequestParameterLoggerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        
        // 設定 JSON 序列化選項
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        await _next(httpContext);

        // 只在以下情況記錄請求資訊：
        // 1. 2xx 成功狀態碼
        // 2. 4xx 客戶端錯誤（業務邏輯錯誤）
        // 避免在 5xx 伺服器錯誤時重複記錄（已在 ExceptionHandlingMiddleware 中記錄）
        if (ShouldLogRequest(httpContext))
        {
            await LogRequestParametersAsync(httpContext);
        }
    }

    /// <summary>
    /// 判斷是否應該記錄請求資訊
    /// </summary>
    private static bool ShouldLogRequest(HttpContext context)
    {
        var statusCode = context.Response.StatusCode;
        
        // 記錄 2xx 成功狀態碼
        if (statusCode >= 200 && statusCode < 300)
        {
            return true;
        }
        
        // 記錄 4xx 客戶端錯誤（業務邏輯錯誤）
        if (statusCode >= 400 && statusCode < 500)
        {
            return true;
        }
        
        // 不記錄 5xx 伺服器錯誤（避免與 ExceptionHandlingMiddleware 重複）
        return false;
    }

    /// <summary>
    /// 記錄請求參數
    /// </summary>
    private async Task LogRequestParametersAsync(HttpContext context)
    {
        try
        {
            var traceContext = GetTraceContext(context);
            var requestInfo = await RequestInfoExtractor.ExtractRequestInfoAsync(context, _jsonOptions);

            // 根據狀態碼決定日誌層級
            var statusCode = context.Response.StatusCode;
            var logLevel = GetLogLevel(statusCode);
            var logMessage = GetLogMessage(statusCode);

            // 使用結構化日誌記錄
            _logger.Log(logLevel,
                "{LogMessage} - {Method} {Path} | StatusCode: {StatusCode} | TraceId: {TraceId} | UserId: {UserId} | RequestInfo: {@RequestInfo}",
                logMessage,
                context.Request.Method,
                context.Request.Path,
                statusCode,
                traceContext.TraceId,
                traceContext.UserId,
                requestInfo);
        }
        catch (Exception ex)
        {
            // 記錄請求參數時發生錯誤，但不影響主要流程
            _logger.LogWarning(ex, 
                "Failed to log request parameters - {Method} {Path} | StatusCode: {StatusCode}", 
                context.Request.Method, 
                context.Request.Path,
                context.Response.StatusCode);
        }
    }

    /// <summary>
    /// 根據狀態碼決定日誌層級
    /// </summary>
    private static LogLevel GetLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => LogLevel.Information,  // 2xx 成功
            >= 400 and < 500 => LogLevel.Warning,      // 4xx 客戶端錯誤
            _ => LogLevel.Error                         // 其他情況
        };
    }

    /// <summary>
    /// 根據狀態碼產生日誌訊息
    /// </summary>
    private static string GetLogMessage(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "Request completed successfully",
            >= 400 and < 500 => "Request failed with client error",
            _ => "Request failed"
        };
    }

    /// <summary>
    /// 取得追蹤內容
    /// </summary>
    private TraceContext GetTraceContext(HttpContext context)
    {
        // 為了簡化，直接創建 TraceContext
        // 在實際部署時可以從 DI 容器取得
        return new TraceContext
        {
            TraceId = context.TraceIdentifier, 
            UserId = context.User?.Identity?.Name ?? "anonymous"
        };
    }
}