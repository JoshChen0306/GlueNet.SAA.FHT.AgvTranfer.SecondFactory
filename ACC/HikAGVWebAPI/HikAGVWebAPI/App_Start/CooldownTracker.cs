using System;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>
    /// 跨樓層預調度「同父任務冷卻期」追蹤器（方案 C 核心）
    /// 單台車情境，僅保留最近一次完成的 ParentTaskDateTime + 完成時間
    /// 供 CrossFloorManager.SelectNextMission 判斷是否於冷卻期內應攔截預調度重派
    ///
    /// 執行緒安全：Dispatch 主循環（讀）與 Callback 回呼（寫）會跨執行緒存取，
    /// 以 lock 保護欄位讀寫。
    /// </summary>
    public class CooldownTracker
    {
        private readonly int _cooldownSeconds;
        private readonly Func<DateTime> _nowProvider;
        private readonly object _lock = new object();

        private string _parentTaskDateTime;
        private DateTime? _completedAt;

        public CooldownTracker(int cooldownSeconds = 30, Func<DateTime> nowProvider = null)
        {
            _cooldownSeconds = cooldownSeconds;
            _nowProvider = nowProvider ?? (() => DateTime.Now);
        }

        /// <summary>
        /// 記錄一筆預調度/歸位完成。null 或空字串 parent 一律略過（無法建立對應關聯）。
        /// 連續呼叫以最新一筆為準（單欄位儲存）。
        /// </summary>
        public void RecordCompletion(string parentTaskDateTime)
        {
            if (string.IsNullOrEmpty(parentTaskDateTime)) return;
            lock (_lock)
            {
                _parentTaskDateTime = parentTaskDateTime;
                _completedAt = _nowProvider();
            }
        }

        /// <summary>
        /// 啟動時從 ubMission 重建冷卻狀態（Task 5 用）。
        /// 與 RecordCompletion 的差異：completedAt 由外部指定（取自 ubMission.EndTime）。
        /// </summary>
        public void Initialize(string parentTaskDateTime, DateTime completedAt)
        {
            if (string.IsNullOrEmpty(parentTaskDateTime)) return;
            lock (_lock)
            {
                _parentTaskDateTime = parentTaskDateTime;
                _completedAt = completedAt;
            }
        }

        /// <summary>
        /// 檢查給定 parent 是否仍在冷卻期內（true → SelectNextMission 應攔截預調度）
        /// 匹配條件：parent 相同 AND 距完成時間 &lt; cooldownSeconds
        /// </summary>
        public bool IsInCooldown(string parentTaskDateTime)
        {
            if (string.IsNullOrEmpty(parentTaskDateTime)) return false;
            lock (_lock)
            {
                if (_parentTaskDateTime != parentTaskDateTime) return false;
                if (_completedAt == null) return false;
                double elapsed = (_nowProvider() - _completedAt.Value).TotalSeconds;
                return elapsed < _cooldownSeconds;
            }
        }
    }
}
