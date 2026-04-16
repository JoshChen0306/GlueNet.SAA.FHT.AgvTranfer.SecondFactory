-- ============================================================
-- 跨樓層任務取消機制完善 — Step 2
-- 新增 ubCancelLog 資料表
--
-- 用途：記錄所有跨樓層任務的強制取消操作，
--       包含操作者、任務資訊、RCS 回應、連動取消項目。
--
-- 執行日期：2026-04-__
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'ubCancelLog'
)
BEGIN
    CREATE TABLE ubCancelLog (
        Id                   bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CancelTime           datetime             NOT NULL DEFAULT GETDATE(),
        Operator             varchar(50)          NULL,
        TaskDateTime         varchar(50)          NOT NULL,
        ParentTaskDateTime   varchar(50)          NULL,
        TaskSource           varchar(50)          NULL,
        BeginStation         varchar(10)          NULL,
        EndStation           varchar(10)          NULL,
        TaskCode             varchar(50)          NULL,
        RcsCancelResult      varchar(200)         NULL,
        LinkedTaskDateTimes  varchar(200)         NULL
    );
    PRINT 'ubCancelLog 資料表已建立';
END
ELSE
BEGIN
    PRINT 'ubCancelLog 資料表已存在，跳過';
END
GO

-- 驗證
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ubCancelLog'
ORDER BY ORDINAL_POSITION;
GO
