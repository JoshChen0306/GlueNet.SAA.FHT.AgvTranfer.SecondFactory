-- =============================================
-- pRoute 2F 路線更新腳本 (新增 S 區支援)
-- 更新日期: 2026-03-04
-- 說明: 更新 2F 雷雕/Vcut 路線，增加 S 區 (清洗區) 為可選終點
-- =============================================

-- 1. 更新 2F 雷雕/Vcut(M/T) -> 加工/清洗區(O/P/S) 路線
-- 將 TargetAreas 從 'O,P' 更新為 'O,P,S'
UPDATE pRoute 
SET RouteName = N'2F 雷雕/Vcut(M/T) → 加工/清洗區(O/P/S)', 
    TargetAreas = 'O,P,S' 
WHERE RouteId = 'ROUTE_2F_MT_TO_OP';

-- 2. 驗證修改結果
SELECT RouteId, RouteName, SourceAreas, TargetAreas, DispatchMode
FROM pRoute 
WHERE RouteId = 'ROUTE_2F_MT_TO_OP';

PRINT 'pRoute update for Area S completed.';
GO
