using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>
    /// Resolve 的裁決結果（純資料，無 side effect）
    /// </summary>
    public class ShuttleMapCodeResolveResult
    {
        /// <summary>
        /// 第一段去重勝出的「原始」回報筆（mapCode 保持 RCS 回傳值，未被裁決覆寫）。
        /// 診斷與測試用；實際要寫入 DB 的請用 <see cref="DbRow"/>。
        /// 無候選時為 null。
        /// </summary>
        public AGVStatusData Winner { get; set; }

        /// <summary>
        /// 實際應寫入 oShuttle 的那一列，其 mapCode 已等於 <see cref="AcceptedMapCode"/>。
        /// 優先取本輪回報中 mapCode 已等於認可值的那筆（避免出現 MapCode 與座標分屬不同地圖的混搭列）；
        /// 若該地圖本輪沒回報，則複製勝出筆並改寫其 mapCode。
        /// 無候選時為 null。
        /// </summary>
        public AGVStatusData DbRow { get; set; }

        /// <summary>本輪同一 robotCode 是否被多張地圖同時回報</summary>
        public bool HasConflict { get; set; }

        /// <summary>衝突明細（各筆 mapCode/座標與勝出原因），供 WARN log 使用；無衝突時為空字串</summary>
        public string ConflictDetail { get; set; } = string.Empty;

        /// <summary>本輪是否讓 MapCode 變更正式生效（連續確認達標）</summary>
        public bool MapCodeAccepted { get; set; }

        /// <summary>目前累積的確認次數；0 表示無待確認的變更</summary>
        public int PendingCount { get; set; }

        /// <summary>裁決後認可的 MapCode（= 應寫入 oShuttle.MapCode 的值）</summary>
        public string AcceptedMapCode { get; set; }
    }

    /// <summary>
    /// oShuttle.MapCode 裁決器。
    ///
    /// 解決的問題：UpdateAGVStatus 逐張地圖輪詢 RCS，Update_oShuttle 的 where 只有 ShuttleId，
    /// 同一輪多張地圖回報同一台車時，後查到的會無條件覆蓋先查到的
    /// （輪詢順序由 MapCodeFloorMapping 的 key 順序決定，現場 AA,BB,DD,FF → DD 恆勝 BB）。
    /// 2026-08-19 客訴即因車在 2F(BB) 卻被 3F(DD) 的殘留幽靈筆覆蓋，
    /// CrossFloorManager 誤判同樓層而跳過預調度，任務卡在 OkFlag=R 達 19 分鐘。
    ///
    /// 裁決分兩段：
    ///   第一段 同輪去重：多筆時取「座標與上一輪相比有變動」者（幽靈筆的特徵是座標凍結）；
    ///                    無法判別時依序黏著 待確認值 → 認可值 → 上一輪勝出值。
    ///   第二段 連續確認：MapCode 要「變更」需連續 ConfirmCount 輪勝出者皆為同一新值才生效，
    ///                    避免單輪誤判直接寫入 DB。
    ///
    /// 刻意不使用 robotIp 作為判準：該欄位在 ACC 無任何業務用途，
    /// 本地 mock RCS（NormalAPI.cs）硬寫空字串，且 8/19 資料中存在兩筆皆帶 IP 的區間。
    ///
    /// 執行緒：僅由 Dispatch 主循環（Execute → UpdateAGVStatus）單執行緒呼叫，故不加鎖。
    /// callback 路徑走 SQLData.Update_oShuttleMapCode 直接寫 DB，
    /// 由 Resolve 的 dbMapCode 參數回頭同步，不共用本物件狀態。
    /// </summary>
    public class ShuttleMapCodeResolver
    {
        /// <summary>MapCode 變更所需的連續確認輪數（輪詢間隔 1 秒 → 最多延遲 3 秒生效）</summary>
        public const int DefaultConfirmCount = 3;

        private readonly int _confirmCount;

        // robotCode → (mapCode → "posX|posY")：各地圖上一輪回報的座標，用於判斷是否凍結
        private readonly Dictionary<string, Dictionary<string, string>> _lastPositions
            = new Dictionary<string, Dictionary<string, string>>();

        // robotCode → 目前認可的 mapCode（= DB 現值）
        private readonly Dictionary<string, string> _acceptedMapCode
            = new Dictionary<string, string>();

        // robotCode → 上一輪第一段勝出的 mapCode
        private readonly Dictionary<string, string> _lastWinnerMapCode
            = new Dictionary<string, string>();

        // robotCode → 待確認的新 mapCode 與已累積次數
        private readonly Dictionary<string, string> _pendingMapCode
            = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _pendingCount
            = new Dictionary<string, int>();

        public ShuttleMapCodeResolver(int confirmCount = DefaultConfirmCount)
        {
            _confirmCount = confirmCount < 1 ? 1 : confirmCount;
        }

        /// <summary>
        /// 裁決某台車本輪應寫入 oShuttle 的資料。
        /// </summary>
        /// <param name="robotCode">車號（對應 oShuttle.ShuttleId）</param>
        /// <param name="reports">本輪各地圖對此車號的全部回報</param>
        /// <param name="dbMapCode">oShuttle.MapCode 的 DB 現值，用於同步 callback 路徑的寫入</param>
        /// <returns>裁決結果；reports 為空時 Winner / DbRow 為 null</returns>
        public ShuttleMapCodeResolveResult Resolve(string robotCode, IList<AGVStatusData> reports, string dbMapCode)
        {
            var result = new ShuttleMapCodeResolveResult();

            if (string.IsNullOrEmpty(robotCode))
            {
                result.AcceptedMapCode = dbMapCode;
                return result;
            }

            // ── 第 0 段：與 DB 現值同步 ───────────────────────────────
            // callback（CallBackAPI → Update_oShuttleMapCode）可能已直接改寫 DB。
            // 其 MapCode 來自 AGV 到站時 RCS 明確回報，可信度高於輪詢，讓它優先。
            string accepted = _acceptedMapCode.ContainsKey(robotCode) ? _acceptedMapCode[robotCode] : null;
            if (!string.IsNullOrEmpty(dbMapCode) && dbMapCode != accepted)
            {
                accepted = dbMapCode;
                _acceptedMapCode[robotCode] = accepted;
                ClearPending(robotCode);
            }

            if (reports == null || reports.Count == 0)
            {
                result.AcceptedMapCode = accepted;
                result.PendingCount = GetPendingCount(robotCode);
                return result;
            }

            var lastPos = GetLastPositions(robotCode);

            // ── 第一段：同輪去重，挑出勝出筆 ──────────────────────────
            AGVStatusData winner;
            string reason;
            bool mapCodeTrusted = true;

            if (reports.Count == 1)
            {
                winner = reports[0];
                reason = "單筆回報";
            }
            else
            {
                result.HasConflict = true;

                var changed = reports.Where(r => IsPositionChanged(lastPos, r)).ToList();
                if (changed.Count == 1)
                {
                    winner = changed[0];
                    reason = "座標有變動";
                }
                else
                {
                    // 都變動或都凍結 → 無法由座標判別，依序黏著
                    string pending = GetPendingMapCode(robotCode);
                    string lastWinner = _lastWinnerMapCode.ContainsKey(robotCode) ? _lastWinnerMapCode[robotCode] : null;

                    winner = FindByMapCode(reports, pending);
                    reason = "黏著待確認值";

                    if (winner == null)
                    {
                        winner = FindByMapCode(reports, accepted);
                        reason = "黏著認可值";
                    }
                    if (winner == null)
                    {
                        winner = FindByMapCode(reports, lastWinner);
                        reason = "黏著上一輪勝出值";
                    }
                    if (winner == null)
                    {
                        // 待確認值 / 認可值 / 上一輪勝出值本輪皆未回報 → 無可信依據
                        // 保守處理：MapCode 維持不變更，也不推進確認計數。
                        // 風險：若此狀態長期持續，MapCode 會停留在舊值；WARN log 每輪皆記錄可供追查。
                        winner = reports[0];
                        reason = "無可信依據，MapCode 維持不變更";
                        mapCodeTrusted = false;
                    }
                }
            }

            // ── 第二段：MapCode 變更需連續確認 ────────────────────────
            if (mapCodeTrusted)
            {
                if (winner.mapCode == accepted)
                {
                    ClearPending(robotCode);
                }
                else
                {
                    string pending = GetPendingMapCode(robotCode);
                    if (pending == winner.mapCode)
                        _pendingCount[robotCode] = GetPendingCount(robotCode) + 1;
                    else
                    {
                        _pendingMapCode[robotCode] = winner.mapCode;
                        _pendingCount[robotCode] = 1;
                    }

                    if (GetPendingCount(robotCode) >= _confirmCount)
                    {
                        accepted = winner.mapCode;
                        _acceptedMapCode[robotCode] = accepted;
                        ClearPending(robotCode);
                        result.MapCodeAccepted = true;
                    }
                }

                _lastWinnerMapCode[robotCode] = winner.mapCode;
            }

            // 認可值尚未建立（首輪且 DB 為空）時，直接以勝出筆為準
            if (string.IsNullOrEmpty(accepted))
            {
                accepted = winner.mapCode;
                _acceptedMapCode[robotCode] = accepted;
            }

            // ── 更新座標基準（合併，不清掉本輪未回報的地圖）──────────
            foreach (var r in reports)
            {
                if (r == null || string.IsNullOrEmpty(r.mapCode)) continue;
                lastPos[r.mapCode] = PositionKey(r);
            }

            result.Winner = winner;
            result.AcceptedMapCode = accepted;
            result.PendingCount = GetPendingCount(robotCode);
            result.DbRow = BuildDbRow(reports, winner, accepted);

            if (result.HasConflict)
                result.ConflictDetail = BuildConflictDetail(robotCode, reports, lastPos, winner, reason, accepted);

            return result;
        }

        // ── 私有方法 ──────────────────────────────────────────────

        /// <summary>
        /// 組出要寫入 DB 的那一列。
        /// 優先取本輪回報中 mapCode 已等於認可值的那筆（欄位彼此一致）；
        /// 找不到才複製勝出筆並改寫 mapCode，避免污染 Winner 供診斷用的原始值。
        /// </summary>
        private static AGVStatusData BuildDbRow(IList<AGVStatusData> reports, AGVStatusData winner, string accepted)
        {
            var exact = FindByMapCode(reports, accepted);
            if (exact != null) return exact;

            var clone = Clone(winner);
            if (clone != null) clone.mapCode = accepted;
            return clone;
        }

        private static AGVStatusData Clone(AGVStatusData source)
        {
            if (source == null) return null;
            return new AGVStatusData
            {
                robotCode = source.robotCode,
                robotDir = source.robotDir,
                robotIp = source.robotIp,
                battery = source.battery,
                posX = source.posX,
                posY = source.posY,
                mapCode = source.mapCode,
                speed = source.speed,
                status = source.status,
                exclType = source.exclType,
                stop = source.stop,
                podCode = source.podCode,
                podDir = source.podDir,
                path = source.path,
            };
        }

        private static AGVStatusData FindByMapCode(IList<AGVStatusData> reports, string mapCode)
        {
            if (string.IsNullOrEmpty(mapCode)) return null;
            return reports.FirstOrDefault(r => r != null && r.mapCode == mapCode);
        }

        private static string PositionKey(AGVStatusData report)
        {
            return string.Format("{0}|{1}", report?.posX, report?.posY);
        }

        /// <summary>
        /// 該地圖的座標是否與上一輪不同。
        /// 上一輪沒有該地圖的紀錄 → 視為有變動（車輛「新出現」在某張圖是強訊號）。
        /// </summary>
        private static bool IsPositionChanged(Dictionary<string, string> lastPos, AGVStatusData report)
        {
            if (report == null || string.IsNullOrEmpty(report.mapCode)) return false;
            if (!lastPos.ContainsKey(report.mapCode)) return true;
            return lastPos[report.mapCode] != PositionKey(report);
        }

        private Dictionary<string, string> GetLastPositions(string robotCode)
        {
            if (!_lastPositions.ContainsKey(robotCode))
                _lastPositions[robotCode] = new Dictionary<string, string>();
            return _lastPositions[robotCode];
        }

        private string GetPendingMapCode(string robotCode)
        {
            return _pendingMapCode.ContainsKey(robotCode) ? _pendingMapCode[robotCode] : null;
        }

        private int GetPendingCount(string robotCode)
        {
            return _pendingCount.ContainsKey(robotCode) ? _pendingCount[robotCode] : 0;
        }

        private void ClearPending(string robotCode)
        {
            _pendingMapCode.Remove(robotCode);
            _pendingCount.Remove(robotCode);
        }

        private static string BuildConflictDetail(
            string robotCode,
            IList<AGVStatusData> reports,
            Dictionary<string, string> lastPos,
            AGVStatusData winner,
            string reason,
            string accepted)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("ShuttleId={0} 同輪被 {1} 張地圖回報：", robotCode, reports.Count);
            foreach (var r in reports)
            {
                if (r == null) continue;
                sb.AppendFormat("[{0} pos={1},{2} {3}]",
                    r.mapCode, r.posX, r.posY,
                    IsPositionChanged(lastPos, r) ? "座標有變動" : "座標凍結");
            }
            sb.AppendFormat(" → 勝出={0}（{1}），採用 MapCode={2}", winner?.mapCode, reason, accepted);
            return sb.ToString();
        }
    }
}
