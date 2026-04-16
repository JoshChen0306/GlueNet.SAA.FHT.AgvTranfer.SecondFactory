using HikAGVDll;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>
    /// 跨樓層車輛管理器
    /// 負責：AGV 閒置自動歸位、預調度任務管控
    /// </summary>
    public class CrossFloorManager
    {
        // ─── 設定 ────────────────────────────────────────────────
        private readonly string _shuttleId;
        private readonly int _idleReturnTimeoutSeconds;
        private readonly string _idleReturnFloor;
        private readonly Dictionary<string, string> _mapCodeFloorMapping;  // MapCode → Floor

        // ─── 依賴注入 ─────────────────────────────────────────────
        private readonly SQLData _mDB;
        private readonly Log _mLog;
        private readonly HikAGV _hikAGV;
        private readonly ElevatorPathCalculator _pathCalculator;

        // ─── 歸位狀態 ─────────────────────────────────────────────
        private DateTime? _idleStartTime;           // 計時開始時間（null = 未計時）
        private bool _idleReturnDispatched;         // 歸位任務已寫入 oMission（待 Dispatch 派發）
        private bool _crossFloorDispatchPending;    // 預調度任務已寫入 oMission（待 Dispatch 派發）

        // ─── 任務名稱常數 ─────────────────────────────────────────
        public const string IDLE_RETURN = "IDLE_RETURN";
        public const string CROSS_FLOOR_DISPATCH = "CROSS_FLOOR_DISPATCH";

        public CrossFloorManager(
            string shuttleId,
            int idleReturnTimeoutSeconds,
            string idleReturnFloor,
            string mapCodeFloorMapping,
            SQLData mDB,
            Log mLog,
            HikAGV hikAGV,
            ElevatorSettings elevatorSettings)
        {
            _shuttleId = shuttleId;
            _idleReturnTimeoutSeconds = idleReturnTimeoutSeconds;
            _idleReturnFloor = idleReturnFloor;
            _mapCodeFloorMapping = mapCodeFloorMapping.Split(',')
                .Select(s => s.Trim().Split(':'))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            _mDB = mDB;
            _mLog = mLog;
            _hikAGV = hikAGV;
            _pathCalculator = new ElevatorPathCalculator(elevatorSettings);
        }

        /// <summary>
        /// 每個 Dispatch 主循環週期呼叫一次
        /// 負責監控車輛 IDLE 狀態並觸發歸位邏輯
        /// </summary>
        public void Tick()
        {
            try
            {
                oShuttleModel shuttle = GetShuttleStatus();
                if (shuttle == null)
                    return;

                // 連線逾時檢查（UpdateTime 超過 60 秒未更新視為離線）
                if (shuttle.UpdateTime == null ||
                    (DateTime.Now - shuttle.UpdateTime.Value).TotalSeconds > 60)
                {
                    if (_idleStartTime != null)
                    {
                        _mLog.TraceOut($"[CrossFloor] 車輛 {_shuttleId} 離線，重置歸位計時", Log.LogType.NONE);
                        ResetIdleTimer();
                    }
                    return;
                }

                // 車輛執行中（含歸位任務本身），不處理
                if (shuttle.Status == "R")
                {
                    return;
                }

                // oMission 交叉檢查：RCS 回報非執行狀態，但 oMission 仍有跨樓層任務在跑
                // （跨樓層途中 RCS 可能短暫回報非執行狀態，如等電梯、遇障暫停）
                if (HasRunningCrossFloorMission())
                {
                    if (_idleStartTime != null)
                    {
                        _mLog.TraceOut($"[CrossFloor] 跨樓層任務執行中（oMission OkFlag=R），重置歸位計時", Log.LogType.NONE);
                        ResetIdleTimer();
                    }
                    return;
                }

                // 車輛 IDLE
                if (shuttle.Status == "I")
                {
                    // 歸位任務已派發，等待 RCS 執行（狀態尚未更新為 R）
                    if (_idleReturnDispatched)
                        return;

                    // 預調度任務已派發，等待 RCS 執行
                    if (_crossFloorDispatchPending)
                        return;

                    // 已在母樓層，不需歸位，重置計時
                    if (IsOnHomeFloor(shuttle))
                    {
                        ResetIdleTimer();
                        return;
                    }

                    // 未啟動計時 → 啟動
                    if (_idleStartTime == null)
                    {
                        _idleStartTime = DateTime.Now;
                        _mLog.TraceOut($"[CrossFloor] 車輛 {_shuttleId} 閒置計時開始，MapCode={shuttle.MapCode}（{GetFloorByMapCode(shuttle.MapCode)}）", Log.LogType.NONE);
                        return;
                    }

                    // 計時中，尚未超時 → 繼續等待
                    double elapsed = (DateTime.Now - _idleStartTime.Value).TotalSeconds;
                    if (elapsed < _idleReturnTimeoutSeconds)
                        return;

                    // 超時 → 派發歸位任務
                    _mLog.TraceOut($"[CrossFloor] 車輛 {_shuttleId} 閒置超時 {elapsed:F0}s，啟動歸位至 {_idleReturnFloor}", Log.LogType.NONE);
                    DispatchIdleReturn(shuttle);
                }
            }
            catch (Exception ex)
            {
                _mLog.TraceOut($"[CrossFloor] Tick Exception: {ex.Message}", Log.LogType.NONE);
            }
        }

        /// <summary>
        /// 有新任務進入佇列時呼叫，取消歸位計時（若尚未派發）
        /// </summary>
        public void OnNewTaskArrived()
        {
            if (_idleReturnDispatched)
            {
                // 歸位已派發，新任務排入佇列，等歸位完成後再接
                _mLog.TraceOut($"[CrossFloor] 歸位任務執行中，新任務排入佇列等待", Log.LogType.NONE);
                return;
            }

            if (_idleStartTime != null)
            {
                _mLog.TraceOut($"[CrossFloor] 收到新任務，取消歸位計時", Log.LogType.NONE);
                ResetIdleTimer();
            }
        }

        /// <summary>
        /// 歸位任務完成時呼叫，重置歸位狀態
        /// </summary>
        public void OnIdleReturnCompleted()
        {
            _mLog.TraceOut($"[CrossFloor] 歸位任務完成，重置狀態", Log.LogType.NONE);
            _idleReturnDispatched = false;
            ResetIdleTimer();
        }

        /// <summary>
        /// 預調度任務完成時呼叫，重置預調度狀態
        /// </summary>
        public void OnCrossFloorDispatchCompleted()
        {
            _mLog.TraceOut($"[CrossFloor] 預調度任務完成，重置狀態", Log.LogType.NONE);
            _crossFloorDispatchPending = false;
        }

        /// <summary>
        /// 判斷此車號是否為跨樓層車輛
        /// </summary>
        public bool IsCrossFloorShuttle(string shuttleId)
        {
            return _shuttleId == shuttleId;
        }

        /// <summary>
        /// 判斷任務是否屬於跨樓層車輛管轄
        /// 系統任務（IDLE_RETURN / CROSS_FLOOR_DISPATCH）直接歸屬；
        /// 站內運輸（PLATE_RECOVERY）不歸屬；其餘依起終點是否跨樓層判斷
        /// </summary>
        public bool IsMissionForCrossFloorShuttle(oMissionModel mission)
        {
            if (mission == null) return false;
            if (mission.TaskSource == IDLE_RETURN || mission.TaskSource == CROSS_FLOOR_DISPATCH)
                return true;
            if (mission.TaskSource == "PLATE_RECOVERY")
                return false;
            return _pathCalculator.IsCrossFloor(mission.BeginStation, mission.EndStation);
        }

        /// <summary>
        /// 從待派發的跨樓層任務清單中選出下一筆應派發的任務
        /// 若需要預調度，會建立 CROSS_FLOOR_DISPATCH 寫入 oMission 並回傳
        /// 回傳 null 表示目前無法（或不需要）派發
        /// </summary>
        public oMissionModel SelectNextMission(List<oMissionModel> crossFloorPending)
        {
            try
            {
                // 系統任務（已寫入 oMission 尚未派發）直接回傳讓 Dispatch 送出
                var systemTask = crossFloorPending.FirstOrDefault(
                    m => m.TaskSource == CROSS_FLOOR_DISPATCH || m.TaskSource == IDLE_RETURN);
                if (systemTask != null)
                    return systemTask;

                // 預調度已送出 RCS（不在 pending），等待 Callback
                if (_crossFloorDispatchPending)
                    return null;

                // 一般跨樓層任務，依建立時間排序
                var normalTasks = crossFloorPending
                    .Where(m => m.TaskSource != IDLE_RETURN && m.TaskSource != CROSS_FLOOR_DISPATCH)
                    .OrderBy(m => m.TaskDateTime)
                    .ToList();

                if (!normalTasks.Any())
                    return null;

                oShuttleModel shuttle = GetShuttleStatus();
                if (shuttle == null) return null;

                string currentFloor = GetFloorByMapCode(shuttle.MapCode);

                // 起點與車輛同樓層的任務優先派發
                var sameFloorTask = normalTasks.FirstOrDefault(m =>
                    _pathCalculator.GetFloor(m.BeginStation) == currentFloor);

                if (sameFloorTask != null)
                {
                    _mLog.TraceOut($"[CrossFloor] 同樓層任務優先派發：{sameFloorTask.BeginStation}→{sameFloorTask.EndStation}", Log.LogType.NONE);
                    return sameFloorTask;
                }

                // 無同樓層任務 → 產生預調度任務，將車移至最早任務的起點樓層
                var triggerTask = normalTasks.First();
                string targetFloor = _pathCalculator.GetFloor(triggerTask.BeginStation);
                if (string.IsNullOrEmpty(currentFloor) || string.IsNullOrEmpty(targetFloor))
                    return null;

                return DispatchCrossFloor(currentFloor, targetFloor, triggerTask.TaskDateTime);
            }
            catch (Exception ex)
            {
                _mLog.TraceOut($"[CrossFloor] SelectNextMission Exception: {ex.Message}", Log.LogType.NONE);
                return null;
            }
        }

        // ─── 私有方法 ─────────────────────────────────────────────

        private bool HasRunningCrossFloorMission()
        {
            try
            {
                var missions = _mDB.Select_oMission();
                return missions.Any(m => m.OkFlag == "R" && IsMissionForCrossFloorShuttle(m));
            }
            catch (Exception ex)
            {
                _mLog.TraceOut($"[CrossFloor] HasRunningCrossFloorMission Exception: {ex.Message}", Log.LogType.NONE);
                return false;
            }
        }

        private oShuttleModel GetShuttleStatus()
        {
            var shuttles = _mDB.Select_oShuttle();
            return shuttles.FirstOrDefault(s => s.ShuttleId == _shuttleId);
        }

        private bool IsOnHomeFloor(oShuttleModel shuttle)
        {
            string currentFloor = GetFloorByMapCode(shuttle.MapCode);
            return currentFloor == _idleReturnFloor;
        }

        private string GetFloorByMapCode(string mapCode)
        {
            if (string.IsNullOrEmpty(mapCode))
                return null;
            string key = mapCode.Trim();
            return _mapCodeFloorMapping.ContainsKey(key) ? _mapCodeFloorMapping[key] : null;
        }

        private void DispatchIdleReturn(oShuttleModel shuttle)
        {
            try
            {
                // 以 MapCode 判斷目前樓層
                string fromFloor = GetFloorByMapCode(shuttle.MapCode);
                if (string.IsNullOrEmpty(fromFloor))
                {
                    _mLog.TraceOut($"[CrossFloor] 無法取得車輛 {_shuttleId} 的目前樓層（MapCode={shuttle.MapCode}），歸位取消", Log.LogType.NONE);
                    return;
                }

                // 確認路徑可建構（驗證用，實際路徑由 Dispatch.GetSchedulingTask 計算）
                var fullPath = _pathCalculator.BuildReturnPath(fromFloor, _idleReturnFloor);
                if (fullPath == null || fullPath.Count == 0)
                {
                    _mLog.TraceOut($"[CrossFloor] 無法建構歸位路徑 {fromFloor}→{_idleReturnFloor}，歸位取消", Log.LogType.NONE);
                    return;
                }

                // 寫入 oMission，由 Dispatch 正常流程派發至 RCS
                oMissionModel mission = new oMissionModel()
                {
                    TaskDateTime = DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                    SerialNo = "0",
                    BeginStation = fullPath.First(),   // fromFloor 電梯等待點
                    EndStation = fullPath.Last(),      // toFloor 電梯等待點
                    TaskSource = IDLE_RETURN,
                    RackId = "-1",
                    WorkOrder = "",
                };

                _mDB.Insert_oMission(mission);
                _mDB.Insert_oRequire(mission);
                _idleReturnDispatched = true;
                ResetIdleTimer();
                _mLog.TraceOut($"[CrossFloor] 歸位任務已寫入 oMission + oRequire，{fromFloor}→{_idleReturnFloor}（{mission.BeginStation}→{mission.EndStation}）", Log.LogType.NONE);
            }
            catch (Exception ex)
            {
                _mLog.TraceOut($"[CrossFloor] DispatchIdleReturn Exception: {ex.Message}", Log.LogType.NONE);
            }
        }

        private void ResetIdleTimer()
        {
            _idleStartTime = null;
        }

        /// <summary>
        /// 建立預調度任務寫入 oMission + oRequire，將車移至 toFloor 電梯等待點
        /// </summary>
        /// <param name="parentTaskDateTime">觸發此預調度的 MCS 任務 TaskDateTime</param>
        private oMissionModel DispatchCrossFloor(string fromFloor, string toFloor, string parentTaskDateTime)
        {
            try
            {
                var fullPath = _pathCalculator.BuildReturnPath(fromFloor, toFloor);
                if (fullPath == null || fullPath.Count == 0)
                {
                    _mLog.TraceOut($"[CrossFloor] 無法建構預調度路徑 {fromFloor}→{toFloor}，預調度取消", Log.LogType.NONE);
                    return null;
                }

                oMissionModel mission = new oMissionModel()
                {
                    TaskDateTime = DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                    SerialNo = "0",
                    BeginStation = fullPath.First(),
                    EndStation = fullPath.Last(),
                    TaskSource = CROSS_FLOOR_DISPATCH,
                    RackId = "-1",
                    WorkOrder = "",
                    ParentTaskDateTime = parentTaskDateTime,
                };

                _mDB.Insert_oMission(mission);
                _mDB.Insert_oRequire(mission);
                _crossFloorDispatchPending = true;
                _mLog.TraceOut($"[CrossFloor] 預調度任務已寫入 oMission + oRequire，{fromFloor}→{toFloor}（{mission.BeginStation}→{mission.EndStation}），關聯 MCS TaskDateTime={parentTaskDateTime}", Log.LogType.NONE);
                return mission;
            }
            catch (Exception ex)
            {
                _mLog.TraceOut($"[CrossFloor] DispatchCrossFloor Exception: {ex.Message}", Log.LogType.NONE);
                return null;
            }
        }
    }
}
