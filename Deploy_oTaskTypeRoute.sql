-- =============================================
-- TaskType 路線對照表 - oTaskTypeRoute TABLE
-- 統一管理搬運(Transport)與空車移動(EmptyMove)的 TaskType
-- 執行日期：2026-03-20
-- =============================================

-- 建立 TABLE（若已存在則跳過）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[oTaskTypeRoute]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[oTaskTypeRoute](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [MoveType] [nvarchar](20) NOT NULL,
        [FromFloor] [nvarchar](10) NOT NULL,
        [ToFloor] [nvarchar](10) NOT NULL,
        [TaskType] [nvarchar](50) NOT NULL,
        [UseFlag] [nvarchar](1) NULL,
        [Remark] [nvarchar](100) NULL,
        CONSTRAINT [PK_oTaskTypeRoute] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY]

    PRINT 'oTaskTypeRoute TABLE 建立成功'
END
ELSE
BEGIN
    PRINT 'oTaskTypeRoute TABLE 已存在，跳過建立'
END
GO

-- =============================================
-- 初始資料（由現有 HikAGV.config 遷移）
-- MoveType：Transport=搬運 / EmptyMove=空車移動
-- FromFloor/ToFloor：樓層代碼（1F, 2F, 3F, 4F）
-- TaskType：海康 RCS TaskType 代碼
-- UseFlag：Y=啟用 / N=停用
-- =============================================

-- === Transport 同樓層（原 SameFloorTaskTypeMap） ===
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '1F' AND [ToFloor] = '1F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '1F', '1F', 'F002', 'Y', '1F 站內搬運')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '2F' AND [ToFloor] = '2F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '2F', '2F', 'F001', 'Y', '2F 站內搬運')

-- === Transport 跨樓層（原 CrossFloorTaskTypeMap） ===
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '1F' AND [ToFloor] = '3F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '1F', '3F', 'F13Test', 'Y', '1F→3F 跨樓層搬運')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '3F' AND [ToFloor] = '1F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '3F', '1F', 'F31Test', 'Y', '3F→1F 跨樓層搬運')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '3F' AND [ToFloor] = '4F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '3F', '4F', 'F34Test', 'Y', '3F→4F 跨樓層搬運')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '4F' AND [ToFloor] = '3F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '4F', '3F', 'F43Test', 'Y', '4F→3F 跨樓層搬運')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '2F' AND [ToFloor] = '4F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '2F', '4F', 'F24Test', 'Y', '2F→4F 跨樓層搬運')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '4F' AND [ToFloor] = '2F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '4F', '2F', 'F42Test', 'Y', '4F→2F 跨樓層搬運')

-- === EmptyMove 空車移動（歸位/預調度用，TaskType 待現場定義） ===
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '1F' AND [ToFloor] = '4F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '1F', '4F', 'EM14', 'Y', '1F→4F 空車移動')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '2F' AND [ToFloor] = '4F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '2F', '4F', 'EM24', 'Y', '2F→4F 空車移動')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '3F' AND [ToFloor] = '4F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '3F', '4F', 'EM34', 'Y', '3F→4F 空車移動')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '4F' AND [ToFloor] = '1F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '4F', '1F', 'EM41', 'Y', '4F→1F 空車移動')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '4F' AND [ToFloor] = '2F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '4F', '2F', 'EM42', 'Y', '4F→2F 空車移動')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '4F' AND [ToFloor] = '3F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '4F', '3F', 'EM43', 'Y', '4F→3F 空車移動')

PRINT '初始資料處理完成（已存在的跳過，新增的補上）'
GO

-- 驗證結果
SELECT * FROM [dbo].[oTaskTypeRoute] ORDER BY [MoveType], [FromFloor], [ToFloor]
GO
