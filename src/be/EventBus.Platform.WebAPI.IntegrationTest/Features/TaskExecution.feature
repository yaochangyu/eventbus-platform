Feature: Task執行流程
  為了確保任務執行系統能正確處理各種類型的任務
  身為系統測試工程師  
  我希望驗證從任務建立到執行完成的完整流程

  Background:
    Given 測試環境已初始化
    And EventBus WebAPI 正在運行
    And 外部回調服務 Mock 已設定

  Scenario: 立即執行任務成功完成
    Given 我有一個有效的立即執行任務請求，包含回調URL "/callback/success"
    When 我透過 API 建立任務
    Then 任務應該被成功建立並回傳 TaskId
    And 任務最終狀態應該為 "Completed"
    And 整個流程的 TraceId 應該保持一致

  Scenario: 立即執行任務回調失敗後重試
    Given 我有一個有效的立即執行任務請求，包含回調URL "/callback/failure"  
    And 外部回調服務設定為回傳 500 錯誤
    When 我透過 API 建立任務
    Then 任務應該被成功建立並回傳 TaskId
    And 任務最終狀態應該為 "Failed"