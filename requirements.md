# MQ Platform 集中管理平台 - Requirements Document

基於 RabbitMQ 的消息隊列平台，支援 event、task、scheduler 三種類型的消息處理，提供完整的任務配置管理、SLO 監控和 API 回調機制。

## Core Features

### 1. 消息類型支援
- **Event（事件）**：通知訂閱者的廣播消息
- **Task（任務）**：回呼指定 API 的單次執行任務
- **Scheduler（調度任務）**：指定時間延遲執行的回呼 API 任務

### 2. 任務配置管理
- Queue 管理：建立、配置和監控消息隊列
- Callback 配置：設定 API 回呼端點和參數
- SLO 配置：設定執行時間限制和超時處理策略

### 3. API 服務
- **Pub API**：發布消息和任務的公開接口
  - 建立立即執行或延遲執行的 Task
  - 發布 Event 並觸發相關訂閱者的 Task
- **Register API**：註冊和管理事件訂閱
- **Callback API**：處理任務執行結果和狀態回報

### 4. 監控與狀態管理
- 任務執行時間追蹤（啟動時間、結束時間）
- 錯誤訊息收集和記錄
- 超時檢測和狀態更新

## User Stories

### 系統管理員
- As a 系統管理員, I want to 配置 Queue 設定, so that 可以管理不同類型的消息隊列
- As a 系統管理員, I want to 設定 SLO 參數, so that 可以監控任務執行效能並處理超時情況
- As a 系統管理員, I want to 查看任務執行統計, so that 可以分析系統效能和健康狀態

### 應用程式開發者
- As a 應用程式開發者, I want to 透過 Pub API 發布 Event, so that 可以通知所有訂閱者
- As a 應用程式開發者, I want to 建立 Task 任務, so that 可以執行特定的 API 回呼
- As a 應用程式開發者, I want to 建立 Scheduler 任務, so that 可以延遲執行指定的 API 回呼
- As a 應用程式開發者, I want to 使用 Register API 訂閱事件, so that 可以接收相關通知

### 服務提供者
- As a 服務提供者, I want to 透過 Callback API 回報執行狀態, so that 系統可以追蹤任務執行結果
- As a 服務提供者, I want to 回報執行時間和錯誤訊息, so that 系統可以進行監控和除錯

## Acceptance Criteria

### 基本功能
- [ ] 支援 RabbitMQ 作為底層消息佇列系統
- [ ] 實現 Event、Task、Scheduler 三種消息類型
- [ ] 提供 Pub API 支援立即執行和延遲執行的任務建立
- [ ] 提供 Register API 支援事件訂閱管理
- [ ] 提供 Callback API 支援狀態回報

### 配置管理
- [ ] Queue 管理功能：建立、修改、刪除隊列
- [ ] Callback 配置：設定 API 端點、請求參數、認證方式
- [ ] SLO 配置：設定執行時間限制、超時處理策略

### 監控與追蹤
- [ ] 記錄任務啟動時間和結束時間
- [ ] 收集和儲存錯誤訊息
- [ ] 實現超時檢測機制
- [ ] 提供任務狀態查詢功能

### API 規格
- [ ] Pub API 支援 Event 發布和 Task 建立
- [ ] Register API 支援訂閱者註冊和取消訂閱
- [ ] Callback API 支援執行結果回報
- [ ] 所有 API 提供適當的錯誤處理和狀態碼

## Non-functional Requirements

### 效能需求
- 支援每秒至少 1000 個消息的處理能力
- API 回應時間不超過 200ms（95%ile）
- 系統可用性達到 99.9%

### 安全需求
- API 存取需要適當的身份驗證機制
- 敏感資料（如 API 金鑰）需要加密儲存
- 提供存取日誌記錄功能

### 相容性需求
- 支援 RabbitMQ 3.8+ 版本
- 支援 .NET 6.0+ 執行環境
- 提供 RESTful API 介面
- 支援 JSON 格式的資料交換

### 可維護性需求
- 提供詳細的 API 文件
- 實現結構化日誌記錄
- 支援配置檔案管理
- 提供健康檢查端點

### 擴展性需求
- 支援水平擴展部署
- 支援多個 RabbitMQ 節點的叢集配置
- 提供插件化的回呼處理機制
