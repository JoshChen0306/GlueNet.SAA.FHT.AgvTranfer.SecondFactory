-- =============================================
-- pFunction 功能選單 - 上下料區綁定設定選單項目
-- 更新日期: 2026-03-19
-- 說明: 新增上下料區綁定設定選單項目 (FunctionNo = 505)
-- =============================================

-- 1. 新增上下料區綁定設定選單項目
IF NOT EXISTS (SELECT 1 FROM pFunction WHERE FunctionNo = 505)
BEGIN
    INSERT INTO pFunction (ColumnNo, RowNo, FunctionNo, FunctionType, FunctionEnglishName, FunctionChineseName, ControlFlag, WebUrl, WebIcon)
    VALUES (5, 5, 505, N'Maintain', N'Port Binding', N'上下料區綁定設定', N'Y', N'/PortBinding', N'fa-solid fa-link');
    PRINT 'Port Binding menu (FunctionNo=505) added.';
END
ELSE
BEGIN
    PRINT 'Port Binding menu already exists.';
END
GO

-- 2. 更新管理者群組的 FunctionGroup，加入 505
UPDATE pGroup
SET FunctionGroup = CASE
    WHEN FunctionGroup IS NULL OR FunctionGroup = '' THEN '505'
    WHEN CHARINDEX('505', FunctionGroup) = 0 THEN FunctionGroup + ',505'
    ELSE FunctionGroup
END,
ModifiedTime = CONVERT(VARCHAR(14), GETDATE(), 112) + REPLACE(CONVERT(VARCHAR(8), GETDATE(), 108), ':', '')
WHERE GroupId = '1';

PRINT 'Admin group FunctionGroup updated with 505.';
GO

-- 3. 驗證結果
SELECT FunctionNo, FunctionChineseName, WebUrl FROM pFunction WHERE FunctionNo = 505;
SELECT GroupId, FunctionGroup FROM pGroup WHERE GroupId = '1';
GO
