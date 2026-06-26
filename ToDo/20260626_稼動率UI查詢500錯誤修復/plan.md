# 稼動率 UI 查詢 500 錯誤修復 — 技術方案書

## 選定方案概述

採「**防呆 + 安全解析 + 例外攔截**」三層修補，集中在 `TasksController.cs` 一支檔，
不改演算法、不改 UI、不動資料表結構。再加一項資料調查釐清新車登錄狀況。

## 架構設計

```
GetMission / GetBarChat / GetTasks (三支 Action)
        │  ← 各自包 try/catch + Warning Log
        ▼
   GetSearchData(startDate, endDate, shuttleId, shiftId)
        │
        ├─ 車輛名稱對照 (:85)
        │     oShuttle.FirstOrDefault(...)?.GustomerName ?? $"車輛{shuttleId}"
        │
        ├─ 撈 ubMission (DB 端 Where，維持現狀)
        │
        └─ 記憶體解析 (:109-119)
              先過濾「BeginTime/EndTime 非 null 且長度 >= 12」
              再用 TryParseExact 解析，失敗的列略過
```

資料流向：DB → EF Core 撈列 → 記憶體安全解析（壞列略過）→ 班別比對 → 回傳報表列。

## 方案分析（六面向）

1. **方案概述**：在既有解析鏈前置「防呆過濾 + TryParse」，Action 外層加例外攔截與 Log。
2. **架構設計**：見上，改動完全收斂在 `TasksController.cs`，不擴散到其他層。
3. **優點**：最小改動面、零資料庫 schema 變更、可立即部署；單筆壞資料不再拖垮整次查詢；日後排查有 Log 可循。
4. **風險與缺點**：壞資料被「略過」而非「修正」，稼動率統計會少算那幾筆異常任務（但異常任務本就不該計入完整稼動率，可接受）；資料調查那段需現場 DB 協助才能完全收尾。
5. **技術債評估**：低。解析邏輯仍在 Controller 內、未抽服務層，未來若要單元測試需再抽純函式；但本次以最小風險修補為優先，技術債可控且有記錄。
6. **建議適用情境**：產線已上線、需快速止血的 bug 修復，最合適。

## 被捨棄的方案

- **方案 B：抽純函式 + 建 SCP.Tests 走 TDD**
  優點是回歸保護完整；但 SCP/WebGui 目前無測試專案、解析邏輯耦合 EF DbContext，建置成本高、交付時程拉長。產線止血優先，故捨棄（已與使用者確認）。
- **方案 C：在 DB 端清洗 ubMission 異常資料**
  治本但風險高（動產線歷史資料），且無法保證未來不再產生未完成任務列。改以程式端容錯為主，DB 清洗不納入本次。
