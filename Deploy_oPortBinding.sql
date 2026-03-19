-- =============================================
-- 二樓站內運輸空平板自動派送 - oPortBinding TABLE
-- 上料區與下料區綁定關係設定
-- 執行日期：2026-03-19
-- =============================================

-- 建立 TABLE（若已存在則跳過）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[oPortBinding]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[oPortBinding](
        [LoadingPort] [nvarchar](20) NOT NULL,
        [UnloadingPort] [nvarchar](20) NOT NULL,
        [FallbackAreas] [nvarchar](100) NULL,
        [UseFlag] [nvarchar](1) NULL
    ) ON [PRIMARY]

    PRINT 'oPortBinding TABLE 建立成功'
END
ELSE
BEGIN
    PRINT 'oPortBinding TABLE 已存在，跳過建立'
END
GO

-- =============================================
-- 初始資料（依現場實際綁定關係調整）
-- LoadingPort：上料區站點
-- UnloadingPort：對應下料區站點
-- FallbackAreas：空平板回收候補區域（逗號分隔，依序找空位）
-- UseFlag：Y=啟用 / N=停用
-- =============================================

-- 先清除舊資料（若需要保留既有設定請註解此段）
-- DELETE FROM [dbo].[oPortBinding]
-- GO

-- 逐筆插入初始綁定關係（已存在則跳過，可重複執行）
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O1')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O1', 'Q1', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O2')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O2', 'Q2', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O3')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O3', 'Q3', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O4')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O4', 'Q4', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O5')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O5', 'Q5', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O6')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O6', 'Q6', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O7')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O7', 'Q7', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'O8')
    INSERT INTO [dbo].[oPortBinding] VALUES ('O8', 'Q8', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P1')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P1', 'Q1', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P2')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P2', 'Q2', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P3')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P3', 'Q3', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P4')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P4', 'Q4', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P5')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P5', 'Q5', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P6')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P6', 'Q6', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P7')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P7', 'Q7', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P8')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P8', 'Q8', 'M,Q,R', 'Y')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oPortBinding] WHERE [LoadingPort] = 'P9')
    INSERT INTO [dbo].[oPortBinding] VALUES ('P9', 'Q9', 'M,Q,R', 'Y')

PRINT '初始資料處理完成（已存在的跳過，新增的補上）'
GO

-- 驗證結果
SELECT * FROM [dbo].[oPortBinding] ORDER BY [LoadingPort]
GO
