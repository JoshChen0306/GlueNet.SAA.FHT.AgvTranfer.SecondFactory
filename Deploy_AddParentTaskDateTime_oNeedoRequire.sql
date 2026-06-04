-- ============================================================
-- 空平板取消連動清源頭 — Step 1
-- oNeed / oRequire 新增 ParentTaskDateTime 欄位
--
-- 用途：記錄空平板回收任務（O→Q）與觸發它的 M→O 物料任務
--       之間的關聯，供 SCP 取消時連動清除卡控中的源頭 oNeed。
--
-- 註：oNeed / oRequire 無對應 ub 歸檔表，且各處 INSERT 皆採
--     明確欄位清單、SELECT * 以欄位名取值，故加欄位安全，
--     不需同步其他表。oMission/ubMission 已於前次 migration
--     加過 ParentTaskDateTime（Deploy_AddParentTaskDateTime.sql）。
--
-- 執行日期：2026-06-04
-- ============================================================

-- oNeed：待派送需求表（卡控中的 M→O 源頭存於此）
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'oNeed' AND COLUMN_NAME = 'ParentTaskDateTime'
)
BEGIN
    ALTER TABLE oNeed ADD ParentTaskDateTime varchar(50) NULL;
    PRINT 'oNeed.ParentTaskDateTime 已新增';
END
ELSE
BEGIN
    PRINT 'oNeed.ParentTaskDateTime 已存在，跳過';
END
GO

-- oRequire：派送需求表（O→Q 於此顯示於 SCP 畫面、為取消入口）
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'oRequire' AND COLUMN_NAME = 'ParentTaskDateTime'
)
BEGIN
    ALTER TABLE oRequire ADD ParentTaskDateTime varchar(50) NULL;
    PRINT 'oRequire.ParentTaskDateTime 已新增';
END
ELSE
BEGIN
    PRINT 'oRequire.ParentTaskDateTime 已存在，跳過';
END
GO

-- 驗證
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('oNeed', 'oRequire')
  AND COLUMN_NAME = 'ParentTaskDateTime';
GO
