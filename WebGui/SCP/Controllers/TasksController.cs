using Microsoft.AspNetCore.Mvc;
using SCP.Models;
using System.Globalization;
using System.Reflection;

namespace SCP.Controllers
{
    public class TasksController : Controller
    {
        private readonly agvDB_1400004Context _DBContext;
        private readonly ILogger<TasksController> _logger;
        public TasksController(agvDB_1400004Context DBContext, ILogger<TasksController> logger)
        {
            _DBContext = DBContext;
            _logger = logger;
        }
        public IActionResult Index()
        {
            try
            {
                // 車輛下拉選單改為從 oShuttle 動態載入，取代原本寫死的 AGV-1/AGV-2，
                // 讓二廠 3F/4F 新車也能被查詢。
                ViewBag.Shuttles = _DBContext.oShuttle.OrderBy(s => s.ShuttleId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "載入車輛下拉選單(oShuttle)失敗，下拉將只剩「請選擇」");
                ViewBag.Shuttles = new List<oShuttle>();
            }
            return View();
        }

        public IActionResult GetMission(string startDate, string endDate, int shuttleId, string shiftId)
        {
            try
            {
                var missions = GetSearchData(startDate, endDate, shuttleId, shiftId);
                ViewBag.Missions = missions
                    .GroupBy(item => item.Date)
                    .Select(group => new
                    {
                        Date = group.Key.ToString("MM/dd"),
                        ShuttleName = group.First().AGV,
                        DayShift = group.Where(i => i.ShiftName == "早班").Count().ToString(),
                        NightShift = group.Where(i => i.ShiftName == "晚班").Count().ToString(),
                        Total = group.Count().ToString()
                    })
                    .OrderBy(item => item.Date);

                return PartialView("_TaskDataPartial");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMission failed. startDate={StartDate}, endDate={EndDate}, shuttleId={ShuttleId}, shiftId={ShiftId}", startDate, endDate, shuttleId, shiftId);
                ViewBag.Missions = Enumerable.Empty<object>();
                return PartialView("_TaskDataPartial");
            }
        }

        public IActionResult GetBarChat(string startDate, string endDate, int shuttleId, string shiftId)
        {
            try
            {
                var missions = GetSearchData(startDate, endDate, shuttleId, shiftId);
                var results = missions
                    .GroupBy(item => new { item.Date, item.ShiftName })
                    .Select(group => new
                    {
                        Date = group.Key.Date,
                        ShiftName = group.Key.ShiftName,
                        Count = group.Count()
                    })
                    .OrderBy(item => item.Date);

                return Json(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBarChat failed. startDate={StartDate}, endDate={EndDate}, shuttleId={ShuttleId}, shiftId={ShiftId}", startDate, endDate, shuttleId, shiftId);
                return Json(Enumerable.Empty<object>());
            }
        }

        public IActionResult GetTasks(string startDate, string endDate, int shuttleId, string shiftId)
        {
            try
            {
                var missions = GetSearchData(startDate, endDate, shuttleId, shiftId);
                ViewBag.Tasks = missions
                    .Select(item => new
                    {
                        Date = item.Date.ToString("MM/dd"),
                        AGV = item.AGV,
                        ShiftName = item.ShiftName,
                        WorkOrder = item.WorkOrder,
                        BeginStation = item.BeginStation,
                        EndStation = item.EndStation,
                        BeginTime = item.BeginTime,
                        EndTime = item.EndTime,
                        TotalTime = item.TotalTime
                    })
                    .OrderBy(item => item.Date);

                return PartialView("_TaskDetialPartial");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTasks failed. startDate={StartDate}, endDate={EndDate}, shuttleId={ShuttleId}, shiftId={ShiftId}", startDate, endDate, shuttleId, shiftId);
                ViewBag.Tasks = Enumerable.Empty<object>();
                return PartialView("_TaskDetialPartial");
            }
        }

        private List<TaskReport> GetSearchData(string startDate, string endDate, int shuttleId ,string shiftId)
        {
            

            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture).AddDays(1);
            List<TaskReport> missionData = new List<TaskReport>();

            // 防呆：若選定車輛未登錄於 oShuttle（如二廠新車漏登錄/ID 對不上），
            // 原本 FirstOrDefault().GustomerName 會 NullReference 造成整支 API 回 500。
            // 改為安全取值，找不到時退回顯示「車輛{id}」，不阻斷查詢。
            var shuttle = _DBContext.oShuttle.FirstOrDefault(s => s.ShuttleId == shuttleId);
            string shuttleName = shuttleId == 0 ? "所有車輛" : (shuttle?.GustomerName ?? $"車輛{shuttleId}");

            var missions = _DBContext.ubMission
                .Where(item => item.BeginTime.Substring(0, 8).CompareTo(startDate) >= 0
                && item.BeginTime.Substring(0, 8).CompareTo(endDate) <= 0
                && (shuttleId == 0 || item.ShuttleId == shuttleId))
                .ToList();

            var shifts = _DBContext.pShift
                .ToList()// 將查詢結果拉取到內存中，以便進行後續的分組和排序操作
                .Where(s => DateTime.ParseExact(s.EffectDateTime, "yyyy-MM-dd", CultureInfo.InvariantCulture) < dtEnd
                && (s.ShiftCode == shiftId || string.IsNullOrEmpty(shiftId))) // 篩選出今天日期大於記錄中日期的值
                .Select(s => new
                {
                    s.ShiftName,
                    BeginDateTime = DateTime.ParseExact(s.BeginDateTime, "HH:mm", CultureInfo.InvariantCulture),
                    EndDateTime = DateTime.ParseExact(s.EndDateTime, "HH:mm", CultureInfo.InvariantCulture),
                    s.EffectDateTime
                })
                .GroupBy(s => s.ShiftName) // 按照班次類型進行分組
                .Select(g => g.OrderByDescending(s => s.EffectDateTime) // 在每個分組內按日期降序排列
                .First()) // 從每組中取出第一筆記錄
                .ToList();

            // 防呆：ubMission 的 BeginTime/EndTime 為可空字串，現場可能出現 null、空字串、
            // 長度不足 12 碼（未完成/取消任務）或格式異常的列。任一壞列若直接 Substring/ParseExact
            // 會整批拋例外造成 API 回 500，故先過濾長度、再用 TryParseExact 解析，解析失敗的列略過。
            var data = missions
               .Select(m =>
               {
                   if (string.IsNullOrEmpty(m.BeginTime) || m.BeginTime.Length < 12
                       || string.IsNullOrEmpty(m.EndTime) || m.EndTime.Length < 12)
                       return null;

                   if (!DateTime.TryParseExact(m.EndTime.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                       || !DateTime.TryParseExact(m.BeginTime.Substring(8, 4), "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var beginTime)
                       || !DateTime.TryParseExact(m.EndTime.Substring(8, 4), "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime))
                       return null;

                   return new
                   {
                       Date = date,
                       WorkOrder = m.WorkOrder,
                       BeginStation = m.BeginStation,
                       EndStation = m.EndStation,
                       BeginTime = beginTime,
                       EndTime = endTime,
                   };
               })
               .Where(x => x != null)
               .ToList();

            foreach (var shiftTime in shifts)
            {
                var result = data
                    .Where(m => shiftTime.BeginDateTime > shiftTime.EndDateTime
                                ? m.EndTime >= shiftTime.BeginDateTime || m.EndTime < shiftTime.EndDateTime
                                : m.EndTime >= shiftTime.BeginDateTime && m.EndTime < shiftTime.EndDateTime)
                    .Select(m => new TaskReport
                    {
                        Date = shiftTime.BeginDateTime > shiftTime.EndDateTime && m.EndTime < shiftTime.EndDateTime ? m.Date.AddDays(-1) : m.Date,
                        AGV = shuttleName,
                        ShiftName = shiftTime.ShiftName,
                        WorkOrder = m.WorkOrder,
                        BeginStation = m.BeginStation,
                        EndStation = m.EndStation,
                        BeginTime = m.BeginTime.ToString("HH:mm"),
                        EndTime = m.EndTime.ToString("HH:mm"),
                        TotalTime = m.BeginTime > m.EndTime ? (m.EndTime.AddDays(1) - m.BeginTime).TotalMinutes.ToString() : (m.EndTime - m.BeginTime).TotalMinutes.ToString()
                    })
                    .Where(m => m.Date >= dtStart && m.Date < dtEnd)
                    .OrderBy(m => m.Date)
                    .ToList();
                missionData.AddRange(result);
            }

            return missionData;
        }
    }
}
