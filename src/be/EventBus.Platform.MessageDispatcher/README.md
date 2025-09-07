# EventBus Platform Message Dispatcher

基於設計文檔 "1. Task 處理機制" 章節實作的 .NET Core 9 Console App，專門負責 Dispatcher 角色功能。

## 功能概述

根據設計文檔中的流程圖，MessageDispatcher 負責：

1. **從 Queue 取出任務請求** - 監聽記憶體佇列中的任務請求
2. **存入資料庫** - 將任務請求持久化到 InMemory 資料庫
3. **狀態追蹤** - 維護任務狀態和追蹤資訊

## 架構設計

### 核心元件

- **MessageDispatcherService**: 主要的背景服務，實作設計文檔中的 Dispatcher 邏輯
- **InMemoryQueueService**: 記憶體佇列服務，模擬 .NET Queue Service
- **InMemoryTaskRepository**: 記憶體資料庫服務，模擬 InMemory Database
- **DemoTaskGenerator**: 演示用任務產生器，模擬來自 API 的任務請求
- **TaskStatusMonitor**: 任務狀態監控服務，提供即時狀態報告

### 資料模型

- **TaskRequest**: 任務請求模型，對應設計文檔中的任務屬性
- **TaskEntity**: 任務實體模型，用於資料庫儲存
- **MessageStatus**: 任務狀態列舉，對應設計文檔中的狀態定義

## 執行方式

```bash
# 建置專案
dotnet build

# 執行應用程式
dotnet run

# 測試運行 30 秒
timeout 30 dotnet run
```

## 設計原則遵循

1. **Clean Architecture**: 分層設計，Repository Pattern
2. **Result Pattern**: (備用，目前 MVP 使用例外處理)
3. **依賴注入**: 完整的 DI 容器配置
4. **非同步程式設計**: 全面使用 async/await
5. **結構化日誌**: 包含 TraceId 的追蹤資訊
6. **背景服務模式**: 使用 BackgroundService 基底類別

## 日誌輸出示例

```
info: EventBus.Platform.MessageDispatcher.Services.InMemoryQueueService[0]
      Task enqueued: 6236bb3c-a75d-48f4-a80b-173e31d3f558 for callback https://httpbin.org/post?task=1 - TraceId: 4d4d63da

info: EventBus.Platform.MessageDispatcher.Repositories.InMemoryTaskRepository[0]
      Task created in repository: 6236bb3c-a75d-48f4-a80b-173e31d3f558 with status Pending - TraceId: 4d4d63da

info: EventBus.Platform.MessageDispatcher.Services.MessageDispatcherService[0]
      Task moved from queue to repository: 6236bb3c-a75d-48f4-a80b-173e31d3f558 - TraceId: 4d4d63da
```

## 下一步擴展

- 整合 RabbitMQ 替代記憶體佇列
- 整合 Entity Framework Core 替代記憶體資料庫
- 實作 TaskWorkerService 處理任務執行
- 加入健康檢查端點
- 實作設定檔案支援