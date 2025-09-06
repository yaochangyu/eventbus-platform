# EventBus Platform 集中管理平台 - ASP.NET Core 9 MVP Task List

## MVP Implementation Tasks

- [x] 1. **基礎設施層建置 (MVP)**
    - [x] 1.1. 建立 MVP 專案結構
        - *Goal*: 建立符合 Clean Architecture 的 MVP 專案結構和基礎配置
        - *Details*: 建立 EventBus.Infrastructure、EventBus.Platform.WebAPI 專案，配置 DI 容器、logging、InMemory Database
        - *Requirements*: 相容性需求 - 支援 .NET 9.0+，可維護性需求 - 結構化日誌記錄
    - [x] 1.2. TraceContext 中介軟體 (簡化版)
        - *Goal*: 實作基本追蹤機制
        - *Details*: 建立簡化的 TraceContextMiddleware，提供 IContextGetter<TraceContext?> 服務，暫不實作 JWT 驗證
        - *Requirements*: 監控與追蹤 - 任務狀態查詢功能
    - [x] 1.3. Memory Cache 服務實作
        - *Goal*: 實作 Memory Cache 快取策略
        - *Details*: 建立 ICacheProvider 介面，實作 MemoryCacheProvider，支援基本快取操作
        - *Requirements*: 效能需求 - API 回應時間優化
    - [x] 1.4. .NET Queue 抽象層
        - *Goal*: 建立 .NET 內建 Queue 的抽象化介面和實作
        - *Details*: 建立 IQueueService 介面，使用 Channel<T> 或 ConcurrentQueue<T> 實作，支援基本隊列操作
        - *Requirements*: 基本功能 - 支援消息隊列處理，MVP 快速實現

- [x] 2. **資料庫層設計與實作 (InMemory)**
    - [x] 2.1. Entity Framework Core InMemory 模型設計
        - *Goal*: 設計 InMemory 資料庫和 EF Core 實體模型
        - *Details*: 建立 Event、Task、SchedulerTask、Subscription、ExecutionMetrics 等實體，使用 InMemory Provider
        - *Requirements*: 監控與追蹤 - 記錄任務啟動時間和結束時間，收集和儲存錯誤訊息
    - [x] 2.2. Repository 模式實作 (簡化版)
        - *Goal*: 實作基本 Repository 提供資料存取服務
        - *Details*: 建立 EventRepository、TaskRepository、SchedulerTaskRepository、SubscriptionRepository，整合 Memory Cache
        - *Requirements*: 基本功能實現，MVP 快速開發

- [x] 3. **核心業務邏輯實作 - Handler 層 (MVP)**
    - [x] 3.1. EventHandler 實作
        - *Goal*: 實作基本事件發布和訂閱管理邏輯
        - *Details*: 實作 PublishEventAsync、訂閱者查詢、事件路由邏輯，使用 Result Pattern 錯誤處理
        - *Requirements*: 基本功能 - 實現 Event 消息類型，API 規格 - Pub API 支援 Event 發布
    - [x] 3.2. TaskHandler 實作
        - *Goal*: 實作基本任務建立和管理邏輯
        - *Details*: 實作 CreateTaskAsync、任務狀態管理、基本重試機制
        - *Requirements*: 基本功能 - 實現 Task 消息類型，API 規格 - Pub API 支援 Task 建立
    - [x] 3.3. SchedulerHandler 實作 (簡化版)
        - *Goal*: 實作基本延遲任務調度邏輯
        - *Details*: 實作 CreateSchedulerTaskAsync、使用 Timer 實現延遲執行，暫不支援 Cron 表達式
        - *Requirements*: 基本功能 - 實現 Scheduler 延遲執行

- [ ] 4. **Web API 層實作 - Controller 層 (MVP)**
    - [x] 4.1. PubController 實作
        - *Goal*: 實作消息發布的 REST API
        - *Details*: 實作 PublishEventAsync、CreateTaskAsync、CreateSchedulerTaskAsync 端點，使用 Primary Constructor 注入
        - *Requirements*: API 規格 - Pub API 支援 Event 發布和 Task 建立
    - [x] 4.2. RegisterController 實作
        - *Goal*: 實作事件訂閱管理的 REST API
        - *Details*: 實作訂閱者註冊、取消訂閱、查詢訂閱狀態的端點
        - *Requirements*: 基本功能 - 提供 Register API 支援事件訂閱管理
    - [x] 4.3. CallbackController 實作
        - *Goal*: 實作任務執行結果回報的 REST API
        - *Details*: 實作執行結果接收、狀態更新、錯誤訊息記錄的端點
        - *Requirements*: 基本功能 - 提供 Callback API 支援狀態回報

- [ ] 5. **消息處理引擎實作 (MVP)**
    - [x] 5.1. 隊列消費者實作
        - *Goal*: 實作 .NET Queue 消息消費和處理邏輯
        - *Details*: 建立 EventConsumer、TaskConsumer、SchedulerConsumer，使用 Channel<T> 或 ConcurrentQueue<T>
        - *Requirements*: 基本功能 - 支援消息隊列處理
    - [x] 5.2. HTTP 回調處理器 (簡化版)
        - *Goal*: 實作基本外部 API 回調的 HTTP 客戶端
        - *Details*: 建立 HttpCallbackService，支援基本 HTTP 方法、標頭處理、超時控制
        - *Requirements*: 配置管理 - Callback 配置
    - [x] 5.3. Timer 調度引擎
        - *Goal*: 實作基於 Timer 的延遲任務調度機制
        - *Details*: 建立 TimerSchedulerService，使用 System.Threading.Timer 實現延遲執行
        - *Requirements*: 基本功能 - 實現 Scheduler 延遲執行

- [ ] 6. **基本監控實作 (MVP)**
    - [x] 6.1. 執行指標收集器 (簡化版)
        - *Goal*: 實作基本任務執行時間和狀態指標收集
        - *Details*: 建立 ExecutionMetricsCollector，記錄開始時間、結束時間、執行狀態、錯誤訊息
        - *Requirements*: 監控與追蹤 - 記錄任務啟動時間和結束時間
    - [x] 6.2. 健康檢查端點
        - *Goal*: 實作基本健康檢查功能
        - *Details*: 實作 /health、/health/ready、/health/live 端點
        - *Requirements*: 可維護性需求 - 提供健康檢查端點

- [ ] 7. **基本錯誤處理 (MVP)**
    - [x] 7.1. 全域錯誤處理中介軟體 (簡化版)
        - *Goal*: 實作統一的錯誤處理和回應機制
        - *Details*: 建立 ExceptionHandlingMiddleware，使用 Result Pattern，統一錯誤回應格式
        - *Requirements*: API 規格 - 所有 API 提供適當的錯誤處理和狀態碼

- [ ] 8. **基本測試實作 (MVP)**
    - [x] 8.1. 核心功能單元測試
        - *Goal*: 為核心 Handler 建立基本單元測試
        - *Details*: 使用 xUnit + FluentAssertions，測試主要業務邏輯流程
        - *Requirements*: 基本功能驗證

## MVP Task Dependencies

- Task 1 (基礎設施層 MVP) 必須最先完成，為後續所有任務提供基礎
- Task 2 (InMemory 資料庫層) 依賴 Task 1.1 的專案結構建立
- Task 3 (Handler 層 MVP) 依賴 Task 1 (基礎設施) 和 Task 2 (資料庫層) 完成
- Task 4 (Controller 層 MVP) 依賴 Task 3 (Handler 層) 完成
- Task 5 (消息處理引擎 MVP) 可與 Task 3-4 並行開發，但依賴 Task 1 (基礎設施層)
- Task 6 (基本監控) 依賴 Task 2-5 完成，需要實際的執行指標
- Task 7 (基本錯誤處理) 建議優先完成，可與其他任務並行開發
- Task 8 (基本測試) 在核心功能模組完成後進行

MVP 並行執行建議：
- Task 1.2-1.4 可並行執行（依賴 1.1）
- Task 2.1-2.2 可並行執行（依賴 Task 1.1）
- Task 3.1-3.3 可並行執行（依賴 Task 1-2）
- Task 4.1-4.3 可並行執行（依賴 Task 3）
- Task 5.1-5.3 可並行執行（依賴 Task 1）
- Task 6.1-6.2 可並行執行（依賴 Task 2-5）

## MVP Estimated Timeline

- Task 1: 8 hours (基礎設施層建置 MVP)
- Task 2: 6 hours (InMemory 資料庫層設計與實作)
- Task 3: 10 hours (核心業務邏輯實作 MVP)
- Task 4: 8 hours (Web API 層實作 MVP)
- Task 5: 10 hours (消息處理引擎實作 MVP)
- Task 6: 6 hours (基本監控實作)
- Task 7: 4 hours (基本錯誤處理)
- Task 8: 6 hours (基本測試實作)
- **Total: 58 hours** (約 7-8 個工作天)

考慮並行執行的優化，實際 MVP 開發時間可壓縮至約 **4-5 個工作天**。

## MVP 功能範圍

### 包含的核心功能
- ✅ Event 發布與訂閱 (InMemory 儲存)
- ✅ Task 立即執行 (.NET Queue 處理)
- ✅ Scheduler 延遲執行 (Timer 實現)
- ✅ HTTP 回調處理
- ✅ 基本監控與健康檢查
- ✅ Result Pattern 錯誤處理
- ✅ 單元測試覆蓋

### 暫不包含的功能 (後續迭代)
- ❌ JWT 身份驗證 (暫用基本追蹤)
- ❌ Redis 分散式快取 (使用 Memory Cache)
- ❌ PostgreSQL 資料庫 (使用 InMemory)
- ❌ RabbitMQ 消息佇列 (使用 .NET Queue)
- ❌ 複雜的 SLO 監控 (基本指標收集)
- ❌ 進階安全性功能
- ❌ 容器化部署設定
- ❌ 效能與負載測試

## MVP 升級路徑

完成 MVP 後，可按以下順序逐步升級：

1. **Phase 2 - 持久化**: InMemory → PostgreSQL
2. **Phase 3 - 分散式**: Memory Cache → Redis
3. **Phase 4 - 可靠性**: .NET Queue → RabbitMQ
4. **Phase 5 - 安全性**: 基本追蹤 → JWT 認證
5. **Phase 6 - 營運**: 基本監控 → 完整 SLO 監控
6. **Phase 7 - 部署**: 本地開發 → 容器化部署
