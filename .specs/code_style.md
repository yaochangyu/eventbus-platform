# 程式碼風格指南

基於 [api.template](./examples/api.template/) 範例專案分析的開發原則與程式碼風格指南。

## 架構原則

### Clean Architecture 架構模式
- 採用 Clean Architecture 分層架構，明確區分各層職責
- **WebAPI 層**: 控制器、中介軟體、HTTP 相關邏輯
- **Handler 層**: 商業邏輯處理器，封裝業務流程
- **Repository 層**: 資料存取層，實作儲存庫模式
- **Infrastructure 層**: 跨領域基礎設施服務 (快取、工具、追蹤)
- **Contract 層**: 從 OpenAPI 規格自動產生的 API 合約

### 依賴性注入原則
- 完整使用 ASP.NET Core 內建 DI 容器
- 在 Program.cs 中設定 `ValidateScopes = true` 和 `ValidateOnBuild = true`
- 所有服務都透過建構函式注入，避免服務定位器模式

## 程式設計模式

### Result Pattern 錯誤處理
- **強制使用** `Result<TSuccess, TFailure>` 作為 Handler 層回傳類型
- 使用 CSharpFunctionalExtensions 函式庫實作 Result Pattern
- 統一的錯誤處理機制，避免異常拋出

```csharp
public async Task<Result<Member, Failure>> InsertAsync(InsertMemberRequest request, CancellationToken cancel = default)
{
    var queryResult = await repository.QueryEmailAsync(request.Email, cancel);
    if (queryResult.IsFailure)
    {
        return queryResult;
    }
    
    // 業務邏輯處理
    var validateResult = ValidateEmail(queryResult.Value, request);
    if (validateResult.IsFailure)
    {
        return validateResult;
    }
    
    return await repository.InsertAsync(request, cancel);
}
```

### 不可變物件設計 (Immutable Objects)
- 使用 C# `record` 類型定義不可變物件
- 所有屬性使用 `init` 關鍵字，確保物件建立後無法修改
- 避免在應用程式各層間傳遞可變狀態

```csharp
public record TraceContext
{
    public string TraceId { get; init; }
    public string UserId { get; init; }
}
```

### 處理器模式 (Handler Pattern)
- 商業邏輯封裝在處理器類別中，如 `MemberHandler`
- 每個處理器專注於特定領域的業務邏輯
- 透過依賴注入取得所需的儲存庫和服務

## 中介軟體架構

### 中介軟體管線順序
```csharp
app.UseMiddleware<MeasurementMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TraceContextMiddleware>();
app.UseMiddleware<RequestParameterLoggerMiddleware>();
```

### 職責分離原則
- **ExceptionHandlingMiddleware**: 捕捉系統層級例外，轉換為標準化 Failure 回應
- **TraceContextMiddleware**: 處理追蹤內容設定與使用者身分驗證
- **RequestParameterLoggerMiddleware**: 記錄請求參數和回應資訊
- **MeasurementMiddleware**: 效能測量與監控

### TraceContext 管理
- 使用 `AsyncLocal<T>` 機制確保 TraceContext 在整個請求生命週期內可用
- 透過 `IContextGetter<T>` 和 `IContextSetter<T>` 介面進行依賴注入
- 自動將 TraceId 附加到結構化日誌和回應標頭中

## 錯誤處理策略

### FailureCode 列舉定義
```csharp
public enum FailureCode
{
    Unauthorized,        // 未授權存取
    DbError,            // 資料庫錯誤
    DuplicateEmail,     // 重複郵件地址
    DbConcurrency,      // 資料庫併發衝突
    ValidationError,    // 驗證錯誤
    InvalidOperation,   // 無效操作
    Timeout,           // 逾時
    InternalServerError, // 內部伺服器錯誤
    Unknown            // 未知錯誤
}
```

### Failure 物件結構
```csharp
public class Failure
{
    public string Code { get; init; }      // 使用 nameof(FailureCode.*) 定義
    public string Message { get; init; }   // 錯誤訊息
    public string TraceId { get; init; }   // 追蹤識別碼
    public object Data { get; init; }      // 額外資料
}
```

### 分層錯誤處理
- **業務邏輯錯誤**: 在 Handler 層使用 Result Pattern 處理
- **系統層級例外**: 在 ExceptionHandlingMiddleware 統一捕捉和處理
- **安全回應**: 根據環境決定錯誤訊息詳細程度

## 快取策略

### 多層快取架構
- **L1 快取**: 記憶體內快取 (`IMemoryCache`)
- **L2 快取**: Redis 分散式快取
- **快取備援**: Redis 不可用時自動降級至記憶體快取

```csharp
public class MemberService
{
    private readonly ICacheProvider _cache;
    
    public MemberService(ICacheProviderFactory cacheFactory)
    {
        _cache = cacheFactory.Create();
    }
    
    public async Task<Member> GetMemberAsync(int id)
    {
        return await _cache.GetOrSetAsync(cacheKey, 
            () => _repository.GetMemberAsync(id),
            TimeSpan.FromMinutes(30));
    }
}
```

## 程式碼產生工作流程

### OpenAPI-First 開發
1. API 規格維護在 `doc/openapi.yml`
2. 使用 Refitter 產生客戶端程式碼至 `Contract` 專案
3. 使用 NSwag 產生伺服器控制器
4. 使用 EF Core 反向工程產生資料庫實體

### Taskfile 任務管理
```yaml
tasks:
  codegen-api:
    desc: 產生 API 客戶端與伺服器端程式碼
    cmds:
      - task: codegen-api-client
      - task: codegen-api-server
      
  api-dev:
    desc: WebApi Development
    cmds:
      - dotnet watch run --local
```

## 日誌與監控

### 結構化日誌
- 使用 Serilog 結構化日誌格式
- 自動包含 TraceId 與 UserId
- 輸出至控制台、檔案和 Seq 日誌伺服器

### 日誌範圍設定
```csharp
using var _ = logger.BeginScope("{Location},{TraceId},{UserId}",
                                "TW", traceId, userId);
```

## 測試策略

### 測試專案結構
- **JobBank1111.Job.Test**: 單元測試 (xUnit)
- **JobBank1111.Job.IntegrationTest**: 整合測試 (xUnit + Testcontainers + Reqnroll BDD)
- **JobBank1111.Testing.Common**: 共享測試工具

### 測試命令
```bash
# 單元測試
dotnet test src/be/JobBank1111.Job.Test/JobBank1111.Job.Test.csproj

# 整合測試
dotnet test src/be/JobBank1111.Job.IntegrationTest/JobBank1111.Job.IntegrationTest.csproj
```

## 環境設定

### 環境變數管理
- 使用 `--local` 參數從 `env/local.env` 載入環境變數
- `appsettings.json` 中的應用程式設定
- Docker Compose 設定 Redis 與 Seq

### 開發環境初始化
```bash
task dev-init    # 初始化開發環境
task redis-start # 啟動 Redis
task api-dev     # 開發模式執行 API
```

## 程式碼品質

### 命名慣例
- 類別、方法、屬性使用 PascalCase
- 區域變數、參數使用 camelCase
- 常數使用 PascalCase
- 私有欄位使用 camelCase 並加 _ 前綴

### 程式碼組織
- 按功能領域組織檔案結構
- 每個處理器對應一個資料夾 (如 Member/)
- 相關檔案放在同一資料夾下

### 相依性管理
- 明確定義各層之間的相依性方向
- Infrastructure 層不應依賴於 WebAPI 層
- Handler 層不應直接處理 HTTP 相關邏輯

## 安全性考量

### 身分驗證與授權
- 使用 Claims-based 身分驗證
- TraceContext 中包含使用者資訊
- 中介軟體層統一處理身分驗證

### 敏感資訊處理
- 不在日誌中記錄敏感資訊
- 使用環境變數管理機密設定
- 根據環境決定錯誤訊息詳細程度

## 效能最佳化

### 非同步程式設計
- 所有 I/O 操作使用 async/await
- 正確使用 CancellationToken
- 避免 async void，使用 async Task

### 資源管理
- 適當使用 using 語句管理資源
- 避免長時間持有資源鎖定
- 使用物件池重用昂貴物件

這些原則確保程式碼的可維護性、可測試性和效能，並提供一致的開發體驗。