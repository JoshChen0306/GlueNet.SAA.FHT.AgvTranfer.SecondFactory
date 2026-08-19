# 技術方案書 — 二廠跨樓層樓層判斷幽靈資料修復

## 建立日期
2026-08-19

---

## 選定方案：兩段式裁決（同輪去重 + 連續 N 次確認）

保留現有「輪詢每張 MapCode 並寫入 oShuttle」的架構，
在寫 DB 之前插入一層純 C# 裁決器，不動 SQL、不動 schema。

### 為什麼不能只做「偵測到衝突就跳過更新」

這是實作前特別驗算過的關鍵點。事故當時 `oShuttle.MapCode` 的軌跡：

```
11:28:50~11:29:04   只有 DD 回報（車確實在 3F 移動 X=253664→254312 後停住）
                    → DB 現值 = DD (3F)，此時是正確的
11:29:05            BB 出現 X=271005（車真的到 2F 了）
                    DD 仍回報 X=254312（凍結，幽靈）
11:29:24            派發 J1→G1
```

若規則是「偵測到衝突就保留舊值」，保留的舊值正是 **DD(3F)** —— 仍然是錯的，
11:29:24 一樣會誤判成同樓層。
**所以裁決器必須能在多筆中挑出對的那筆，不能只是跳過。**

---

## 架構設計

### 元件關係

```
Dispatch.UpdateAGVStatus()
   │
   ├─ 1. 逐 MapCode 呼叫 GetAGVStatus()，累積到 Dictionary<robotCode, List<AGVStatusData>>
   │      （原本是「查到一筆立刻 Update_oShuttle」，改為先收集）
   │
   ├─ 2. Select_oShuttle() 取得各車 DB 現值 MapCode（供 callback 同步用）
   │
   ├─ 3. 對每個 robotCode 呼叫 ShuttleMapCodeResolver.Resolve()
   │         │
   │         └─ [新增] App_Start/ShuttleMapCodeResolver.cs（純類別，無 DB / HTTP 依賴）
   │              ├─ 第一段：同輪去重，挑勝出筆
   │              └─ 第二段：MapCode 變更需連續 3 輪確認
   │
   ├─ 4. HasConflict → mLog WARN（含各筆 mapCode/座標/勝出原因）
   │
   └─ 5. 以裁決結果呼叫既有的 Update_oShuttle / 稼動率 / CheckBattery
```

### 資料流向

```
RCS ──queryAgvStatus(AA)──┐
RCS ──queryAgvStatus(BB)──┤
RCS ──queryAgvStatus(DD)──┼──→ 本輪回報池 ──→ Resolver ──→ 裁決後單筆 ──→ oShuttle
RCS ──queryAgvStatus(FF)──┘                      ↑
                                                 │
                          oShuttle.MapCode 現值 ─┘（同步 callback 寫入的值）
```

---

## 裁決演算法

### 第一段：同輪去重（挑勝出筆）

```
輸入：本輪某 robotCode 的全部回報 reports、DB 現值 dbMapCode

1. baseline 同步
   若 dbMapCode != 內部 accepted（表示 callback 剛寫過）
     → accepted = dbMapCode，清空 pending 計數
     （callback 的 MapCode 來自 AGV 到站時 RCS 明確回報，可信度高於輪詢，讓它優先）

2. 挑勝出筆 winner
   a. reports.Count == 1                → winner = reports[0]
   b. changed = reports 中「posX/posY 與上一輪同 mapCode 的值不同」者
      （該 mapCode 上一輪未出現 → 視為 changed，新出現是強訊號）
      - changed.Count == 1               → winner = changed[0]
      - 其餘（都變 / 都沒變）             → winner = reports 中 mapCode == accepted 那筆（黏著）
      - 黏著也找不到                     → winner = reports[0]，但 MapCode 不採用（維持 accepted）
   c. reports.Count > 1                  → HasConflict = true
```

**幽靈筆的可靠特徵是「座標凍結」**，而非 IP 為空。8/19 資料：
DD 自 11:28:54 起凍結在 X=254312，11:29:14 鏡像成 X=270072 後再度凍結至 11:29:31；
BB 則持續變動（271005→270749→270506→270244→270072→269814→...）。

### 第二段：連續 3 次確認（debounce）

```
若 winner.mapCode == accepted
  → 清空 pending，輸出 accepted
否則
  → 若 pending.mapCode == winner.mapCode 則 pending.count++
     否則 pending = (winner.mapCode, 1)
  → 若 pending.count >= 3
       accepted = pending.mapCode，清空 pending，MapCodeAccepted = true
  → 輸出 accepted
```

**寫入 DB 的那一列**＝本輪回報中 `mapCode == 輸出的 accepted` 的那一筆；
若不存在（確認窗口中，accepted 對應的地圖本輪沒回報），
則取 winner 但把 `mapCode` 覆寫為 accepted。
如此可避免出現「MapCode 是 DD、PosX 卻是 BB 的座標」的混搭列。

Battery / Status / PosX / PosY / RobotDir **每輪照常更新，不受 debounce 影響**
（電量與狀態延遲會影響 `CrossFloorManager.Tick` 的離線判定與稼動率統計）。

### 套用到 8/19 實際資料的驗算

| 時間 | BB 回報 | DD 回報 | 第一段勝出 | 第二段 | DB MapCode |
|------|---------|---------|-----------|--------|-----------|
| 11:29:04 | — | X=254312 | DD | — | DD |
| 11:29:05 | X=271005（新出現） | X=254312（凍結） | **BB** | 1/3 | DD |
| 11:29:06 | X=271005 | X=254312 | BB（黏著） | 2/3 | DD |
| 11:29:08 | X=271005 | X=254312 | BB（黏著） | 3/3 ✅ | **BB** |
| 11:29:11 | X=270749（動） | X=254312（凍結） | BB | — | BB |
| 11:29:14 | X=270072（動） | X=270072（動後凍結） | BB（黏著） | — | BB |
| 11:29:24 | X=269147 | X=270072（凍結 10 輪） | BB | — | **BB (2F)** ✅ |

→ 11:29:24 派發時讀到 2F，`DecideNextCrossFloorAction` 不會把 J1(3F) 判成同樓層，
改走預調度 2F→3F。**客訴現象消除。**

代價：樓層變更最多晚 3 秒反映。跨樓層決策發生在任務結束後，3 秒無實質影響。

---

## 決策層閘門（第 ② 項）

除了「樓層不可信就不派」，實作前讀 code 另外確認到一個**既有潛在缺陷**：

`CrossFloorManager.cs:333-335`
```csharp
var sameFloorTask = normalTasks.FirstOrDefault(m =>
    pathCalculator.GetFloor(m.BeginStation) == currentFloor);
```

`ElevatorPathCalculator.GetFloor()`（:92-102）對未知首字母回傳 `null`，
`CrossFloorManager.GetFloorByMapCode()`（:380-386）對未知 MapCode 也回傳 `null`。
兩者同時為 null 時 **`null == null` 成立 → 誤判為同樓層並直接派發**。

因此決策層要做兩件事：
1. `currentFloor` 為 null/空 時，`sameFloorTask` 比對必須直接排除（不得靠 `==` 的 null 相等）
2. `SelectNextMission` 取不到可信樓層 → `return null`（本輪不派，下一輪再試）
   —— 但**系統任務分支（`CrossFloorManager.cs:273-276`）維持在最前面**，
   已寫入 oMission 的 `CROSS_FLOOR_DISPATCH` / `IDLE_RETURN` 照常送出，不受影響

---

## 方案分析（六面向）

### 1. 方案概述
在輪詢與 DB 寫入之間插入純 C# 裁決層，用「座標是否變動」挑出真實回報，
並要求 MapCode 變更連續 3 輪確認才生效。

### 2. 架構設計
新增 `ShuttleMapCodeResolver`（純類別，無 SQLData / HikAGV / Log 依賴），
比照既有 `CooldownTracker`、`ElevatorPathCalculator` 的抽出慣例，放同一層 `App_Start/`。
`Dispatch.UpdateAGVStatus` 由「查一筆寫一筆」改為「收集 → 裁決 → 寫入」。

### 3. 優點
- 治本：幽靈筆再也碰不到 `MapCode`，不需猜哪一筆是真的
- 純類別可完整單元測試，能用 8/19 實際資料回放驗證（AC7）
- 不改 SQL、不改 schema、不動 SCP / svrPair，影響面收斂在單一檔案 + 單一函式
- 保留現有輪詢架構，callback 路徑不必改，符合使用者「保留原本方式」的要求

### 4. 風險與缺點
- 樓層變更最多延遲 3 秒（已評估對跨樓層決策無實質影響）
- `UpdateAGVStatus` 需多一次 `Select_oShuttle()`（每秒一次，與 `CrossFloorManager.Tick` 同量級，可接受）
- 「座標凍結 = 幽靈」是從 8/19 單日資料歸納，若 RCS 未來出現「幽靈筆座標也會變動」的形態，
  第一段會退化為黏著；但第二段的 3 次確認仍能擋住單輪誤判，且會留 WARN log 可追

### 5. 技術債評估
- 裁決邏輯集中在一個純類別，未來要換判準（例如改用 RCS 新增的欄位）只需改該類別
- 常數 3 若日後要調，改一個 `const` 即可；使用者已決定本次不放設定檔，避免現場誤調
- 不引入新的設定項、不新增 DB 欄位，維護面無額外負擔

### 6. 建議適用情境
外部系統回報可能重複或殘留、而本地又必須維持單一權威值的場景。
本專案 `oShuttle` 正是此形態（一台車一列，多張地圖來源）。

---

## 被捨棄的方案

### 方案 B：以 `robotIp` 為空淘汰幽靈筆
**捨棄原因（兩個致命問題）**
1. 本地 mock RCS `NormalAPI.cs:411,428` 硬寫 `robotIp = ""`，採用後開發機所有車輛狀態會被丟棄，系統癱瘓
2. 8/19 資料中 11:29:05~11:29:13 兩筆皆帶 IP，IP 判準在該區間本就無效；
   「幽靈筆 IP 為空」只是單日觀察到的相關性，海康無任何文件保證此因果

### 方案 C：`UpdateAGVStatus` 不再寫 MapCode，改由 callback 單一維護
**捨棄原因**：使用者 2026-08-19 決定保留原本輪詢寫入方式。
（此方案本身最乾淨——幽靈筆連碰都碰不到 MapCode——但改變了資料來源的權威歸屬，
　且 callback 漏收時 MapCode 會停在舊值，需搭配看門狗才完整，而看門狗本次不做。）

### 方案 D：只做「偵測到衝突就不更新」
**捨棄原因**：驗算證明擋不住本次事故。保留的舊值正是錯誤的 DD(3F)，詳見上方「為什麼不能只做⋯⋯」段落。
