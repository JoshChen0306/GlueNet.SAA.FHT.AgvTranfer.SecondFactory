-- =============================================
-- pFunction 功能選單 - TaskType 路由設定選單項目
-- 更新日期: 2026-03-20
-- 說明: 新增 TaskType 路由設定選單項目 (FunctionNo = 506)
-- =============================================

-- 1. 新增 TaskType 路由設定選單項目
IF NOT EXISTS (SELECT 1 FROM pFunction WHERE FunctionNo = 506)
BEGIN
    INSERT INTO pFunction (ColumnNo, RowNo, FunctionNo, FunctionType, FunctionEnglishName, FunctionChineseName, ControlFlag, WebUrl, WebIcon)
    VALUES (5, 6, 506, N'Maintain', N'TaskType Route', N'TaskType 路由設定', N'Y', N'/TaskTypeRoute', N'fa-solid fa-route');
    PRINT 'TaskType Route menu (FunctionNo=506) added.';
END
ELSE
BEGIN
    PRINT 'TaskType Route menu already exists.';
END
GO

-- 2. 更新管理者群組的 FunctionGroup，加入 506
UPDATE pGroup
SET FunctionGroup = CASE
    WHEN FunctionGroup IS NULL OR FunctionGroup = '' THEN '506'
    WHEN CHARINDEX('506', FunctionGroup) = 0 THEN FunctionGroup + ',506'
    ELSE FunctionGroup
END,
ModifiedTime = CONVERT(VARCHAR(14), GETDATE(), 112) + REPLACE(CONVERT(VARCHAR(8), GETDATE(), 108), ':', '')
WHERE GroupId = '1';

PRINT 'Admin group FunctionGroup updated with 506.';
GO

-- 3. 驗證結果
SELECT FunctionNo, FunctionChineseName, WebUrl FROM pFunction WHERE FunctionNo = 506;
SELECT GroupId, FunctionGroup FROM pGroup WHERE GroupId = '1';
GO
