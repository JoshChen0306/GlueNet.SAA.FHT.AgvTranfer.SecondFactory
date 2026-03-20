
using SCP.Commons;
using SCP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Data.Common;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Localization;
using SCP.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using System.Globalization;


namespace SCP.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly agvDB_1400004Context _DBContext;
        private readonly IStringLocalizer<HomeController> _localizer;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, agvDB_1400004Context DBContext , IStringLocalizer<HomeController> localizer ,IConfiguration configuration)
        {
            _logger = logger;
            _DBContext = DBContext;
            _localizer = localizer;
            _configuration = configuration;
        }
        [AllowAnonymous]
        public IActionResult Index(string returnUrl = null, string expiresUtc = null)
        {
            if (returnUrl == null)  
                returnUrl = _configuration.GetSection("MyConfig")["HomePage"];

            ViewBag.LogoutTime = expiresUtc;
            ViewBag.Functions = GetFunctions();
            ViewBag.iframe = $"<iframe name='myIframe' id='myIframe' width='100%' scrolling='no'  src='{returnUrl}' ></iframe>";
            return View();
        }     

        public IActionResult WarSituation()
        {
            GetTaskStatus();   
            return View();
        }

        public IActionResult Map()
        {
            return View();
        }
        public IActionResult UpdateAgvStatus()
        {
            GetTaskStatus();
            return PartialView("_TaskStatusPartial");
        }

        public IActionResult UpdateTotalTask()
        {
           
          (string beginTime,string endTime) = GetShiftTime();

            var result = _DBContext.ubMission.Count(item=> item.EndTime.CompareTo(beginTime) >=0 && item.EndTime.CompareTo(endTime) <= 0);
            return Json(result);
        }

        public IActionResult UpdateAlarm()
        {
            (string beginTime, string endTime) = GetShiftTime();

            var result = _DBContext.ubActivation.Count(item => item.EndTime.CompareTo(beginTime) >= 0 && item.EndTime.CompareTo(endTime) <= 0 && item.TaskType =="A" && !string.IsNullOrEmpty(item.EndTime));
            return Json(result);
        }

        public IActionResult ChangeLanguage(string lang, string returnUrl)
        {   

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

            return Redirect(returnUrl);
        }

        public IActionResult GetAgvActivation()
        {

            (string beginTime, string endTime) = GetShiftTime();

            var data = _DBContext.ubActivation
                .Where(item => item.BeginTime.CompareTo(beginTime) >= 0 && item.BeginTime.CompareTo(endTime) <= 0 && !(string.IsNullOrEmpty(item.EndTime)))
                .Select(item => new
                {
                    item.TaskType,
                    BeginDateTime = DateTime.ParseExact(item.BeginTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
                    EndDateTime = DateTime.ParseExact(item.EndTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),

                }).ToList()
                .GroupBy(item => item.TaskType)
                .Select(group => new
                {
                    GroupID = group.Key,
                    TimeDifference = Math.Round(group.Sum(item => (item.EndDateTime - item.BeginDateTime).TotalHours),2)
                });

            return Json(data);
        }
        public IActionResult GetMission()
        {
            DateTime today = DateTime.Today;
            string endTime = today.ToString("yyyyMMdd");
            string startTime = today.AddDays(-7).ToString("yyyyMMdd");
            List<object> data = new List<object>();

            var shifts = _DBContext.pShift
                .ToList()// 將查詢結果拉取到內存中，以便進行後續的分組和排序操作
                .Where(s => DateTime.ParseExact(s.EffectDateTime, "yyyy-MM-dd", CultureInfo.InvariantCulture) < today) // 篩選出今天日期大於記錄中日期的值
                .Select(s => new {
                    s.ShiftName,
                    BeginDateTime = DateTime.ParseExact(s.BeginDateTime, "HH:mm", CultureInfo.InvariantCulture),
                    EndDateTime = DateTime.ParseExact(s.EndDateTime, "HH:mm", CultureInfo.InvariantCulture),
                    s.EffectDateTime
                })
                .GroupBy(s => s.ShiftName) // 按照班次類型進行分組
                .Select(g => g.OrderByDescending(s => s.EffectDateTime) // 在每個分組內按日期降序排列
                .First()) // 從每組中取出第一筆記錄
                .ToList();

            var missions = _DBContext.ubMission
                .Where(m => m.EndTime.Substring(0, 8).CompareTo(startTime) >= 0 && m.EndTime.Substring(0, 8).CompareTo(endTime) <= 0)
                .ToList()
                .Select(m => new
                {
                    Date = DateTime.ParseExact(m.EndTime.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture),
                    endTime = DateTime.ParseExact(m.EndTime.Substring(8, 4), "HHmm", CultureInfo.InvariantCulture)
                })
                .ToList();

            foreach (var shiftTime in shifts)
            {
                var result = missions
                    .Where(m => shiftTime.BeginDateTime > shiftTime.EndDateTime
                                ? m.endTime >= shiftTime.BeginDateTime || m.endTime < shiftTime.EndDateTime
                                : m.endTime >= shiftTime.BeginDateTime && m.endTime < shiftTime.EndDateTime)
                    .Select(m => new {
                        Date = shiftTime.BeginDateTime > shiftTime.EndDateTime && m.endTime < shiftTime.EndDateTime ? m.Date.AddDays(-1) : m.Date,
                        shiftName = shiftTime.ShiftName,
                        end = m.endTime
                    })
                    .Where(m => m.Date >= today.AddDays(-7) && m.Date < today)
                    .GroupBy(m => m.Date)
                    .Select(m => new
                    {
                        Date = m.Key,
                        ShiftName = shiftTime.ShiftName,
                        Count = m.Count()
                    })
                    .OrderBy(m => m.Date)
                    .ToList();
                data.AddRange(result);
            }

            return Json(data);
        }


        

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        private List<pFunction> GetFunctions()
        {
            return _DBContext.pFunction.ToList();
        }

        private (string,string) GetShiftTime()
        {
            DateTime nowDate = DateTime.Now.Date;
            string beginTime = string.Empty;
            string endTime = string.Empty;
            string shiftTime = _DBContext.pShift.Where(item => item.ShiftName == "早班").Select(item => item.BeginDateTime).FirstOrDefault();

            if (DateTime.Now.TimeOfDay < TimeSpan.Parse(shiftTime)) 
            {
                beginTime = nowDate.AddDays(-1).ToString("yyyyMMdd")+ shiftTime.Replace(":", "");
                endTime = nowDate.ToString("yyyyMMdd")+ shiftTime.Replace(":", "");
            }
            else
            {
                beginTime = nowDate.ToString("yyyyMMdd") + shiftTime.Replace(":", "");
                endTime = nowDate.AddDays(+1).ToString("yyyyMMdd") + shiftTime.Replace(":", "");
            }

           
            return (beginTime, endTime);
        }

        private void GetTaskStatus()
        {
            Dictionary<string, (string Color, string Description)> Status = new Dictionary<string, (string Color, string Description)>
            {
                {"R",("text-info","運行") },
                {"I",("text-green","閒置") },
                {"C",("text-warning","充電") },
                {"A",("text-danger","異常") },
                {"F",("text-secondary","離線") },
            };

            // 系統任務類型的顯示覆寫（優先於一般運行狀態）
            Dictionary<string, (string Color, string Description)> TaskSourceDisplay = new Dictionary<string, (string Color, string Description)>
            {
                {"IDLE_RETURN",        ("text-warning", "歸位中") },
                {"CROSS_FLOOR_DISPATCH",("text-primary", "預調度") },
            };

            // 取得目前執行中任務的 TaskSource（依車號索引）
            var runningTaskSources = _DBContext.oMission
                .Where(m => m.OkFlag == "R" && m.ShuttleId != null)
                .Select(m => new { m.ShuttleId, m.TaskSource })
                .ToList()
                .GroupBy(m => m.ShuttleId!.Value)
                .ToDictionary(g => g.Key, g => g.First().TaskSource ?? "");

            ViewBag.TaskStatus = _DBContext.oShuttle
                .ToList()
                .Select(s =>
                {
                    var taskSource = runningTaskSources.ContainsKey(s.ShuttleId) ? runningTaskSources[s.ShuttleId] : "";
                    bool hasOverride = TaskSourceDisplay.ContainsKey(taskSource);
                    return new
                    {
                        s.ShuttleId,
                        s.GustomerName,
                        s.Battery,
                        BatteryColor = s.Battery < 50 && s.Battery > 30 ? "yellow" : s.Battery <= 30 ? "red" : "",
                        Color       = hasOverride ? TaskSourceDisplay[taskSource].Color       : Status[s.Status].Color,
                        Description = hasOverride ? TaskSourceDisplay[taskSource].Description : Status[s.Status].Description,
                        s.LastStation,
                        s.BeginStation,
                        s.EndStation
                    };
                });
        }

    }
}