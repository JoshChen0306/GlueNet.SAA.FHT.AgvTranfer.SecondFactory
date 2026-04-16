-- ============================================================
-- 連動取消 SQL 驗證腳本
-- 用途：驗證 SCP DeleteoNeed 連動取消邏輯的正確性
-- 使用方式：在 SSMS 逐段執行，觀察 SELECT 結果
-- ============================================================

-- ============================================================
-- 前置：清理測試資料（確保乾淨環境）
-- ============================================================
DELETE FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001', 'TEST_MCS_002', 'TEST_DISPATCH_002', 'TEST_MCS_003');
DELETE FROM oMission WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001', 'TEST_MCS_002', 'TEST_DISPATCH_002', 'TEST_MCS_003');

PRINT '========================================';
PRINT '情境 1：取消 MCS → 預調度也被取消';
PRINT '========================================';

-- 插入 MCS 任務
INSERT INTO oMission (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, ParentTaskDateTime)
VALUES ('TEST_MCS_001', 0, 'A01', 'B01', 'MCS', '-1', '', NULL);
INSERT INTO oRequire (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, AssignFlag)
VALUES ('TEST_MCS_001', 'A01', 0, 'A01', 'B01', 'MCS', '', '', 'Y');

-- 插入預調度任務（ParentTaskDateTime 指向 MCS）
INSERT INTO oMission (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, ParentTaskDateTime)
VALUES ('TEST_DISPATCH_001', 0, 'EV01', 'EV02', 'CROSS_FLOOR_DISPATCH', '-1', '', 'TEST_MCS_001');
INSERT INTO oRequire (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, AssignFlag)
VALUES ('TEST_DISPATCH_001', 'EV01', 0, 'EV01', 'EV02', 'CROSS_FLOOR_DISPATCH', '', '', 'Y');

-- 驗證：兩筆都存在且 OkFlag 為 NULL
SELECT '插入後' AS [階段], TaskDateTime, TaskSource, OkFlag, ParentTaskDateTime FROM oMission WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001');
SELECT '插入後' AS [階段], TaskDateTime, TaskSource, OkFlag FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001');

-- 模擬取消 MCS（SCP DeleteoNeed 邏輯）
-- 1. 取消 MCS 本身
UPDATE oMission SET OkFlag = 'C' WHERE TaskDateTime = 'TEST_MCS_001';
UPDATE oRequire SET OkFlag = 'C' WHERE TaskDateTime = 'TEST_MCS_001';
-- 2. 連動取消以 MCS 為 Parent 的預調度
UPDATE oMission SET OkFlag = 'C' WHERE ParentTaskDateTime = 'TEST_MCS_001';
UPDATE oRequire SET OkFlag = 'C' WHERE TaskDateTime IN (SELECT TaskDateTime FROM oMission WHERE ParentTaskDateTime = 'TEST_MCS_001');

-- 驗證：兩筆 OkFlag 都應為 'C'
SELECT '取消後' AS [階段], TaskDateTime, TaskSource, OkFlag, ParentTaskDateTime FROM oMission WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001');
SELECT '取消後' AS [階段], TaskDateTime, TaskSource, OkFlag FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001');

-- 清理
DELETE FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001');
DELETE FROM oMission WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_DISPATCH_001');

PRINT '';
PRINT '========================================';
PRINT '情境 2：取消預調度 → MCS 先被取消';
PRINT '========================================';

-- 插入 MCS 任務
INSERT INTO oMission (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, ParentTaskDateTime)
VALUES ('TEST_MCS_002', 0, 'A01', 'B01', 'MCS', '-1', '', NULL);
INSERT INTO oRequire (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, AssignFlag)
VALUES ('TEST_MCS_002', 'A01', 0, 'A01', 'B01', 'MCS', '', '', 'Y');

-- 插入預調度任務
INSERT INTO oMission (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, ParentTaskDateTime)
VALUES ('TEST_DISPATCH_002', 0, 'EV01', 'EV02', 'CROSS_FLOOR_DISPATCH', '-1', '', 'TEST_MCS_002');
INSERT INTO oRequire (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, AssignFlag)
VALUES ('TEST_DISPATCH_002', 'EV01', 0, 'EV01', 'EV02', 'CROSS_FLOOR_DISPATCH', '', '', 'Y');

-- 驗證：兩筆都存在
SELECT '插入後' AS [階段], TaskDateTime, TaskSource, OkFlag, ParentTaskDateTime FROM oMission WHERE TaskDateTime IN ('TEST_MCS_002', 'TEST_DISPATCH_002');

-- 模擬取消預調度（SCP DeleteoNeed 邏輯）
-- 1. 查詢 ParentTaskDateTime
DECLARE @ParentTDT VARCHAR(50);
SELECT @ParentTDT = ParentTaskDateTime FROM oMission WHERE TaskDateTime = 'TEST_DISPATCH_002';
PRINT '預調度的 ParentTaskDateTime = ' + ISNULL(@ParentTDT, 'NULL');
-- 2. 先取消 MCS（Parent）
UPDATE oMission SET OkFlag = 'C' WHERE TaskDateTime = @ParentTDT;
UPDATE oRequire SET OkFlag = 'C' WHERE TaskDateTime = @ParentTDT;
-- 3. 再取消預調度本身
UPDATE oMission SET OkFlag = 'C' WHERE TaskDateTime = 'TEST_DISPATCH_002';
UPDATE oRequire SET OkFlag = 'C' WHERE TaskDateTime = 'TEST_DISPATCH_002';

-- 驗證：兩筆 OkFlag 都應為 'C'
SELECT '取消後' AS [階段], TaskDateTime, TaskSource, OkFlag, ParentTaskDateTime FROM oMission WHERE TaskDateTime IN ('TEST_MCS_002', 'TEST_DISPATCH_002');
SELECT '取消後' AS [階段], TaskDateTime, TaskSource, OkFlag FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_002', 'TEST_DISPATCH_002');

-- 清理
DELETE FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_002', 'TEST_DISPATCH_002');
DELETE FROM oMission WHERE TaskDateTime IN ('TEST_MCS_002', 'TEST_DISPATCH_002');

PRINT '';
PRINT '========================================';
PRINT '情境 3：同起訖點兩筆 MCS → 只取消其中一筆';
PRINT '========================================';

-- 插入兩筆同起訖點的 MCS 任務
INSERT INTO oMission (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, ParentTaskDateTime)
VALUES ('TEST_MCS_001', 0, 'A01', 'B01', 'MCS', '-1', '', NULL);
INSERT INTO oRequire (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, AssignFlag)
VALUES ('TEST_MCS_001', 'A01', 0, 'A01', 'B01', 'MCS', '', '', 'Y');

INSERT INTO oMission (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, ParentTaskDateTime)
VALUES ('TEST_MCS_003', 0, 'A01', 'B01', 'MCS', '-1', '', NULL);
INSERT INTO oRequire (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, AssignFlag)
VALUES ('TEST_MCS_003', 'A01', 0, 'A01', 'B01', 'MCS', '', '', 'Y');

-- 驗證：兩筆都存在
SELECT '插入後' AS [階段], TaskDateTime, TaskSource, OkFlag FROM oMission WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_MCS_003');

-- 模擬只取消 TEST_MCS_001（用 TaskDateTime 精確匹配）
UPDATE oMission SET OkFlag = 'C' WHERE TaskDateTime = 'TEST_MCS_001';
UPDATE oRequire SET OkFlag = 'C' WHERE TaskDateTime = 'TEST_MCS_001';

-- 驗證：TEST_MCS_001 OkFlag='C'，TEST_MCS_003 OkFlag 仍為 NULL
SELECT '取消後' AS [階段], TaskDateTime, TaskSource, OkFlag FROM oMission WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_MCS_003');
SELECT '取消後' AS [階段], TaskDateTime, TaskSource, OkFlag FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_MCS_003');

-- 清理
DELETE FROM oRequire WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_MCS_003');
DELETE FROM oMission WHERE TaskDateTime IN ('TEST_MCS_001', 'TEST_MCS_003');

PRINT '';
PRINT '========================================';
PRINT '全部測試完成，資料已清理';
PRINT '========================================';
