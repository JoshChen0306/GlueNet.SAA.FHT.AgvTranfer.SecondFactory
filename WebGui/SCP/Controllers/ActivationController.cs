using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SCP.Models;
using System;
using System.Globalization;

namespace SCP.Controllers
{
    public class ActivationController : Controller
    {
        private readonly agvDB_1400004Context _DBContext;
        private readonly ILogger<ActivationController> _logger;

        private const string ActTimeFormat = "yyyyMMddHHmmssffffff";

        public ActivationController(agvDB_1400004Context DBContext, ILogger<ActivationController> logger)
        {
            _DBContext = DBContext;
            _logger = logger;
        }
        public IActionResult Index()
        {
            try
            {
                // 車輛下拉選單改為從 oShuttle 動態載入，取代寫死的 AGV-1/AGV-2。
                ViewBag.Shuttles = _DBContext.oShuttle.OrderBy(s => s.ShuttleId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "載入車輛下拉選單(oShuttle)失敗，下拉將只剩「請選擇」");
                ViewBag.Shuttles = new List<oShuttle>();
            }
            return View();
        }

        public IActionResult GetPieActivation(string startDate, string endDate, string shuttleId)
        {
            try
            {
                double totalHours = GetTotalHours(startDate, endDate);
                var data = GetSearchData(startDate, endDate, shuttleId);
                var result = GetPie(data, totalHours);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPieActivation failed. startDate={StartDate}, endDate={EndDate}, shuttleId={ShuttleId}", startDate, endDate, shuttleId);
                return Json(Enumerable.Empty<object>());
            }
        }
        public IActionResult GetBarActivation(string startDate, string endDate, string shuttleId)
        {
            try
            {
                double totalHours = GetTotalHours(startDate, endDate);
                var data = GetSearchData(startDate, endDate, shuttleId);
                var result = GetBar(data, totalHours);

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBarActivation failed. startDate={StartDate}, endDate={EndDate}, shuttleId={ShuttleId}", startDate, endDate, shuttleId);
                return Json(Enumerable.Empty<object>());
            }
        }

        public IActionResult GetTaskTable(string startDate, string endDate, string shuttleId)
        {
            try
            {
                var data = GetSearchData(startDate, endDate, shuttleId);
                double totalHours = GetTotalHours(startDate, endDate);

                ViewBag.Activation = GetPie(data, totalHours);
                ViewBag.TotalActivation = data
                   .Select(item => new
                   {
                       item.TaskType,
                       BeginTime = DateTime.ParseExact(item.BeginTime, ActTimeFormat, CultureInfo.InvariantCulture),
                       EndTime = DateTime.ParseExact(item.EndTime, ActTimeFormat, CultureInfo.InvariantCulture),
                   })
                   .GroupBy(item => new { item.TaskType })
                   .Select(group =>

                       Math.Round(Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2) / (totalHours) * 100, 2)
                   ).FirstOrDefault().ToString("0.00");
                return PartialView("_ActivationPartial");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTaskTable failed. startDate={StartDate}, endDate={EndDate}, shuttleId={ShuttleId}", startDate, endDate, shuttleId);
                ViewBag.Activation = new List<ActivationReport>();
                ViewBag.TotalActivation = "0.00";
                return PartialView("_ActivationPartial");
            }
        }

        public IActionResult GetDetailTable(string startDate, string endDate, string shuttleId)
        {
            try
            {
                var data = GetSearchData(startDate, endDate, shuttleId);
                double totalHours = GetTotalHours(startDate, endDate);

                ViewBag.Activation = GetBar(data, totalHours);
                return PartialView("_ActivationPartial");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDetailTable failed. startDate={StartDate}, endDate={EndDate}, shuttleId={ShuttleId}", startDate, endDate, shuttleId);
                ViewBag.Activation = new List<ActivationReport>();
                return PartialView("_ActivationPartial");
            }
        }

        private List<ubActivation> GetSearchData(string startDate, string endDate, string shuttleId)
        {
            (string beginTime, string endTime) = GetShiftTime(startDate,endDate);

            var data = _DBContext.ubActivation
                .Where(item => item.BeginTime.CompareTo(beginTime) >= 0
                && item.BeginTime.CompareTo(endTime) <= 0
                && (string.IsNullOrEmpty(shuttleId) || item.ShuttleId == shuttleId)
                && !string.IsNullOrEmpty(item.EndTime))
                .ToList()
                // 防呆：ubActivation 含 ShuttleId='AGV_001'/空字串等雜資料，BeginTime/EndTime
                // 可能非預期格式。先用 TryParseExact 濾掉解析不過的列，避免下游 GetPie/GetBar/
                // GetTaskTable 的 ParseExact 對單筆壞列拋例外造成整頁 500。
                .Where(item => DateTime.TryParseExact(item.BeginTime, ActTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                            && DateTime.TryParseExact(item.EndTime, ActTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                .ToList();


            return data;
        }

        private List<ActivationReport> GetPie(List<ubActivation> data, double totalHours)
        {
            var result = data
               .Select(item => new
               {
                   item.ShuttleId,
                   item.TaskType,
                   BeginTime = DateTime.ParseExact(item.BeginTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
                   EndTime = DateTime.ParseExact(item.EndTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
               })
               .GroupBy(item => new { item.ShuttleId })
               .Select(group => new ActivationReport
               {
                   ShuttleId = group.Key.ShuttleId,
                   Travling = Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Idle = Math.Round(totalHours - group.Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Charging = Math.Round(group.Where(item => item.TaskType == "C").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Alarm = Math.Round(group.Where(item => item.TaskType == "A").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Offline = Math.Round(group.Where(item => item.TaskType == "F").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   NonTraveling = Math.Round(totalHours - group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Activation = Math.Round(Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2)
                   / (totalHours - Math.Round(group.Where(item => item.TaskType == "C").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2)) * 100, 2).ToString("0.00")
               })               
               .OrderBy(item => item.ShuttleId)
               .ToList();
            
            return result;
        }

        private List<ActivationReport> GetBar(List<ubActivation> data, double totalHours)
        {
            var result = data
               .Select(item => new
               {
                   item.ShuttleId,
                   item.TaskType,
                   BeginTime = DateTime.ParseExact(item.BeginTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
                   EndTime = DateTime.ParseExact(item.EndTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
                   Date = DateTime.ParseExact(item.BeginTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture).AddHours(-8).AddMinutes(-30).Date
               })
               .GroupBy(item => new { item.ShuttleId, item.Date })
               .Select(group =>
               {
               double dailyTotalHours = group.Key.Date == DateTime.Today
                ? (DateTime.Now < group.Key.Date.AddHours(8).AddMinutes(30))
                    ? (DateTime.Now - group.Key.Date.AddDays(-1).AddHours(8).AddMinutes(30)).TotalHours
                    : (DateTime.Now - group.Key.Date.AddHours(8).AddMinutes(30)).TotalHours
                : 24.0;
                
                   return new ActivationReport
                   {
                       ShuttleId = group.Key.ShuttleId,
                       Date = group.Key.Date.ToString("MM/dd"),
                       Travling = Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                       Idle = Math.Round(dailyTotalHours - group.Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                       Charging = Math.Round(group.Where(item => item.TaskType == "C").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                       Alarm = Math.Round(group.Where(item => item.TaskType == "A").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                       Offline = Math.Round(group.Where(item => item.TaskType == "F").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                       NonTraveling = Math.Round(dailyTotalHours - group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                       Activation = Math.Round(Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2)
                       / (dailyTotalHours- Math.Round(group.Where(item => item.TaskType == "C").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2)) * 100, 2).ToString("0.00")
                   };
               })
               .OrderBy(item => item.Date)
               .ThenBy(item => item.ShuttleId)
               .ToList();

            return result;
        }

        private (string, string) GetShiftTime(string startDate , string endDate)
        {
            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            string beginTime = string.Empty;
            string endTime = string.Empty;
            string shiftTime = _DBContext.pShift.Where(item => item.ShiftName == "早班").Select(item => item.BeginDateTime).FirstOrDefault();

            if (DateTime.Now.TimeOfDay < TimeSpan.Parse(shiftTime))
            {
                beginTime = dtStart.AddDays(-1).ToString("yyyyMMdd") + shiftTime.Replace(":", "");
                endTime = dtEnd.ToString("yyyyMMdd") + shiftTime.Replace(":", "");
            }
            else
            {
                beginTime = dtStart.ToString("yyyyMMdd") + shiftTime.Replace(":", "");
                endTime = dtEnd.AddDays(+1).ToString("yyyyMMdd") + shiftTime.Replace(":", "");
            }


            return (beginTime, endTime);
        }
        
        private double GetTotalHours(string startDate, string endDate)
        {
            double totalHours;
            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture).AddHours(8).AddMinutes(30);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            if (dtEnd == DateTime.Now.Date)
            {
                if (DateTime.Now < dtStart)
                {
                    totalHours = Math.Round((DateTime.Now - dtStart.AddDays(-1)).TotalHours, 2);
                }
                else
                {
                    totalHours = Math.Round((DateTime.Now - dtStart).TotalHours, 2);
                }

            }
            else
            {
                totalHours = Math.Round((dtEnd.AddDays(1).AddHours(8).AddMinutes(30) - dtStart).TotalHours, 2);
            }

            return totalHours;
        }
    }
}
