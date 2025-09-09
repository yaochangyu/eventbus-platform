using System.Text.Json;
using System.Text;

namespace EventBus.Platform.WebAPI.Infrastructure;

/// <summary>
/// 統一的請求資訊擷取工具
/// </summary>
public static class RequestInfoExtractor
{
    /// <summary>
    /// 敏感標頭過濾清單
    /// </summary>
    private static readonly string[] SensitiveHeaders = 
    {
        "Authorization", "Cookie", "X-API-Key", "X-Auth-Token", 
        "Set-Cookie", "Proxy-Authorization"
    };

    /// <summary>
    /// 擷取完整請求資訊
    /// </summary>
    /// <param name="context">HTTP 內容</param>
    /// <param name="jsonOptions">JSON 序列化選項</param>
    /// <returns>請求資訊物件</returns>
    public static async Task<object> ExtractRequestInfoAsync(HttpContext context, JsonSerializerOptions jsonOptions)
    {
        try
        {
            var request = context.Request;
            
            // 1. 基本資訊
            var basicInfo = new
            {
                Method = request.Method,
                Path = request.Path.ToString(),
                QueryString = request.QueryString.ToString(),
                ContentType = request.ContentType,
                ContentLength = request.ContentLength,
                Scheme = request.Scheme,
                Host = request.Host.ToString(),
                RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = request.Headers["User-Agent"].ToString()
            };

            // 2. 路由參數
            var routeValues = context.Request.RouteValues
                .Where(rv => rv.Value != null)
                .ToDictionary(rv => rv.Key, rv => rv.Value?.ToString());

            // 3. 查詢參數
            var queryParameters = request.Query
                .ToDictionary(q => q.Key, q => q.Value.ToString());

            // 4. 請求標頭 (排除敏感標頭)
            var headers = request.Headers
                .Where(h => !SensitiveHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            // 5. 請求本文 (僅針對 POST/PUT/PATCH 且有內容的請求)
            object? requestBody = null;
            if (ShouldLogRequestBody(request))
            {
                requestBody = await ExtractRequestBodyAsync(request, jsonOptions);
            }

            return new
            {
                BasicInfo = basicInfo,
                RouteValues = routeValues,
                QueryParameters = queryParameters,
                Headers = headers,
                RequestBody = requestBody
            };
        }
        catch (Exception ex)
        {
            // 擷取請求資訊時發生錯誤，回傳基本資訊
            return new
            {
                Error = "Failed to extract request info",
                Exception = ex.Message,
                BasicInfo = new
                {
                    Method = context.Request.Method,
                    Path = context.Request.Path.ToString(),
                    QueryString = context.Request.QueryString.ToString()
                }
            };
        }
    }

    /// <summary>
    /// 判斷是否應該記錄請求本文
    /// </summary>
    private static bool ShouldLogRequestBody(HttpRequest request)
    {
        // 只記錄 POST/PUT/PATCH 請求的本文
        var methodsToLog = new[] { "POST", "PUT", "PATCH" };
        if (!methodsToLog.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // 檢查內容長度
        if (request.ContentLength == null || request.ContentLength == 0)
        {
            return false;
        }

        // 檢查內容類型
        var contentType = request.ContentType?.ToLowerInvariant();
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        // 只記錄 JSON 和表單資料
        return contentType.Contains("application/json") || 
               contentType.Contains("application/x-www-form-urlencoded") ||
               contentType.Contains("multipart/form-data");
    }

    /// <summary>
    /// 擷取並解析請求本文
    /// </summary>
    private static async Task<object?> ExtractRequestBodyAsync(HttpRequest request, JsonSerializerOptions jsonOptions)
    {
        try
        {
            // 確保請求本文可以被讀取
            request.EnableBuffering();
            request.Body.Position = 0;

            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var bodyContent = await reader.ReadToEndAsync();
            
            // 重設 Stream 位置供後續使用
            request.Body.Position = 0;

            if (string.IsNullOrEmpty(bodyContent))
            {
                return null;
            }

            // 限制記錄的本文長度 (避免記錄過大的內容)
            const int maxBodyLength = 5000;
            if (bodyContent.Length > maxBodyLength)
            {
                bodyContent = bodyContent.Substring(0, maxBodyLength) + "... [truncated]";
                return new { Content = bodyContent, Truncated = true };
            }

            // 嘗試解析 JSON
            var contentType = request.ContentType?.ToLowerInvariant();
            if (contentType?.Contains("application/json") == true)
            {
                try
                {
                    var jsonDocument = JsonDocument.Parse(bodyContent);
                    return new { Content = jsonDocument.RootElement, ContentType = "JSON" };
                }
                catch
                {
                    // 如果 JSON 解析失敗，回傳原始文字
                    return new { Content = bodyContent, ContentType = "Text" };
                }
            }

            // 其他類型回傳原始內容
            return new { Content = bodyContent, ContentType = contentType ?? "Unknown" };
        }
        catch (Exception ex)
        {
            return new 
            { 
                Error = "Failed to read request body", 
                Exception = ex.Message 
            };
        }
    }
}