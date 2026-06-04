using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SCP.Models;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SCP.Controllers
{
    public class DispatchController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly agvDB_1400004Context _DBContext;
        public DispatchController(IConfiguration configuration, agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
            _configuration = configuration;

        }
        [Authorize]
        public IActionResult Index()
        {
            var areas = _configuration.GetSection("Area").Get<Dictionary<string, string>>();
            var areaList = new List<SelectListItem>();
            IEnumerable<KeyValuePair<string, string>> filterAreas = areas;
            string groupId = User.FindFirst(ClaimTypes.Role)?.Value;
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // 使用路線權限過濾區域（新機制）
            var userRoutes = _DBContext.pUserRoute
                .Where(ur => ur.UserId == userId)
                .Join(_DBContext.pRoute.Where(r => r.ControlFlag == "Y"),
                      ur => ur.RouteId,
                      r => r.RouteId,
                      (ur, r) => r)
                .ToList();

            // 儲存允許的起點區域（用於過濾派送起點）
            List<string> allowedSourceAreas = new List<string>();
            // 儲存允許的所有區域（包含起點和終點，用於樓層過濾）
            List<string> allowedAllAreas = new List<string>();

            if (userRoutes.Any())
            {
                // 使用者有設定路線權限，使用新機制
                // 只取得「一般派送」(DISPATCH) 路線的起點區域作為可選派送區域
                // RELEASE 類型路線不顯示在派送頁面
                allowedSourceAreas = userRoutes
                    .Where(r => !string.IsNullOrEmpty(r.SourceAreas) && 
                                (r.DispatchMode == "DISPATCH" || string.IsNullOrEmpty(r.DispatchMode)))
                    .SelectMany(r => r.SourceAreas.Split(',').Select(a => a.Trim()))
                    .Distinct()
                    .ToList();

                // 取得所有路線的起點區域，用於樓層過濾
                // 注意：只使用 SourceAreas，不包含 TargetAreas
                // 這樣使用者只能看到他可以操作的樓層，而不是所有相關樓層
                allowedAllAreas = userRoutes
                    .Where(r => !string.IsNullOrEmpty(r.SourceAreas))
                    .SelectMany(r => r.SourceAreas.Split(',').Select(a => a.Trim()))
                    .Distinct()
                    .ToList();

                // 只使用「一般派送」起點區域過濾派送區域選單
                filterAreas = areas.Where(item => allowedSourceAreas.Contains(item.Value));
            }
            else
            {
                // 使用者無路線權限設定，使用舊的 switch-case 邏輯（相容舊資料）
                switch (groupId)
                {
                    case "2":
                        filterAreas = areas.Where(item => item.Value == "A");
                        break;
                    case "6":
                        filterAreas = areas.Where(item => item.Value == "C" || item.Value == "D");
                        break;
                    case "5":
                        filterAreas = areas.Where(item => item.Value == "F");
                        break;
                }
                // 舊機制：所有區域都是允許的
                allowedAllAreas = filterAreas.Select(a => a.Value).ToList();
            }

            foreach (var item in filterAreas)
            {
                areaList.Add(new SelectListItem { Value = item.Value, Text = item.Key });
            }

            ViewBag.AreaList = areaList;
            ViewBag.Site = _DBContext.oPort.Where(p => p.UseFlag == "Y").Select(p => new SelectListItem { Value = p.StationNo, Text = p.MachineName });
            ViewBag.Role = groupId;

            // 樓層選擇器 - 從 FloorArea 設定動態讀取
            var floorArea = _configuration.GetSection("FloorArea").Get<Dictionary<string, string[]>>();
            
            // 根據路線權限過濾樓層（只使用起點區域）
            var allowedAreasSet = allowedAllAreas.Any() ? allowedAllAreas.ToHashSet() : filterAreas.Select(a => a.Value).ToHashSet();
            var filteredFloorArea = new Dictionary<string, string[]>();
            var filteredFloorList = new List<SelectListItem>();

            foreach (var floor in floorArea)
            {
                // 檢查該樓層的區域是否有使用者可用的區域
                var matchedAreas = floor.Value.Where(a => allowedAreasSet.Contains(a)).ToArray();
                if (matchedAreas.Any())
                {
                    filteredFloorArea[floor.Key] = matchedAreas;
                    filteredFloorList.Add(new SelectListItem { Value = floor.Key, Text = floor.Key });
                }
            }

            ViewBag.FloorList = filteredFloorList;
            ViewBag.FloorArea = filteredFloorArea;
            
            // 設定預設樓層（使用者可用的第一個樓層）
            ViewBag.DefaultFloor = filteredFloorList.FirstOrDefault()?.Value ?? "";

            // 傳遞路線資訊給前端（用於驗證完整路線）
            ViewBag.UserRoutes = userRoutes.Select(r => new {
                r.RouteId,
                r.RouteName,
                r.SourceAreas,
                r.TargetAreas
            }).ToList();
            
            // 傳遞路線 ID 清單（用於前端權限過濾，如 V Cut 物料權限）
            ViewBag.UserRouteIds = userRoutes.Select(r => r.RouteId).ToList();

            // 物料登記畫面是否顯示「貨架條碼」欄位（預設 true 顯示；關閉時前端隱藏欄位、送出固定帶 -1 哨兵值通過後端必填驗證）
            ViewBag.ShowRackIdField = _configuration.GetValue("MyConfig:ShowRackIdField", true);

            return View();
        }

        public IActionResult UpdateDispatch()
        {
            ViewBag.Dispatchs = _DBContext.oRequire
                .ToList()
                .Select(item => new
                {
                    item.TaskDateTime,
                    item.BeginStation,
                    item.EndStation,
                    WorkOrder = item.WorkOrder.Split('^').Length > 3 ? item.WorkOrder.Split('^')[3] : string.Empty,
                    TaskType = item.TaskSource == "CROSS_FLOOR_DISPATCH" ? "跨樓層預調度" : item.TaskSource == "IDLE_RETURN" ? "歸位" : "一般搬運",
                    Status = item.AssignFlag == "Y" && item.OkFlag == "R" ? "執行中" : item.AssignFlag == "Y" ? "已派車" : item.AssignFlag == "C" ? "取消" : "異常",
                    TextColor = item.AssignFlag == "Y" && item.OkFlag == "R" ? "text-primary" : item.AssignFlag == "Y" ? "text-success" : item.AssignFlag == "C" ? "text-secondary" : "text-danger"
                });
            ViewBag.Role = User.FindFirst(ClaimTypes.Role)?.Value;
            return PartialView("_DispatchPartial");
        }

        [HttpGet]
        public IActionResult GetPortBindingList()
        {
            var list = _DBContext.oPortBinding
                .Where(b => b.UseFlag == "Y")
                .Select(b => b.LoadingPort)
                .ToList();
            return Ok(list);
        }

        public IActionResult InsertoNeed([FromBody] Dictionary<string, string> need)
        {

            string area = need["Area"];
            string objStation = need["BegingStation"];
            string endStation = need["EndStation"];
            string rackId = need["RackId"];
            string workOrder = need["WorkOrder"];
            string btnName = need["btnName"];
            string status = need["Status"];
            string assignFlag = (area == "C" && btnName == "ConfirmButton") ? "W" : (area == "C" && btnName == "ChangeButton") ? "R" : "";

            if (btnName == "RejectdButton")
            {
                workOrder = (status == "1") ? "" : _DBContext.oPort.Where(p => p.StationNo == need["EndStation"]).FirstOrDefault()?.WorkOrder?.ToString() ?? "";
                endStation = _DBContext.oPort.Where(p => p.Block == "B" && p.UseFlag == "Y" && p.HaveFlag == "0" && (p.BgnToEnd == "" || p.BgnToEnd == null)).FirstOrDefault()?.StationNo.ToString() ?? "";
                objStation = need["EndStation"];
            }
            if (string.IsNullOrEmpty(endStation)) return BadRequest(new { message = "暫存區無空架" });

            // ★★★ 空平板自動回收卡控：檢查目的地是否有 oPortBinding 綁定 ★★★
            var binding = _DBContext.oPortBinding
                .FirstOrDefault(b => b.LoadingPort == endStation && b.UseFlag == "Y");

            if (binding != null)
            {
                var loadingPort = _DBContext.oPort.FirstOrDefault(p => p.StationNo == endStation);
                var unloadingPort = _DBContext.oPort.FirstOrDefault(p => p.StationNo == binding.UnloadingPort);

                if (loadingPort == null || unloadingPort == null)
                {
                    return BadRequest(new { message = $"上料區 {endStation} 或下料區 {binding.UnloadingPort} 站點不存在，請至「上下料區綁定設定」頁面檢查設定是否正確" });
                }

                string loadingHaveFlag = loadingPort.HaveFlag ?? "";
                string unloadingHaveFlag = unloadingPort.HaveFlag ?? "";

                if (loadingHaveFlag == "1")
                {
                    // 上料區有空平板，需搬走
                    if (unloadingHaveFlag == "1")
                    {
                        // 下料區已有空平板，O/P 空平板送 FallbackAreas
                        string fallbackSlot = FindFallbackEmptySlot(binding.FallbackAreas, endStation);
                        if (string.IsNullOrEmpty(fallbackSlot))
                        {
                            return BadRequest(new { message = $"上料區 {endStation} 有空平板需回收，但回收候補區域（{binding.FallbackAreas}）皆無空位，請先清理候補區域的貨架" });
                        }
                        // FallbackAreas 有空位，放行（後端 svrPair 自動處理回收）
                    }
                    else if (unloadingHaveFlag == "0")
                    {
                        // 下料區是空架，空平板可直接送過去（最理想），放行
                    }
                    else
                    {
                        // 下料區有料盤（HaveFlag == 3 等），阻擋
                        return BadRequest(new { message = $"下料區 {binding.UnloadingPort} 目前有料盤（HaveFlag=3），請先將料盤取走或完成下料" });
                    }
                }
                else
                {
                    // 上料區無空平板
                    if (unloadingHaveFlag != "1")
                    {
                        // 上料區和下料區都沒空平板
                        return BadRequest(new { message = $"上料區 {endStation} 及下料區 {binding.UnloadingPort} 皆無空平板，請先於上料區或下料區人工放置空平板" });
                    }
                    // 下料區有空平板，直接放行
                }
            }

            string sql = "INSERT INTO oNeed (ObjStation,RackId,WorkOrder,EndStation,TaskSource,TaskDateTime,AssignFlag) VALUES({0},{1},{2},{3},{4},{5},{6})";
            try
            {
                _DBContext.Database.ExecuteSqlRaw(sql, objStation, rackId, workOrder, endStation, "Web", DateTime.Now.ToString("yyyyMMddHHmmssffffff"), assignFlag);
            }
            catch (Exception ex)
            {

            }

            return Ok();
        }

        public IActionResult UpdateoPort([FromBody] Dictionary<string, string> need)
        {

            string objStation = need["BegingStation"];

            try
            {
                _DBContext.oPort
                    .Where(p => p.StationNo == objStation)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.WorkOrder, "")
                        .SetProperty(p => p.HaveFlag, "1"));
            }
            catch (Exception ex)
            {

            }

            return Ok();
        }

        public IActionResult DeleteoNeed([FromBody] Dictionary<string, string> need)
        {
            string taskDateTime = need["taskDateTime"];

            try
            {
                using var transaction = _DBContext.Database.BeginTransaction();

                // 取得被取消任務的父關聯：先查 oMission，找不到再退查 oRequire。
                // 空平板回收 O→Q 顯示於 oRequire，其 ParentTaskDateTime 指向卡控中的 M→O 源頭 oNeed；
                // 跨樓層預調度則記在 oMission。兩者皆以 ParentTaskDateTime 連動取消。
                var targetMission = _DBContext.oMission
                    .FirstOrDefault(m => m.TaskDateTime == taskDateTime);
                string? parentTdt = targetMission?.ParentTaskDateTime;
                if (string.IsNullOrEmpty(parentTdt))
                {
                    parentTdt = _DBContext.oRequire
                        .Where(r => r.TaskDateTime == taskDateTime)
                        .Select(r => r.ParentTaskDateTime)
                        .FirstOrDefault();
                }

                // 收集所有要取消的 TaskDateTime（含連動）
                var cancelList = new List<string> { taskDateTime };

                if (!string.IsNullOrEmpty(parentTdt))
                {
                    // 被取消的是子任務（跨樓層預調度 / 空平板回收 O→Q）→ 連動取消其父任務（父先取消）
                    cancelList.Insert(0, parentTdt);
                }

                // 查詢以此任務為 Parent 的子任務（被取消的是父任務 → 連動取消子任務）
                var childTasks = _DBContext.oMission
                    .Where(m => m.ParentTaskDateTime == taskDateTime)
                    .Select(m => m.TaskDateTime)
                    .ToList();
                foreach (var childTDT in childTasks)
                {
                    if (!string.IsNullOrEmpty(childTDT) && !cancelList.Contains(childTDT))
                        cancelList.Add(childTDT);
                }

                // 依序取消所有關聯任務（父任務優先）
                foreach (var tdt in cancelList)
                {
                    _DBContext.oMission
                        .Where(m => m.TaskDateTime == tdt)
                        .ExecuteUpdate(setters => setters.SetProperty(m => m.OkFlag, "C"));

                    _DBContext.oRequire
                        .Where(r => r.TaskDateTime == tdt)
                        .ExecuteUpdate(setters => setters.SetProperty(r => r.OkFlag, "C"));

                    // ★ 清源頭：卡控中的 M→O 物料任務只存在於 oNeed（未轉 oRequire/oMission），
                    //   標 AssignFlag='C' 交由 svrPair RecyclingoNeedByAssignFlag 刪除，
                    //   斷掉「取消後主迴圈重掃 oNeed 又生空平板回收任務」的源頭。
                    _DBContext.oNeed
                        .Where(n => n.TaskDateTime == tdt)
                        .ExecuteUpdate(setters => setters.SetProperty(n => n.AssignFlag, "C"));
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteoNeed] 取消任務失敗：TaskDateTime={taskDateTime}, Error={ex.Message}");
            }
            return Ok();
        }

        public IActionResult ReLogin([FromBody] Dictionary<string, string> need)
        {
            var userId = need["userId"];
            var password = need["password"];

            var result = _DBContext.pUser.Where(u => u.UserId == userId && u.Password == password).FirstOrDefault()?.GroupId?.ToString() ?? "";
            if (result == "1" || result == "7")
            {
                return Ok();
            }
            else
            {
                return BadRequest();
            }

        }
        public IActionResult GetoNeed()
        {
            var result = _DBContext.oNeed;
            return Json(result);
        }

        /// <summary>
        /// 取得所有站點的最新資料（供前端即時查詢使用）
        /// </summary>
        [HttpGet]
        public IActionResult GetAllStations()
        {
            try
            {
                // 取得已有待處理任務的起點站，避免重複派送
                // 1. oNeed 中待處理的任務（AssignFlag 為空）
                var pendingFromONeed = _DBContext.oNeed
                    .Where(n => n.AssignFlag == null || n.AssignFlag == "")
                    .Select(n => n.ObjStation)
                    .ToList();

                // 2. oRequire 中進行中的任務（OkFlag 為空，表示尚未完成）
                var pendingFromORequire = _DBContext.oRequire
                    .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                    .Select(r => r.BeginStation)
                    .ToList();

                // 3. oMission 中進行中的任務（OkFlag 為空或 R=執行中）
                var pendingFromOMission = _DBContext.oMission
                    .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                    .Select(m => m.BeginStation)
                    .ToList();

                // 合併所有進行中任務的起點站
                var allPendingBeginStations = pendingFromONeed
                    .Concat(pendingFromORequire)
                    .Concat(pendingFromOMission)
                    .Distinct()
                    .ToList();

                // 取得已有待處理任務的終點站，避免重複指派
                var pendingEndFromONeed = _DBContext.oNeed
                    .Where(n => n.AssignFlag == null || n.AssignFlag == "")
                    .Select(n => n.EndStation)
                    .ToList();

                var pendingEndFromORequire = _DBContext.oRequire
                    .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                    .Select(r => r.EndStation)
                    .ToList();

                var pendingEndFromOMission = _DBContext.oMission
                    .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                    .Select(m => m.EndStation)
                    .ToList();

                // 合併所有進行中任務的終點站
                var allPendingEndStations = pendingEndFromONeed
                    .Concat(pendingEndFromORequire)
                    .Concat(pendingEndFromOMission)
                    .Distinct()
                    .ToList();

                // 合併所有不可選的站點（起點 + 終點）
                var allExcludedStations = allPendingBeginStations
                    .Concat(allPendingEndStations)
                    .Distinct()
                    .ToList();

                var stations = _DBContext.oPort
                    .Where(p => p.UseFlag == "Y" &&
                                !allExcludedStations.Contains(p.StationNo))  // 排除所有進行中任務的站點（起點和終點）
                    .Select(p => new
                    {
                        p.Area,
                        p.Port,
                        p.BgnToEnd,
                        Reserve = string.IsNullOrEmpty(p.BgnToEnd) ? "N" : "Y",  // 轉換 BgnToEnd 為 Reserve
                        p.StationNo,
                        p.Block,
                        p.HaveFlag,
                        p.WorkOrder,
                        p.RackId,
                        p.PutTime,
                        p.MachineName,
                        p.InterfaceName
                    })
                    .ToList();

                return Json(stations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "取得站點資料失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 登記帳料 - 在空架站點登記工單和貨架資訊
        /// </summary>
        [HttpPost]
        public IActionResult RegisterLot([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";
                string workOrder = data.ContainsKey("workOrder") ? data["workOrder"] : "";
                string rackId = data.ContainsKey("rackId") ? data["rackId"] : "";
                string isVcutMaterial = data.ContainsKey("isVcutMaterial") ? data["isVcutMaterial"] : "false";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 驗證：工單必填（R 區例外，工單選填）
                var stationArea = stationNo.Substring(0, 1).ToUpper();
                if (string.IsNullOrEmpty(workOrder) && stationArea != "R")
                {
                    return BadRequest(new { message = "請輸入工單條碼" });
                }

                // 驗證：J 區（3F 插針室）、H 區（2F 成型後）、I 區（3F 品檢區）、K 區（4F 烘烤前）、L 區（4F 烘烤後）RackId 必填
                if ((stationArea == "J" || stationArea == "H" || stationArea == "I" || stationArea == "K" || stationArea == "L") && string.IsNullOrEmpty(rackId))
                {
                    return BadRequest(new { message = "請輸入貨架條碼" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查該站點是否為待處理任務的終點（避免與 Release 回送任務衝突）
                var pendingAsEndTask = _DBContext.oNeed
                    .FirstOrDefault(n => n.EndStation == stationNo &&
                                         (n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y"));
                if (pendingAsEndTask != null)
                {
                    return BadRequest(new { message = $"此站點有待處理的回送任務（來自 {pendingAsEndTask.ObjStation}），請等待任務完成後再登記" });
                }

                // 檢查該站點是否為進行中任務的起點（不能修改有任務的站點物料）
                var hasPendingAsBegin =
                    _DBContext.oNeed.Any(n => n.ObjStation == stationNo && (n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")) ||
                    _DBContext.oRequire.Any(r => r.BeginStation == stationNo && (r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")) ||
                    _DBContext.oMission.Any(m => m.BeginStation == stationNo && (m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R"));

                if (hasPendingAsBegin)
                {
                    return BadRequest(new { message = "此站點有進行中的派送任務，無法修改物料資訊" });
                }

                // 檢查站點狀態（僅記錄，不阻擋）
                if (port.HaveFlag != "0")
                {
                    // 站點不是空架，但仍允許覆蓋登記
                    // 可在此處記錄日誌
                }



                // 處理 V Cut 標記
                // 先移除現有的 ^VCUT 和 ^DONE 標記（如果有的話）
                workOrder = workOrder.Replace("^VCUT^DONE", "").Replace("^VCUT", "").Replace("^DONE", "");
                // 同時移除 ^NG 和 ^RETURN 標記（避免重複）
                workOrder = workOrder.Replace("^NG", "").Replace("^RETURN", "");

                // 判斷站點區域（stationArea 已在前面宣告）

                if (stationArea == "T")
                {
                    // T 區（V Cut區）建立物料時，自動標記為已加工完成
                    workOrder = workOrder + "^VCUT^DONE";
                }
                else if (stationArea == "M" && isVcutMaterial == "true")
                {
                    // M 區（雷雕區）勾選 V Cut 專用時，附加 ^VCUT 標記
                    workOrder = workOrder + "^VCUT";
                }
                else if (stationArea == "I")
                {
                    // I 區（3F 品檢區）建立物料時，自動標記為 NG 回送
                    // 表示品檢失敗，需送回 4F 烘烤後再加工
                    workOrder = workOrder + "^NG";
                }

                // 更新 oPort 表
                var putTimeStr = DateTime.Now.ToString("yyyyMMddHHmmssffffff");
                _DBContext.oPort
                    .Where(p => p.StationNo == stationNo)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.HaveFlag, "3")           // 設為料盤
                        .SetProperty(p => p.WorkOrder, workOrder)     // 工單資訊
                        .SetProperty(p => p.RackId, rackId)           // 貨架編號
                        .SetProperty(p => p.PutTime, putTimeStr));    // 放置時間

                return Ok(new { message = "登記成功", stationNo = stationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "登記失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 清除物料 - 將站點設為空架，清除工單和貨架資訊
        /// </summary>
        [HttpPost]
        public IActionResult ClearLot([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查該站點是否有進行中的派送任務（不能清除有任務的站點）
                var hasPendingTask =
                    _DBContext.oNeed.Any(n => n.ObjStation == stationNo && (n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")) ||
                    _DBContext.oRequire.Any(r => r.BeginStation == stationNo && (r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")) ||
                    _DBContext.oMission.Any(m => m.BeginStation == stationNo && (m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R"));

                if (hasPendingTask)
                {
                    return BadRequest(new { message = "此站點有進行中的派送任務，無法清除物料" });
                }

                // 更新 oPort 表 - 清除物料資訊
                _DBContext.oPort
                    .Where(p => p.StationNo == stationNo)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.HaveFlag, "1")      // 設為空板：清除物料時空平板留置原位，標 1 以免自動回送往此格疊板（RackId 仍清空）
                        .SetProperty(p => p.WorkOrder, "")      // 清除工單
                        .SetProperty(p => p.RackId, "")         // 清除貨架
                        .SetProperty(p => p.PutTime, ""));      // 清除放置時間

                return Ok(new { message = "清除成功", stationNo = stationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "清除失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 標記空板 - 將站點從料盤(3)改為空板(1)，清除工單資訊
        /// 適用於 O/P/S/N 區
        /// </summary>
        [HttpPost]
        public IActionResult MarkEmptyTray([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查站點狀態是否為料盤
                if (port.HaveFlag != "3")
                {
                    return BadRequest(new { message = "站點狀態不是料盤，無法標記為空板" });
                }

                // 更新 oPort 表 - 標記為空板（保留 RackId）
                _DBContext.oPort
                    .Where(p => p.StationNo == stationNo)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.HaveFlag, "1")      // 設為空板
                        .SetProperty(p => p.WorkOrder, ""));    // 清除工單（保留 RackId）

                return Ok(new { message = "標記成功", stationNo = stationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "標記失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// Release - 將空板回送到指定區域
        /// O/P/S/N 區 → M 區（雷雕區）→ Q 區（出貨區）→ R 區
        /// EE 區（電梯暫存區）→ J 區（3F 插針室）
        /// </summary>
        [HttpPost]
        public IActionResult Release([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查站點狀態是否為空板
                if (port.HaveFlag != "1")
                {
                    return BadRequest(new { message = "請先標記為空板" });
                }

                // 檢查是否已有待處理的派送任務（防止重複派送）
                var existingTask = _DBContext.oNeed
                    .FirstOrDefault(n => n.ObjStation == stationNo &&
                                         (n.AssignFlag == null || n.AssignFlag == ""));
                if (existingTask != null)
                {
                    return BadRequest(new { message = "此站點已有待處理的派送任務，終點：" + existingTask.EndStation });
                }

                // 取得已有待處理任務的終點站，避免重複指派到同一位置
                var pendingEndStations = _DBContext.oNeed
                    .Where(n => n.AssignFlag == null || n.AssignFlag == "")
                    .Select(n => n.EndStation)
                    .ToList();

                // 判斷起點區域，決定回送目的地
                var stationArea = stationNo.Substring(0, 1).ToUpper();
                oPort emptySlot = null;

                if (stationArea == "G")
                {
                    // G 區（電梯暫存區）→ 回送到 J 區（3F 插針室）
                    emptySlot = _DBContext.oPort
                        .Where(p => p.Block == "J" &&
                                    p.HaveFlag == "0" &&
                                    (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                    p.UseFlag == "Y" &&
                                    !pendingEndStations.Contains(p.StationNo))
                        .OrderByDescending(p => p.Priority)
                        .ThenBy(p => p.Port)
                        .FirstOrDefault();

                    if (emptySlot == null)
                    {
                        return BadRequest(new { message = "3F 插針室 (J區) 沒有可放置的空位" });
                    }
                }
                else if (stationArea == "K")
                {
                    // K 區（4F烘烤前入貨區）→ 回送到 H 區（2F成型後）
                    emptySlot = _DBContext.oPort
                        .Where(p => p.Block == "H" &&
                                    p.HaveFlag == "0" &&
                                    (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                    p.UseFlag == "Y" &&
                                    !pendingEndStations.Contains(p.StationNo))
                        .OrderByDescending(p => p.Priority)
                        .ThenBy(p => p.Port)
                        .FirstOrDefault();

                    if (emptySlot == null)
                    {
                        return BadRequest(new { message = "2F 成型後 (H區) 沒有可放置的空位" });
                    }
                }
                else if (stationArea == "I")
                {
                    // I 區（3F品檢區）→ 回送到 L 區（4F烘烤後）L1-L4
                    emptySlot = _DBContext.oPort
                        .Where(p => p.Block == "L" &&
                                    p.HaveFlag == "0" &&
                                    (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                    p.UseFlag == "Y" &&
                                    p.Port >= 1 && p.Port <= 4 &&  // 只選 L1-L4
                                    !pendingEndStations.Contains(p.StationNo))
                        .OrderByDescending(p => p.Priority)
                        .ThenBy(p => p.Port)
                        .FirstOrDefault();

                    if (emptySlot == null)
                    {
                        return BadRequest(new { message = "4F 烘烤後 (L1-L4) 沒有可放置的空位" });
                    }
                }
                else
                {
                    // O/P/S/N 區 → 依序尋找：M 區（雷雕區）→ Q 區（出貨區）→ R 區
                    emptySlot = _DBContext.oPort
                        .Where(p => p.Block == "M" &&
                                    p.HaveFlag == "0" &&
                                    (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                    p.UseFlag == "Y" &&
                                    !pendingEndStations.Contains(p.StationNo))
                        .OrderBy(p => p.Port)
                        .FirstOrDefault();

                    if (emptySlot == null)
                    {
                        // M 區滿，查詢 Q 區（出貨區）
                        emptySlot = _DBContext.oPort
                            .Where(p => p.Block == "Q" &&
                                        p.HaveFlag == "0" &&
                                        (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                        p.UseFlag == "Y" &&
                                        !pendingEndStations.Contains(p.StationNo))
                            .OrderBy(p => p.Port)
                            .FirstOrDefault();
                    }

                    if (emptySlot == null)
                    {
                        // Q 區滿，查詢 R 區
                        emptySlot = _DBContext.oPort
                            .Where(p => p.Block == "R" &&
                                        p.HaveFlag == "0" &&
                                        (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                        p.UseFlag == "Y" &&
                                        !pendingEndStations.Contains(p.StationNo))
                            .OrderBy(p => p.Port)
                            .FirstOrDefault();
                    }

                    if (emptySlot == null)
                    {
                        return BadRequest(new { message = "目前沒有可放置的貨架" });
                    }
                }

                // 建立派送任務 (oNeed) - 帶入起點站的 RackId
                string rackId = port.RackId ?? "";
                
                // 空板回送 WorkOrder 為空（與其他區域一致）
                // 任務完成後 HaveFlag 會根據 WorkOrder 是否為空設定為 "1"（空板）
                string workOrderForRelease = "";
                
                string sql = "INSERT INTO oNeed (ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime, AssignFlag) VALUES({0},{1},{2},{3},{4},{5},{6})";
                _DBContext.Database.ExecuteSqlRaw(sql,
                    stationNo,                                    // ObjStation (起點)
                    rackId,                                       // RackId (從起點站讀取)
                    workOrderForRelease,                          // WorkOrder (空板回送為空)
                    emptySlot.StationNo,                          // EndStation (終點)
                    "Web",                                        // TaskSource
                    DateTime.Now.ToString("yyyyMMddHHmmssffffff"), // TaskDateTime
                    "");                                          // AssignFlag

                return Ok(new { message = "Release 成功", stationNo = stationNo, endStation = emptySlot.StationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Release 失敗", error = ex.Message });
            }
        }
        /// <summary>
        /// 依 FallbackAreas 設定依序找空位（HaveFlag=0、UseFlag=Y、未被註冊、未被其他任務佔用）
        /// </summary>
        private string FindFallbackEmptySlot(string fallbackAreas, string excludeStation)
        {
            if (string.IsNullOrEmpty(fallbackAreas)) return null;

            var pendingEndStations = _DBContext.oNeed
                .Where(n => n.AssignFlag == null || n.AssignFlag == "")
                .Select(n => n.EndStation)
                .ToList();

            string[] areas = fallbackAreas.Split(',');
            foreach (string area in areas)
            {
                string trimmedArea = area.Trim();
                if (string.IsNullOrEmpty(trimmedArea)) continue;

                var slot = _DBContext.oPort
                    .Where(p => p.Block == trimmedArea &&
                                p.HaveFlag == "0" &&
                                p.UseFlag == "Y" &&
                                (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                p.StationNo != excludeStation &&
                                !pendingEndStations.Contains(p.StationNo))
                    .OrderBy(p => p.Port)
                    .FirstOrDefault();

                if (slot != null) return slot.StationNo;
            }

            return null;
        }
    }

}
