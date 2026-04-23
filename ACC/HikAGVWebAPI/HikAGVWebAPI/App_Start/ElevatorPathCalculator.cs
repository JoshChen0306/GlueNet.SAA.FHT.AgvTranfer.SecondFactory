using System;
using System.Collections.Generic;
using System.Linq;

namespace HikAGVWebAPI
{
    /// <summary>
    /// 電梯路徑計算引擎
    /// 根據起終點計算包含電梯中繼點的完整路徑
    /// </summary>
    public class ElevatorPathCalculator
    {
        private readonly ElevatorSettings _settings;

        // 客梯站點
        private Dictionary<string, string> _customerWaitPoints;    // 樓層 -> 等待點
        private Dictionary<string, string> _customerInsidePoints;  // 樓層 -> 電梯內點
        private HashSet<string> _customerFloors;

        // 客貨梯站點
        private Dictionary<string, string> _freightWaitPoints;     // 樓層 -> 等待點
        private Dictionary<string, string> _freightInsidePoints;   // 樓層 -> 電梯內點
        private HashSet<string> _freightFloors;

        // 站點樓層對照
        private Dictionary<string, string> _stationFloorMapping;   // 站點首字母 -> 樓層

        public ElevatorPathCalculator(ElevatorSettings settings)
        {
            _settings = settings;
            ParseSettings();
        }

        /// <summary>
        /// 解析配置設定
        /// </summary>
        private void ParseSettings()
        {
            // 解析客梯樓層
            _customerFloors = new HashSet<string>(
                _settings.CustomerElevatorFloors.Split(',').Select(s => s.Trim()));

            // 解析客梯等待點
            _customerWaitPoints = ParsePointMapping(_settings.CustomerElevatorWaitPoints);

            // 解析客梯電梯內點
            _customerInsidePoints = ParsePointMapping(_settings.CustomerElevatorInsidePoints);

            // 解析客貨梯樓層
            _freightFloors = new HashSet<string>(
                _settings.FreightElevatorFloors.Split(',').Select(s => s.Trim()));

            // 解析客貨梯等待點
            _freightWaitPoints = ParsePointMapping(_settings.FreightElevatorWaitPoints);

            // 解析客貨梯電梯內點
            _freightInsidePoints = ParsePointMapping(_settings.FreightElevatorInsidePoints);

            // 解析站點樓層對照
            _stationFloorMapping = ParsePointMapping(_settings.StationFloorMapping);
        }

        /// <summary>
        /// 解析 "Key:Value,Key:Value" 格式的字串
        /// </summary>
        private Dictionary<string, string> ParsePointMapping(string mapping)
        {
            return mapping.Split(',')
                .Select(s => s.Trim().Split(':'))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
        }

        /// <summary>
        /// 反查電梯等待點所屬樓層（用於 IDLE_RETURN 任務的 BeginStation/EndStation 還原）
        /// </summary>
        public string GetFloorByWaitPoint(string waitPoint)
        {
            if (string.IsNullOrEmpty(waitPoint))
                return null;

            var freight = _freightWaitPoints.FirstOrDefault(kv => kv.Value == waitPoint);
            if (freight.Key != null) return freight.Key;

            var customer = _customerWaitPoints.FirstOrDefault(kv => kv.Value == waitPoint);
            return customer.Key;
        }

        /// <summary>
        /// 取得站點所屬樓層（根據首字母）
        /// </summary>
        public string GetFloor(string station)
        {
            if (string.IsNullOrEmpty(station))
                return null;

            var prefix = station[0].ToString().ToUpper();
            if (_stationFloorMapping.ContainsKey(prefix))
                return _stationFloorMapping[prefix];

            return null;
        }

        /// <summary>
        /// 根據起終樓層與路由清單取得對應的 TaskType
        /// </summary>
        /// <param name="beginStation">起點站點</param>
        /// <param name="endStation">終點站點</param>
        /// <param name="routes">路由清單（從 DB 查詢，已篩選 UseFlag='Y'）</param>
        /// <param name="defaultTaskType">全域預設 TaskType（找不到對應設定時使用）</param>
        /// <returns>對應的 TaskType</returns>
        public string GetTaskType(string beginStation, string endStation,
                                  List<oTaskTypeRouteModel> routes, string defaultTaskType)
        {
            var beginFloor = GetFloor(beginStation);
            var endFloor = GetFloor(endStation);

            // 無法判斷樓層：返回預設 TaskType
            if (beginFloor == null || endFloor == null)
                return defaultTaskType;

            if (routes == null || routes.Count == 0)
                return defaultTaskType;

            // 查詢符合起終樓層的路由
            var match = routes.FirstOrDefault(r =>
                r.FromFloor == beginFloor && r.ToFloor == endFloor);

            if (match != null)
                return match.TaskType;

            // 找不到對應路線，返回預設值
            return defaultTaskType;
        }

        /// <summary>
        /// 判斷是否為跨樓層任務
        /// </summary>
        public bool IsCrossFloor(string beginStation, string endStation)
        {
            var beginFloor = GetFloor(beginStation);
            var endFloor = GetFloor(endStation);

            if (beginFloor == null || endFloor == null)
                return false;

            return beginFloor != endFloor;
        }

        /// <summary>
        /// 計算完整路徑（包含電梯中繼點）
        /// </summary>
        public List<string> CalculatePath(string beginStation, string endStation)
        {
            var path = new List<string> { beginStation };

            var beginFloor = GetFloor(beginStation);
            var endFloor = GetFloor(endStation);

            // 無法判斷樓層時，直接返回起終點
            if (beginFloor == null || endFloor == null)
            {
                path.Add(endStation);
                return path;
            }

            // 同樓層，直接返回起終點
            if (beginFloor == endFloor)
            {
                path.Add(endStation);
                return path;
            }

            // 判斷使用哪個電梯
            bool needCustomerElevator = NeedCustomerElevator(beginFloor, endFloor);
            bool needFreightElevator = NeedFreightElevator(beginFloor, endFloor);
            bool isGoingUp = CompareFloor(beginFloor, endFloor) < 0;

            if (needCustomerElevator && needFreightElevator)
            {
                // 雙電梯換乘（經過 3F）
                if (isGoingUp)
                {
                    // 上行：客梯(起點→3F) → 客貨梯(3F→4F)
                    AddElevatorPath(path, beginFloor, "3F", isGoingUp, isCustomer: true);
                    AddElevatorPath(path, "3F", endFloor, isGoingUp, isCustomer: false);
                }
                else
                {
                    // 下行：客貨梯(4F→3F) → 客梯(3F→終點)
                    AddElevatorPath(path, beginFloor, "3F", isGoingUp, isCustomer: false);
                    AddElevatorPath(path, "3F", endFloor, isGoingUp, isCustomer: true);
                }
            }
            else if (needCustomerElevator)
            {
                // 只需客梯
                AddElevatorPath(path, beginFloor, endFloor, isGoingUp, isCustomer: true);
            }
            else if (needFreightElevator)
            {
                // 只需客貨梯
                AddElevatorPath(path, beginFloor, endFloor, isGoingUp, isCustomer: false);
            }

            path.Add(endStation);
            return path;
        }

        /// <summary>
        /// 判斷是否需要客梯（1F-3F）
        /// </summary>
        private bool NeedCustomerElevator(string beginFloor, string endFloor)
        {
            // 起點或終點在 1F 或 2F 時需要客梯
            return beginFloor == "1F" || beginFloor == "2F" || endFloor == "1F" || endFloor == "2F";
        }

        /// <summary>
        /// 判斷是否需要客貨梯（3F-4F）
        /// </summary>
        private bool NeedFreightElevator(string beginFloor, string endFloor)
        {
            // 起點或終點在 4F 時需要客貨梯
            return beginFloor == "4F" || endFloor == "4F";
        }

        /// <summary>
        /// 比較樓層（返回 -1 表示 floor1 < floor2）
        /// </summary>
        private int CompareFloor(string floor1, string floor2)
        {
            int f1 = int.Parse(floor1.Replace("F", ""));
            int f2 = int.Parse(floor2.Replace("F", ""));
            return f1.CompareTo(f2);
        }

        /// <summary>
        /// 建構歸位路徑：從指定樓層的電梯等待點出發，到達目標樓層的電梯等待點
        /// 不依賴 LastStation，直接根據樓層名稱計算完整電梯路徑
        /// </summary>
        /// <param name="fromFloor">目前所在樓層（由 MapCode 推算）</param>
        /// <param name="toFloor">目標歸位樓層</param>
        /// <returns>完整路徑站點列表，或 null（無法計算）</returns>
        public List<string> BuildReturnPath(string fromFloor, string toFloor)
        {
            if (string.IsNullOrEmpty(fromFloor) || string.IsNullOrEmpty(toFloor))
                return null;

            if (fromFloor == toFloor)
                return null;

            var path = new List<string>();
            bool isGoingUp = CompareFloor(fromFloor, toFloor) < 0;
            bool needCustomer = NeedCustomerElevator(fromFloor, toFloor);
            bool needFreight = NeedFreightElevator(fromFloor, toFloor);

            if (needCustomer && needFreight)
            {
                if (isGoingUp)
                {
                    // 上行：客梯(起→3F) → 出客梯等待點(W1) → 客貨梯(3F→終)
                    AddElevatorPath(path, fromFloor, "3F", true, isCustomer: true);
                    path.Add(_customerWaitPoints["3F"]);
                    AddElevatorPath(path, "3F", toFloor, true, isCustomer: false);
                }
                else
                {
                    // 下行：客貨梯(起→3F) → 出客貨梯等待點(X1) → 客梯(3F→終)
                    AddElevatorPath(path, fromFloor, "3F", false, isCustomer: false);
                    path.Add(_freightWaitPoints["3F"]);
                    AddElevatorPath(path, "3F", toFloor, false, isCustomer: true);
                }
            }
            else if (needFreight)
            {
                AddElevatorPath(path, fromFloor, toFloor, isGoingUp, isCustomer: false);
            }
            else if (needCustomer)
            {
                AddElevatorPath(path, fromFloor, toFloor, isGoingUp, isCustomer: true);
            }

            if (path.Count == 3 && (fromFloor == "1F" || fromFloor == "2F"))
            {
                path.Add(_customerWaitPoints[toFloor]);
            }
            else
            {
                // 加上目標樓層等待點作為路徑終點
                if (_freightWaitPoints.ContainsKey(toFloor))
                    path.Add(_freightWaitPoints[toFloor]);
                else if (_customerWaitPoints.ContainsKey(toFloor))
                    path.Add(_customerWaitPoints[toFloor]);
            }

            return path.Count > 0 ? path : null;
        }

        /// <summary>
        /// 取得目標樓層的電梯出口等待站點（用於自動歸位目標站設定）
        /// 優先回傳客貨梯等待點（服務高樓層），其次回傳客梯等待點
        /// </summary>
        public string GetElevatorExitStation(string currentStation, string targetFloor)
        {
            if (string.IsNullOrEmpty(targetFloor))
                return null;

            // 優先使用客貨梯等待點（3F-4F）
            if (_freightFloors.Contains(targetFloor) && _freightWaitPoints.ContainsKey(targetFloor))
                return _freightWaitPoints[targetFloor];

            // 其次使用客梯等待點（1F-3F）
            if (_customerFloors.Contains(targetFloor) && _customerWaitPoints.ContainsKey(targetFloor))
                return _customerWaitPoints[targetFloor];

            return null;
        }

        /// <summary>
        /// 添加電梯路徑
        /// </summary>
        /// <param name="path">路徑列表</param>
        /// <param name="fromFloor">起始樓層</param>
        /// <param name="toFloor">目標樓層</param>
        /// <param name="isGoingUp">是否上行</param>
        /// <param name="isCustomer">是否為客梯</param>
        private void AddElevatorPath(List<string> path, string fromFloor, string toFloor, bool isGoingUp, bool isCustomer)
        {
            var waitPoints = isCustomer ? _customerWaitPoints : _freightWaitPoints;
            var insidePoints = isCustomer ? _customerInsidePoints : _freightInsidePoints;

            if (isGoingUp)
            {
                // 上行：等待點(起) → 電梯內(起) → 電梯內(終)
                if (waitPoints.ContainsKey(fromFloor))
                    path.Add(waitPoints[fromFloor]);
                if (insidePoints.ContainsKey(fromFloor))
                    path.Add(insidePoints[fromFloor]);
                if (insidePoints.ContainsKey(toFloor))
                    path.Add(insidePoints[toFloor]);
            }
            else
            {
                // 下行：等待點(起) → 電梯內(起) → 電梯內(終)
                if (waitPoints.ContainsKey(fromFloor))
                    path.Add(waitPoints[fromFloor]);
                if (insidePoints.ContainsKey(fromFloor))
                    path.Add(insidePoints[fromFloor]);
                if (insidePoints.ContainsKey(toFloor))
                    path.Add(insidePoints[toFloor]);
            }
        }
    }
}
