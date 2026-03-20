-- 新增 UpdateTime 欄位至 oShuttle 資料表
-- 用途：記錄 AGV 狀態最後更新時間，供 CrossFloorManager 判斷連線狀態
-- 說明：Update_oShuttle 每秒更新此欄位，若超過 60 秒未更新視為離線

ALTER TABLE oShuttle ADD UpdateTime datetime NULL
