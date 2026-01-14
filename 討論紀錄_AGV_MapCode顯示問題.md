# AGV MapCode 顯示問題 - 完整討論紀錄

**日期**: 2026-01-14  
**問題類型**: Bug Fix  
**影響範圍**: Web 地圖顯示功能  

---

## 一、問題發現過程

### 初始問題描述

用戶提供了 AGV 系統的 LOG 記錄，發現以下現象：

```log
2026/01/08 16:00:11.000 [       ] 任務異常!
2026/01/08 16:00:11.090 [       ] Update AGV Data! [Robot Code] : 20106; [Robot Dir] : -24; [Robot IP] : 192.168.7.33; [Battery] : 50 ; [Pos X] : 193588; [Pos Y] : 208168; [Map Code] : AA; [Speed] : 732; [Status] : A ; [Excl Type] : 0; [Stop] : 0; [Pod Code] : ; [Pod Dir] : 0; [Path] : 
```

**用戶問題**：依照 LOG 來看，現在的 MapCode 是不是不符合更新條件？

### 初步分析

1. **MapCode 的作用範圍**
   - 系統會依照設定檔中的 MapCode 列表，逐一查詢每個 MapCode 的 AGV 狀態
   - 從 `Dispatch.cs` 第 157-163 行可見
   
2. **任務異常的判斷**
   - 當 AGV 的 status = "3" 時，系統會設定 `ShuttleStatus = "A"`
   - 記錄異常開始時間並輸出 "任務異常!" 的 LOG

3. **LOG 顯示內容**
   - AGV **20106** 的 **MapCode = "AA"**，status = **"3"** → 被判定為**任務異常**
   - AGV **1** 的 **MapCode = "FF"**，status = **"0"** → 被判定為**預設警報狀態**

---

## 二、深入問題探討

### 用戶的兩個關鍵問題

#### Q1: 如果收到 AGV 20106 的 MapCode = "AA" 的時候，資料庫的 MapCode 就會被更新成 AA 嗎？

**答案：❌ 不會！**

從 `SQLData.cs` 第 76-86 行的 `Update_oShuttle` 方法可以看到，更新的欄位只包括：
- `Battery`（電量）
- `Status`（狀態）
- `PosX`（X 座標）
- `PosY`（Y 座標）
- `RobotDir`（方向）

**MapCode 並未包含在更新的欄位中**，所以即使 AGV 回報 MapCode = "AA"，資料庫中的 MapCode 也不會被改變。

#### Q2: 如果更新成 AA，Web 畫面上是不是就無法顯示了？

**答案：✅ 是的！**

從 `CommonController.cs` 第 221-231 行可以看到：

```csharp
private List<oShuttle> GetAgv(string area)
{
    // 只會查詢 MapCode == area 的 AGV
    List<oShuttle> AgvPositions = _DBContext.oShuttle.Where(x => x.MapCode == area).ToList();
    ...
}
```

**顯示邏輯問題**：
- Web 畫面會根據使用者選擇的樓層（area）來過濾 AGV
- 目前 `appsettings.json` 中只配置了 `FHT1-1F` 和 `FHT1-3F`
- 如果 AGV 的 MapCode 是 "AA"，系統會嘗試查詢 `MapCode == "AA"` 的 AGV
- 但因為 "AA" 並沒有對應的座標轉換設定，所以會出錯

---

## 三、關鍵發現：MapCode 對應關係

### 用戶提供的關鍵資訊

```
AA = 1F
BB = 2F
DD = 3F
FF = 4F
```

**問題根源**：
- 當收到 MapCode = AA，代表這台車正在一樓
- 人在 WEB 畫面切換到 1F 的時候，**應該要出現車**
- 但實際上**車輛顯示有問題**！

### 根本原因分析

**MapCode 不匹配問題**：

| 系統 | MapCode 格式 | 範例 |
|------|-------------|------|
| 海康 RCS | AA, BB, DD, FF | AA (1F) |
| Web 前端 | FHT1-1F, FHT1-2F, FHT1-3F, FHT1-4F | FHT1-1F |
| 資料庫 | AA, BB, DD, FF (來自海康) | AA |

**查詢邏輯錯誤**：
1. 用戶在 Web 上選擇 **"FHT1-1F"** (1樓)
2. 程式查詢 `WHERE MapCode == "FHT1-1F"`
3. 但資料庫中 AGV 的 MapCode 是 **"AA"**
4. **結果**：完全找不到車輛！❌

---

## 四、解決方案設計

### 核心思路

在 Web 前端區域代碼（FHT1-1F）與海康 MapCode（AA）之間建立**轉換層**。

### 設計原則

1. **雙向兼容**：保持現有座標轉換邏輯不變
2. **配置驅動**：使用 `appsettings.json` 維護對應關係
3. **向後兼容**：找不到映射時返回原值，不破壞舊邏輯
4. **職責清晰**：MapCode 用於查詢，area 用於座標轉換

### 實作方案

#### 方案 1：配置檔新增對應關係

在 `appsettings.json` 新增 `MapCodeMapping` 區段：

```json
"MapCodeMapping": {
  "FHT1-1F": "AA",
  "FHT1-2F": "BB",
  "FHT1-3F": "DD",
  "FHT1-4F": "FF"
}
```

#### 方案 2：Controller 新增轉換方法

在 `CommonController.cs` 新增：

```csharp
/// <summary>
/// 將 Web 區域代碼轉換為海康 MapCode
/// </summary>
private string GetMapCodeFromArea(string area)
{
    var mapping = _configuration.GetSection("MapCodeMapping").Get<Dictionary<string, string>>();
    if (mapping != null && mapping.ContainsKey(area))
    {
        return mapping[area];
    }
    // 如果找不到映射，返回原始 area（向後兼容）
    return area;
}
```

#### 方案 3：更新查詢邏輯

修改 `GetAgv(string area)` 方法：

```csharp
private List<oShuttle> GetAgv(string area)
{
    // 將 Web 區域代碼轉換為海康 MapCode
    string mapCode = GetMapCodeFromArea(area);
    
    List<oShuttle> AgvPositions = _DBContext.oShuttle.Where(x => x.MapCode == mapCode).ToList();
    foreach (var item in AgvPositions)
    {
        // 使用原始 area 進行座標轉換（因為 appsettings 使用 FHT1-1F 作為 key）
        item.PosX = ConvertX(item.PosX, area);
        item.PosY = ConvertY(item.PosY, area);
    }
    return AgvPositions;
}
```

---

## 五、實作結果

### 修改檔案清單

#### 1. [appsettings.json](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/WebGui/SCP/appsettings.json)

**新增內容**：

```json
"MapCodeMapping": {
  "FHT1-1F": "AA",
  "FHT1-2F": "BB",
  "FHT1-3F": "DD",
  "FHT1-4F": "FF"
}
```

#### 2. [CommonController.cs](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/WebGui/SCP/Controllers/CommonController.cs)

**新增方法**：
- `GetMapCodeFromArea(string area)` - MapCode 轉換方法

**修改方法**：
- `GetAgv(string area)` - 使用 MapCode 轉換進行查詢

### 修改前後對比

#### 修改前

```
用戶選 1F → area = "FHT1-1F" 
→ 查詢 MapCode = "FHT1-1F" 
→ 資料庫中是 "AA" 
→ ❌ 找不到 AGV
```

#### 修改後

```
用戶選 1F → area = "FHT1-1F" 
→ 轉換 GetMapCodeFromArea("FHT1-1F") = "AA"
→ 查詢 MapCode = "AA" 
→ ✅ 找到 AGV
→ 使用 area = "FHT1-1F" 進行座標轉換
→ ✅ 正確顯示在地圖上
```

---

## 五之二、🔴 關鍵後續發現：MapCode 必須更新到資料庫

### 問題再發現

實作完前面的 MapCodeMapping 後，用戶提出了一個**非常關鍵的問題**：

> 所以目前收到 AGV 狀態後會更新資料庫 MapCode 嗎？

**初步回答**：❌ 不會

但用戶立即指出：

> 但是不會更新，畫面顯示效果不就失效了？

### MapCode 的真實本質

這個問題揭示了對 MapCode 的**理解錯誤**：

#### ❌ 錯誤理解（初期）
- MapCode 是靜態的，在資料庫初始化時設定
- AGV 的 MapCode 固定不變

#### ✅ 正確理解
- **MapCode 是動態的**，代表 AGV **當前所在的樓層**
- AGV 從 1F 移動到 3F 時，海康會回報 MapCode 從 "AA" 變成 "DD"
- **MapCode 必須即時更新到資料庫，才能正確顯示 AGV 位置**

### 問題場景

```
AGV 20106 從 1F 移動到 3F
  ↓
海康 RCS 回報: MapCode = "DD" (原本是 "AA")
  ↓
Update_oShuttle() 執行（未更新 MapCode）
  ↓
❌ 資料庫 MapCode 還是 "AA"（舊值）
  ↓
用戶在 Web 切換到 3F
  ↓
GetMapCodeFromArea("FHT1-3F") → "DD"
查詢 WHERE MapCode = 'DD'
  ↓
❌ 找不到 AGV（因為資料庫中還是 "AA"）
  ↓
❌ 畫面顯示失效！
```

### 解決方案：修改 SQLData.cs

#### 3. [SQLData.cs](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/ACC/HikAGVWebAPI/HikAGVWebAPI/SQLData/SQLData.cs)

**修改內容**：在 `Update_oShuttle` 方法中新增 `MapCode` 欄位更新

```diff
  public void Update_oShuttle(AGVStatusData agvStatus)
  {
      string sSQL = $@"update oShuttle
                          set Battery = '{agvStatus?.battery}'
                             ,Status = '{agvStatus?.status}'
                             ,PosX = '{agvStatus?.posX.PadRight(6, '0')}'
                             ,PosY = '{agvStatus?.posY.PadRight(6, '0')}'
                             ,RobotDir = '{agvStatus?.robotDir}'
+                            ,MapCode = '{agvStatus?.mapCode}'
                        where ShuttleId = {agvStatus?.robotCode} ";
      mSql.WriteSqlByAutoOpen(sSQL);
  }
```

**修改前（5個欄位）**：
- Battery
- Status
- PosX
- PosY
- RobotDir

**修改後（6個欄位）**：
- Battery
- Status
- PosX
- PosY
- RobotDir
- ✅ **MapCode**（新增）

### 完整的資料流（修正後）

```
AGV 20106 從 1F 移動到 3F
  ↓
海康 RCS 回報狀態
  ↓
[Robot Code]: 20106
[Map Code]: DD  ← 從 AA 變成 DD
[Pos X]: 215000
[Pos Y]: 200000
  ↓
Dispatch.cs 接收並呼叫 Update_oShuttle()
  ↓
✅ 資料庫更新：
   - MapCode = 'DD'  ← 現在會更新了！
   - PosX = '215000'
   - PosY = '200000'
   - Battery, Status, RobotDir 等
  ↓
Web 用戶切換到 3F
  ↓
GetMapCodeFromArea("FHT1-3F") → "DD"
  ↓
查詢: WHERE MapCode = 'DD'
  ↓
✅ 找到 AGV 20106
  ↓
✅ 正確顯示在 3F 地圖上！
```

### 兩階段解決方案總結

#### 階段 1：Web 查詢層（CommonController.cs）
**問題**：Web 區域代碼（FHT1-1F）與海康 MapCode（AA）不匹配  
**解決**：新增 `GetMapCodeFromArea()` 轉換方法  
**效果**：能正確查詢到對應樓層的 AGV

#### 階段 2：資料庫更新層（SQLData.cs）
**問題**：資料庫 MapCode 沒有隨 AGV 移動而更新  
**解決**：在 UPDATE 語句中新增 `MapCode` 欄位  
**效果**：資料庫即時反映 AGV 當前所在樓層

### 為什麼需要兩個修正？

1. **只有階段 1**：查詢時能找到 AGV，但 AGV 移動到其他樓層後，資料庫 MapCode 不變，顯示錯誤
2. **只有階段 2**：資料庫會更新 MapCode，但查詢時用錯誤的代碼（FHT1-1F vs AA），找不到 AGV
3. **兩者配合**：✅ 資料庫正確更新 + 查詢正確轉換 = 完美運作

---

## 六、驗證計畫


### 資料庫驗證

```sql
-- 連接資料庫: agvDB_1400004_1
SELECT ShuttleId, MapCode, Status, PosX, PosY, Battery 
FROM oShuttle
ORDER BY ShuttleId;
```

**預期結果**：AGV 的 MapCode 應為 `AA`, `BB`, `DD`, `FF`（不是 `FHT1-1F`）

### Web 畫面測試

#### 測試案例 1：1F 地圖顯示

1. 啟動 Web 應用程式
2. 進入地圖頁面
3. 切換到 **1F** 樓層
4. **預期結果**：MapCode = "AA" 的 AGV 應該顯示在 1F 地圖上

#### 測試案例 2：3F 地圖顯示

1. 切換到 **3F** 樓層
2. **預期結果**：MapCode = "DD" 的 AGV 應該顯示在 3F 地圖上

#### 測試案例 3：即時更新

1. 等待 AGV 移動或透過派車觸發移動
2. 觀察 AGV 位置是否即時更新
3. **預期結果**：即時位置更新功能正常運作

### 瀏覽器檢查

1. 開啟瀏覽器開發者工具（F12）
2. 切換樓層時檢查 Console
3. **預期結果**：無 JavaScript 錯誤
4. 檢查 Network 標籤，確認 API 呼叫正常

---

## 七、重要注意事項

### 座標設定限制

目前 `appsettings.json` 中只有兩個樓層的座標轉換設定：
- `FHT1-1F` (1F, MapCode AA)
- `FHT1-3F` (3F, MapCode DD)

如需顯示 **2F** 或 **4F** 地圖，需要：
1. 取得這些樓層的座標校準資料
2. 在 `AgvSetting` 中新增對應設定：
   - `FHT1-2F` for MapCode BB
   - `FHT1-4F` for MapCode FF

### 向後兼容性

`GetMapCodeFromArea()` 方法包含容錯機制：
- 如果找不到映射，返回原始 `area` 值
- 確保系統不會因為缺少映射而中斷

---

## 八、技術總結

### 問題本質

**系統整合時的命名空間不一致**：
- 海康 RCS 使用自己的 MapCode 命名規則（AA/BB/DD/FF）
- Web 系統使用自己的區域代碼（FHT1-1F/2F/3F/4F）
- 兩者之間缺少轉換層，導致查詢失敗

### 解決方案優點

1. ✅ **低侵入性**：只修改必要的查詢邏輯
2. ✅ **可維護性**：對應關係集中在配置檔
3. ✅ **可擴展性**：輕鬆新增更多樓層映射
4. ✅ **向後兼容**：不破壞現有功能
5. ✅ **職責分離**：MapCode 查詢與座標轉換各司其職

### 學習要點

1. **系統整合時要注意命名規範的一致性**
2. **使用配置檔管理系統間的對應關係**
3. **設計時要考慮向後兼容性**
4. **分離關注點：查詢 vs. 顯示**

---

## 九、後續建議

### 短期改進

- [ ] 補齊 2F 和 4F 的座標轉換設定（如需要）
- [ ] 進行完整的用戶驗收測試
- [ ] 更新系統文檔

### 長期優化

- [ ] 考慮在資料庫層面統一 MapCode 格式
- [ ] 建立 MapCode 管理介面
- [ ] 加入自動化測試保護這個邏輯

---

## 附錄：相關檔案

### 修改檔案

1. [appsettings.json](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/WebGui/SCP/appsettings.json) - 新增 MapCodeMapping 配置
2. [CommonController.cs](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/WebGui/SCP/Controllers/CommonController.cs) - 新增 MapCode 轉換邏輯
3. [SQLData.cs](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/ACC/HikAGVWebAPI/HikAGVWebAPI/SQLData/SQLData.cs) - 在 Update_oShuttle 中新增 MapCode 更新

### 分析檔案

1. [Dispatch.cs](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/ACC/HikAGVWebAPI/HikAGVWebAPI/App_Start/Dispatch.cs) - AGV 狀態更新邏輯
2. [SQLData.cs](file:///d:/專案管理/Custom/GlueNet.SAA.FHT.AgvTranfer.OneFactory/ACC/HikAGVWebAPI/HikAGVWebAPI/SQLData/SQLData.cs) - 資料庫更新方法

### 文檔檔案

1. [implementation_plan.md](file:///C:/Users/user/.gemini/antigravity/brain/b8be6b4e-8b7d-4ce6-8f47-8f88fa62a51b/implementation_plan.md) - 實作計畫
2. [walkthrough.md](file:///C:/Users/user/.gemini/antigravity/brain/b8be6b4e-8b7d-4ce6-8f47-8f88fa62a51b/walkthrough.md) - 修改紀錄
3. [task.md](file:///C:/Users/user/.gemini/antigravity/brain/b8be6b4e-8b7d-4ce6-8f47-8f88fa62a51b/task.md) - 任務清單

---

**文檔版本**: 2.0  
**最後更新**: 2026-01-14  
**狀態**: ✅ 已完成雙階段實作（Web 查詢層 + 資料庫更新層），等待驗證
