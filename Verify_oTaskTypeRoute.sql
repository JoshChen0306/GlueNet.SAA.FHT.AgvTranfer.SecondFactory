-- =============================================
-- oTaskTypeRoute 遷移驗證腳本
-- 執行日期：2026-03-20
-- 用途：確認 DB 資料與原 config 設定一致
-- =============================================

-- 1. 檢查資料表是否存在
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[oTaskTypeRoute]') AND type in (N'U'))
BEGIN
    PRINT '[FAIL] oTaskTypeRoute 資料表不存在！'
    RETURN
END
PRINT '[PASS] oTaskTypeRoute 資料表存在'
GO

-- 2. 顯示所有資料
PRINT ''
PRINT '=== 全部資料 ==='
SELECT * FROM [dbo].[oTaskTypeRoute] ORDER BY [MoveType], [FromFloor], [ToFloor]
GO

-- 3. 驗證 Transport 同樓層（原 SameFloorTaskTypeMap="1F:F002,2F:F001"）
PRINT ''
PRINT '=== 驗證 Transport 同樓層 ==='
IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='1F' AND ToFloor='1F' AND TaskType='F002' AND UseFlag='Y')
    PRINT '[PASS] 1F->1F Transport = F002'
ELSE
    PRINT '[FAIL] 1F->1F Transport 應為 F002'

IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='2F' AND ToFloor='2F' AND TaskType='F001' AND UseFlag='Y')
    PRINT '[PASS] 2F->2F Transport = F001'
ELSE
    PRINT '[FAIL] 2F->2F Transport 應為 F001'
GO

-- 4. 驗證 Transport 跨樓層（原 CrossFloorTaskTypeMap）
PRINT ''
PRINT '=== 驗證 Transport 跨樓層 ==='
IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='1F' AND ToFloor='3F' AND TaskType='F13Test' AND UseFlag='Y')
    PRINT '[PASS] 1F->3F Transport = F13Test'
ELSE
    PRINT '[FAIL] 1F->3F Transport 應為 F13Test'

IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='3F' AND ToFloor='1F' AND TaskType='F31Test' AND UseFlag='Y')
    PRINT '[PASS] 3F->1F Transport = F31Test'
ELSE
    PRINT '[FAIL] 3F->1F Transport 應為 F31Test'

IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='3F' AND ToFloor='4F' AND TaskType='F34Test' AND UseFlag='Y')
    PRINT '[PASS] 3F->4F Transport = F34Test'
ELSE
    PRINT '[FAIL] 3F->4F Transport 應為 F34Test'

IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='4F' AND ToFloor='3F' AND TaskType='F43Test' AND UseFlag='Y')
    PRINT '[PASS] 4F->3F Transport = F43Test'
ELSE
    PRINT '[FAIL] 4F->3F Transport 應為 F43Test'

IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='2F' AND ToFloor='4F' AND TaskType='F24Test' AND UseFlag='Y')
    PRINT '[PASS] 2F->4F Transport = F24Test'
ELSE
    PRINT '[FAIL] 2F->4F Transport 應為 F24Test'

IF EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE MoveType='Transport' AND FromFloor='4F' AND ToFloor='2F' AND TaskType='F42Test' AND UseFlag='Y')
    PRINT '[PASS] 4F->2F Transport = F42Test'
ELSE
    PRINT '[FAIL] 4F->2F Transport 應為 F42Test'
GO

-- 5. 驗證 EmptyMove 空車移動
PRINT ''
PRINT '=== 驗證 EmptyMove 空車移動 ==='
DECLARE @emCount int
SELECT @emCount = COUNT(*) FROM [dbo].[oTaskTypeRoute] WHERE MoveType='EmptyMove' AND UseFlag='Y'
PRINT '[INFO] EmptyMove 啟用筆數: ' + CAST(@emCount AS VARCHAR)

IF @emCount >= 6
    PRINT '[PASS] EmptyMove 至少 6 筆路線'
ELSE
    PRINT '[FAIL] EmptyMove 應至少 6 筆路線'
GO

-- 6. 資料筆數統計
PRINT ''
PRINT '=== 統計 ==='
SELECT MoveType, COUNT(*) AS Count
FROM [dbo].[oTaskTypeRoute]
WHERE UseFlag = 'Y'
GROUP BY MoveType
GO
