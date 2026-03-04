-- =============================================
-- pRoute 1F 路線部署腳本 (新版權限管理)
-- 更新日期: 2026-03-04
-- 說明: 獨立部署 1F 站內派送路線，供新版權限機制使用
-- =============================================

-- 1. 新增 1F 備料區路線 (A -> B)
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_1F_A_TO_B' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'1F 備料區(A) → 暫存區(B)', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'A', TargetAreas = 'B', ControlFlag = 'Y', SortOrder = 11, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_1F_A_TO_B', N'1F 備料區(A) → 暫存區(B)', '1F', '1F', 'INTERNAL', 'A', 'B', 'Y', 11, 'DISPATCH');

-- 2. 新增 1F 待料上料區路線 (C -> D)
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_1F_C_TO_D' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'1F 待料上料(C) → 待料下料(D)', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'C', TargetAreas = 'D', ControlFlag = 'Y', SortOrder = 12, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_1F_C_TO_D', N'1F 待料上料(C) → 待料下料(D)', '1F', '1F', 'INTERNAL', 'C', 'D', 'Y', 12, 'DISPATCH');

-- 3. 新增 1F 待料下料區路線 (D -> E)
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_1F_D_TO_E' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'1F 待料下料(D) → 暫存區(E)', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'D', TargetAreas = 'E', ControlFlag = 'Y', SortOrder = 13, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_1F_D_TO_E', N'1F 待料下料(D) → 暫存區(E)', '1F', '1F', 'INTERNAL', 'D', 'E', 'Y', 13, 'DISPATCH');

-- 4. 新增 1F 下料區路線 (F)
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_1F_F_UNLOAD' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'1F 下料區(F) 下料完成', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'F', TargetAreas = '', ControlFlag = 'Y', SortOrder = 14, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_1F_F_UNLOAD', N'1F 下料區(F) 下料完成', '1F', '1F', 'INTERNAL', 'F', '', 'Y', 14, 'DISPATCH');

-- 驗證
SELECT RouteId, RouteName, SourceFloor, SourceAreas, TargetAreas, SortOrder
FROM pRoute 
WHERE SourceFloor = '1F'
ORDER BY SortOrder;
GO
