using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCP.Models;
using System;
using System.Globalization;
using System.Security.Cryptography.Xml;

namespace SCP.Controllers
{
    [Route("api/[controller]")]
    public class CommonController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly agvDB_1400004Context _DBContext;
        public CommonController(IConfiguration configuration, agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
            _configuration = configuration;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("ShowMap")]
        public IActionResult ShowMap(string area = "")
        {
            try
            {
                // 根據使用者路線權限過濾可見樓層
                var allowedFloors = GetUserAllowedFloors();
                ViewBag.AllowedFloors = allowedFloors;

                // 如果未指定 area 或指定的 area 不在允許的樓層中，使用第一個允許的樓層
                if (string.IsNullOrEmpty(area) || !allowedFloors.Contains(area))
                {
                    area = allowedFloors.FirstOrDefault() ?? "FHT2-1F";
                }

                LogMgt.Logger?.Info($"[ShowMap] 開始載入地圖, area={area}");
                
                LogMgt.Logger?.Debug($"[ShowMap] 正在取得站點資料...");
                var positions = GetTrac(area);
                LogMgt.Logger?.Info($"[ShowMap] 站點資料載入成功, 共 {positions.Count} 個站點");
                ViewBag.positions = positions;
                
                LogMgt.Logger?.Debug($"[ShowMap] 正在取得 AGV 資料...");
                var agvPositions = GetAgv(area);
                LogMgt.Logger?.Info($"[ShowMap] AGV 資料載入成功, 共 {agvPositions.Count} 個 AGV");
                ViewBag.AgvPositions = agvPositions;
                ViewBag.CurrentArea = area;

                LogMgt.Logger?.Info($"[ShowMap] 地圖載入完成");
                return PartialView("_MapPartial");
            }
            catch (Exception ex)
            {
                LogMgt.Logger?.Error(ex, $"[ShowMap] 錯誤: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 取得使用者允許的樓層清單（根據路線權限）
        /// </summary>
        private List<string> GetUserAllowedFloors()
        {
            // 預設所有樓層
            var allFloors = new List<string> { "FHT2-1F", "FHT2-2F", "FHT2-3F", "FHT2-4F" };
            
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                // 未登入使用者，顯示所有樓層
                return allFloors;
            }

            // 取得使用者的路線權限
            var userRoutes = _DBContext.pUserRoute
                .Where(ur => ur.UserId == userId)
                .Join(_DBContext.pRoute.Where(r => r.ControlFlag == "Y"),
                      ur => ur.RouteId,
                      r => r.RouteId,
                      (ur, r) => r)
                .ToList();

            if (!userRoutes.Any())
            {
                // 使用者沒有設定路線權限，使用舊的邏輯，顯示所有樓層
                return allFloors;
            }

            // 取得使用者所有允許的起點區域（只使用 SourceAreas）
            // 使用者只能看到他可以操作的樓層
            var allowedAreas = userRoutes
                .Where(r => !string.IsNullOrEmpty(r.SourceAreas))
                .SelectMany(r => r.SourceAreas.Split(',').Select(a => a.Trim()))
                .Distinct()
                .ToList();

            // 讀取 FloorArea 設定，判斷哪些樓層包含使用者允許的區域
            var floorArea = _configuration.GetSection("FloorArea").Get<Dictionary<string, string[]>>();
            var allowedFloors = new List<string>();

            // 樓層名稱到地圖區域的映射
            var floorToMapArea = new Dictionary<string, string>
            {
                { "1F", "FHT2-1F" },
                { "2F - 站內運輸", "FHT2-2F" },
                { "2F - 站外運輸", "FHT2-2F" },
                { "3F", "FHT2-3F" },
                { "4F", "FHT2-4F" }
            };

            foreach (var floor in floorArea)
            {
                // 檢查該樓層的區域是否有使用者可用的區域
                if (floor.Value.Any(a => allowedAreas.Contains(a)))
                {
                    if (floorToMapArea.ContainsKey(floor.Key))
                    {
                        var mapArea = floorToMapArea[floor.Key];
                        if (!allowedFloors.Contains(mapArea))
                        {
                            allowedFloors.Add(mapArea);
                        }
                    }
                }
            }

            return allowedFloors.Any() ? allowedFloors : allFloors;
        }

        [HttpGet("UpdateTrac")]
        public IActionResult UpdateTrac()
        {
            var data = Json(GetTrac());
            return data;
        }
        [HttpGet("UpdateAgv")]
        public IActionResult UpdateAgv()
        {
            var data = Json(GetAgv());
            return data;
        }

        [HttpGet("GetHitchhikeStation")]
        public Dictionary<string, string>? GetHitchhikeStation()
        {
            var mission = _DBContext.oMission.FirstOrDefault(x => x.EndStation.StartsWith("G") && x.OkFlag == "Y");
            Dictionary<string, string>? station = new Dictionary<string, string>();
            if (mission != null)
            {
                var beginStation = _DBContext.oPort.FirstOrDefault(x => x.Block == "G" && x.HaveFlag == "1");
                var endStation = _DBContext.oPort.FirstOrDefault(x => x.Block == "J" && x.HaveFlag == "0");
                if (beginStation != null && endStation != null)
                {
                    station.Add("beginStation", beginStation.StationNo);
                    station.Add("endStation", endStation.StationNo);
                }
                else station = null;
            }
            return station;
        }

        private List<Position> GetTrac(string area)
        {
            #region [讀取暫存架位置及狀態]      
            List<oPort> query = _DBContext.oPort.Where(p => p.UseFlag == "Y" && p.Area == area).ToList();
            List<Position> result = new List<Position>();

            foreach (var item in query)
            {
                Position data = new Position
                {
                    Name = item.StationNo,
                    Left = ConvertX(item.Remark.Split(",")[0], area),
                    Bottom = ConvertY(item.Remark.Split(",")[1], area),
                    Transform = "rotate(" + item.Remark.Split(",")[2] + "deg)",
                    ImgSrc = GetStationImgSrc(item.HaveFlag, item.WorkOrder),
                    Reserve = string.IsNullOrEmpty(item.BgnToEnd) ? "N" : "Y",
                    HaveFlag = item.HaveFlag,
                    RackId = item.RackId,
                    WorkOrder = item.WorkOrder,
                    InterfaceName = item.InterfaceName,
                    PutTime = item.PutTime
                };
                result.Add(data);
            }
            #endregion
            return result;
        }
        private List<Position> GetTrac()
        {
            #region [讀取暫存架位置及狀態]      
            List<oPort> query = _DBContext.oPort.Where(p => p.UseFlag == "Y").ToList();
            List<Position> result = new List<Position>();

            foreach (var item in query)
            {
                Position data = new Position
                {
                    Name = item.StationNo,
                    Left = ConvertX(item.Remark.Split(",")[0], item.Area),
                    Bottom = ConvertY(item.Remark.Split(",")[1], item.Area),
                    Transform = "rotate(" + item.Remark.Split(",")[2] + "deg)",
                    ImgSrc = GetStationImgSrc(item.HaveFlag, item.WorkOrder),
                    Reserve = string.IsNullOrEmpty(item.BgnToEnd) ? "N" : "Y",
                    HaveFlag = item.HaveFlag,
                    RackId = item.RackId,
                    WorkOrder = item.WorkOrder,
                    InterfaceName = item.InterfaceName,
                    PutTime = item.PutTime
                };
                result.Add(data);
            }
            #endregion
            return result;
        }

        /// <summary>
        /// 將 Web 區域代碼轉換為海康 MapCode
        /// </summary>
        private string GetMapCodeFromArea(string area)
        {
            var mapping = _configuration.GetSection("MapCodeMapping").Get<Dictionary<string, string>>();
            if (mapping != null && mapping.ContainsKey(area))
            {
                return mapping[area];
            }
            // 如果找不到映射，返回原始 area（向後兼容）
            return area;
        }

        private List<oShuttle> GetAgv(string area)
        {
            #region [讀取車輛狀態及位置]
            // 將 Web 區域代碼轉換為海康 MapCode
            string mapCode = GetMapCodeFromArea(area);
            
            List<oShuttle> AgvPositions = _DBContext.oShuttle.Where(x => x.MapCode == mapCode).ToList();
            foreach (var item in AgvPositions)
            {
                // 使用原始 area 進行座標轉換（因為 appsettings 使用 FHT2-1F 作為 key）
                item.PosX = ConvertX(item.PosX, area);
                item.PosY = ConvertY(item.PosY, area);
            }
            #endregion
            return AgvPositions;
        }
        private List<oShuttle> GetAgv()
        {
            #region [讀取車輛狀態及位置]
            List<oShuttle> AgvPositions = _DBContext.oShuttle.ToList();
            foreach (var item in AgvPositions)
            {
                item.PosX = ConvertX(item.PosX, item.MapCode);
                item.PosY = ConvertY(item.PosY, item.MapCode);
            }
            #endregion
            return AgvPositions;
        }

        /// <summary>
        /// 根據 HaveFlag 和 WorkOrder 決定站點圖示
        /// </summary>
        private string GetStationImgSrc(string haveFlag, string workOrder)
        {
            // 處理 null 或空值的情況，預設為空架 (0)
            if (string.IsNullOrEmpty(haveFlag))
            {
                haveFlag = "0";
            }

            // 特殊物料顏色判斷：只有 HaveFlag=3 且有 WorkOrder 時才檢查
            if (haveFlag == "3" && !string.IsNullOrEmpty(workOrder))
            {
                // 優先順序：^NG → ^RETURN → ^VCUT^DONE → ^VCUT
                if (workOrder.Contains("^NG"))
                {
                    // 橙色：NG 回送物料（品檢失敗）
                    return "/img/ng-material.svg";
                }
                else if (workOrder.Contains("^RETURN"))
                {
                    // 黃色：Release 回送空板（不可派送）
                    return "/img/return-material.svg";
                }
                else if (workOrder.Contains("^VCUT^DONE"))
                {
                    // 紫色：V Cut 已加工完成
                    return "/img/vcut-done.svg";
                }
                else if (workOrder.Contains("^VCUT"))
                {
                    // 橙色：V Cut 待加工
                    return "/img/vcut-pending.svg";
                }
            }
            // 預設：使用設定檔的圖示
            var imgSrc = _configuration.GetSection("TracStatus").GetSection(haveFlag).Value;
            // 如果設定檔中找不到對應的圖示，使用空架圖示作為預設
            return imgSrc ?? "/img/empty.svg";
        }

        private string ConvertX(string posX, string area)
        {
            string result;
            var setting = _configuration.GetSection($"AgvSetting:{area}");
            double minPercentX = Convert.ToDouble(setting["minPercentX"]);
            double maxPercentX = Convert.ToDouble(setting["maxPercentX"]);
            double minX = Convert.ToDouble(setting["minX"]);
            double maxX = Convert.ToDouble(setting["maxX"]);
            double percentRangeX = maxPercentX - minPercentX;
            double rangeX = maxX - minX;

            double normalizedX = (Convert.ToDouble(posX) - minX) / rangeX;
            // 將0-1範圍的X座標轉換為minPercent-maxPercent%範圍
            result = (minPercentX + (normalizedX * percentRangeX)).ToString() + "%";
            return result;
        }

        private string ConvertY(string posY, string area)
        {
            string result;
            var setting = _configuration.GetSection($"AgvSetting:{area}");
            double minPercentY = Convert.ToDouble(setting["minPercentY"]);
            double maxPercentY = Convert.ToDouble(setting["maxPercentY"]);
            double minY = Convert.ToDouble(setting["minY"]);
            double maxY = Convert.ToDouble(setting["maxY"]);
            double percentRangeY = maxPercentY - minPercentY;
            double rangeY = maxY - minY;

            double normalizedY = (Convert.ToDouble(posY) - minY) / rangeY;
            // 將0-1範圍的X座標轉換為minPercent-maxPercent%範圍
            result = (minPercentY + (normalizedY * percentRangeY)).ToString() + "%";
            return result;
        }
    }
}
