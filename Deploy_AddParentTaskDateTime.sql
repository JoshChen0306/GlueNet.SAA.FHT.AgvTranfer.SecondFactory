-- ============================================================
-- 跨樓層任務取消機制完善 — Step 1
-- oMission / ubMission 新增 ParentTaskDateTime 欄位
--
-- 用途：記錄預調度任務（CROSS_FLOOR_DISPATCH）與觸發它的
--       MCS 原始任務之間的關聯，供連動取消使用。
--
-- 執行日期：2026-04-__
-- ============================================================

-- oMission：執行中任務表
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'oMission' AND COLUMN_NAME = 'ParentTaskDateTime'
)
BEGIN
    ALTER TABLE oMission ADD ParentTaskDateTime varchar(50) NULL;
    PRINT 'oMission.ParentTaskDateTime 已新增';
END
ELSE
BEGIN
    PRINT 'oMission.ParentTaskDateTime 已存在，跳過';
END
GO

-- ubMission：歷史歸檔表（Insert_ubMission 使用 select *，欄位須與 oMission 同步）
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ubMission' AND COLUMN_NAME = 'ParentTaskDateTime'
)
BEGIN
    ALTER TABLE ubMission ADD ParentTaskDateTime varchar(50) NULL;
    PRINT 'ubMission.ParentTaskDateTime 已新增';
END
ELSE
BEGIN
    PRINT 'ubMission.ParentTaskDateTime 已存在，跳過';
END
GO

-- 驗證
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('oMission', 'ubMission')
  AND COLUMN_NAME = 'ParentTaskDateTime';
GO
