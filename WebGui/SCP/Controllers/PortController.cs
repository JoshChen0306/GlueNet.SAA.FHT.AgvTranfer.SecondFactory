using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SCP.Models;

namespace SCP.Controllers
{
    [Authorize(Roles = "1")]
    public class PortController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly agvDB_1400004Context _DBContext;
        public PortController(IConfiguration configuration, agvDB_1400004Context DBContext)
        {
            _configuration = configuration;
            _DBContext = DBContext;
        }
        public IActionResult Index(string floor = "1F")
        {
            // 樓層與區域對應
            var floorBlocks = new Dictionary<string, string[]>
            {
                { "1F", new[] { "A", "B", "C", "D", "E", "F", "EE", "G" } },
                { "2F", new[] { "H", "M", "N", "O", "P", "Q", "R", "S", "T" } },
                { "3F", new[] { "J", "I" } },
                { "4F", new[] { "K", "L" } }
            };

            var blocks = floorBlocks.ContainsKey(floor) ? floorBlocks[floor] : floorBlocks["1F"];

            #region [讀取暫存架位置及狀態]      
            List<oPort> query = _DBContext.oPort
                .Where(p => blocks.Contains(p.Block))
                .ToList();
            List<Position> result = new List<Position>();

            // 將 floor 轉換為 area 格式 (如 "1F" -> "FHT2-1F")
            string area = $"FHT2-{floor}";

            foreach (var item in query)
            {
                // 跳過沒有座標設定的站點
                if (string.IsNullOrEmpty(item.Remark) || !item.Remark.Contains(","))
                    continue;

                Position data = new Position
                {
                    Name = item.StationNo,
                    Left = ConvertX(item.Remark.Split(",")[0], area),
                    Bottom = ConvertY(item.Remark.Split(",")[1], area),
                    Transform = "rotate(" + item.Remark.Split(",")[2] + "deg)",
                    ImgSrc = GetStationImgSrc(item.HaveFlag, item.WorkOrder),
                    Reserve = string.IsNullOrEmpty(item.BgnToEnd) ? "N" : "Y",
                    InterfaceName = item.InterfaceName,
                    MachineName = item.MachineName,
                    UseFlag = item.UseFlag,
                    HaveFlag = item.HaveFlag,
                    WorkOrder = item.WorkOrder,
                };
                result.Add(data);
            }
            #endregion

            ViewBag.positions = result;
            ViewBag.CurrentFloor = floor;
            ViewBag.MapImage = $"/img/FHT2-{floor}.png";
            return View();
        }
        public IActionResult UpdateoPort([FromBody] Dictionary<string, string> port)
        {
            //Dictionary<string, string> portDict = port.ToDictionary(item => item["name"], item => item["value"]);
            string name = port["name"];
            string machinename = port["machinename"];
            string interfacename = port["interfacename"];
            string haveflag = port["haveflag"];
            string workorder = (haveflag == "3") ? port["workorder"] : "";
            string useflag = port.ContainsKey("useflag") ? "Y" : "N";

            try
            {
                _DBContext.oPort
                    .Where(p => p.StationNo == name)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.MachineName, machinename)
                        .SetProperty(p => p.InterfaceName, interfacename)
                        .SetProperty(p => p.HaveFlag, haveflag)
                        .SetProperty(p => p.WorkOrder, workorder)
                        .SetProperty(p => p.UseFlag, useflag));
            }
            catch (Exception ex)
            {

            }

            return Ok();
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

            // V Cut 物料顏色判斷：只有 HaveFlag=3 且有 WorkOrder 時才檢查
            if (haveFlag == "3" && !string.IsNullOrEmpty(workOrder))
            {
                if (workOrder.Contains("^VCUT^DONE"))
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
            // 將0-1範圍的Y座標轉換為minPercent-maxPercent%範圍
            result = (minPercentY + (normalizedY * percentRangeY)).ToString() + "%";
            return result;
        }
    }
}
