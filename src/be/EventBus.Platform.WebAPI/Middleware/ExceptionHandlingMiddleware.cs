using System.Net;
using System.Text.Json;
using EventBus.Infrastructure.TraceContext;
using EventBus.Infrastructure.Models;
using EventBus.Platform.WebAPI.Infrastructure;

namespace EventBus.Platform.WebAPI.Middleware;

/// <summary>
/// 系統層級例外處理中介軟體
/// 捕捉並處理所有未處理的系統例外，轉換為標準化的錯誤回應
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IWebHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
        
        // 設定 JSON 序列化選項
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceContext = GetTraceContext(context);
        
        // 擷取請求資訊用於日誌記錄
        var requestInfo = await RequestInfoExtractor.ExtractRequestInfoAsync(context, _jsonOptions);
        
        // 記錄未處理的例外，包含所有請求資訊
        _logger.LogError(exception,
            "Unhandled exception occurred - {Method} {Path} | TraceId: {TraceId} | UserId: {UserId} | ExceptionType: {ExceptionType} | RequestInfo: {@RequestInfo}",
            context.Request.Method,
            context.Request.Path,
            traceContext.TraceId,
            traceContext.UserId,
            exception.GetType().Name,
            requestInfo);

        // 如果回應已經開始，無法修改狀態碼和標頭
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started, the exception middleware will not be executed.");
            return;
        }

        // 設定回應內容類型
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // 建立標準化的錯誤回應
        var failure = CreateFailure(exception, traceContext);
        
        var jsonResponse = JsonSerializer.Serialize(failure, _jsonOptions);
        await context.Response.WriteAsync(jsonResponse);
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

    /// <summary>
    /// 建立標準化的錯誤回應物件
    /// </summary>
    private Failure CreateFailure(Exception exception, TraceContext traceContext)
    {
        // 根據環境決定要回傳的錯誤訊息詳細程度
        var message = _env.IsDevelopment() 
            ? exception.Message 
            : "內部伺服器錯誤";

        var data = _env.IsDevelopment() 
            ? (object)new
            {
                ExceptionType = exception.GetType().Name,
                StackTrace = exception.StackTrace,
                Timestamp = DateTimeOffset.UtcNow
            }
            : new
            {
                Timestamp = DateTimeOffset.UtcNow
            };

        return new Failure
        {
            Code = nameof(FailureCode.InternalServerError),
            Message = message,
            TraceId = traceContext.TraceId,
            Exception = exception, // 這個不會序列化到 JSON 回應中
            Data = data
        };
    }
}