using HikAGVDll;
using HikAGVWebAPI.App_Start;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HikAGVWebAPI
{
    public class Dispatch
    {
        private readonly Configuration config;//抓取 Config 檔案資料
        private readonly string ConfigFileName = string.Format("{0}\\Config\\FHtSetting.config", string.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath) ? AppDomain.CurrentDomain.BaseDirectory : AppDomain.CurrentDomain.RelativeSearchPath);
        private DBSettings DBSettings = new DBSettings();
        private LogSettings LogSettings = new LogSettings();
        private FHtSettings FHtSettings = new FHtSettings();
        private ElevatorSettings ElevatorSettings = new ElevatorSettings();

        private Thread DispatchThread;//執行續
        private bool _stopThread = false;
        private HikAGV hikAGV = new HikAGV();//海康接口
        private Log mLog;//AGV 任務 Log 路徑
        private SQLData mDB;//SQL Server 連線
        private const int SleepTime = 1000;//執行續執行時間
        private Dictionary<string, DateTime?> dtChargeStartTime = new Dictionary<string, DateTime?>();//充電稼動率
        private Dictionary<string, DateTime?> dtAbnormalStartTime = new Dictionary<string, DateTime?>();//任務異常動率
        private static readonly HttpClient client = new HttpClient();//上拋客戶端
        private Dictionary<string, Dictionary<string, object>> dicLowBattery = new Dictionary<string, Dictionary<string, object>>();//

        public Dispatch()
        {
            config = LoadExternalConfig(ConfigFileName);
            ReadDBConfig();
            InitialData();

            DispatchThread = new Thread(new ThreadStart(Execute));
            DispatchThread.IsBackground = false;
            DispatchThread.Start();
        }

        /// <summary>
        /// 初始化資料
        /// </summary>
        private void InitialData()
        {
            try
            {
                mDB = new SQLData(DBSettings.DBName, DBSettings.DBIP);//SQL Server 連線
                mLog = new Log(LogSettings.LogPath, "AGVDispatch");
                mLog.KeepDate = LogSettings.KeepDate;

                #region 依照 DB oShuttle 表內的車號增加充電稼動率變數
                List<oShuttleModel> oShuttles = mDB.Select_oShuttle();
                dtChargeStartTime = oShuttles.Select(x => new KeyValuePair<string, DateTime?>(x.ShuttleId, null)).ToDictionary(x => x.Key, x => x.Value);
                dtAbnormalStartTime = oShuttles.Select(x => new KeyValuePair<string, DateTime?>(x.ShuttleId, null)).ToDictionary(x => x.Key, x => x.Value);
                dicLowBattery = oShuttles.Select(x => new KeyValuePair<string, Dictionary<string, object>>(x.ShuttleId, new Dictionary<string, object>())).ToDictionary(x => x.Key, x => x.Value);
                #endregion 依照 DB oShuttle 表內的車號增加充電稼動率變數
            }
            catch (Exception ex)
            {
            }
        }

        #region 讀取設定檔全部資料
        private Configuration LoadExternalConfig(string configName)
        {
            try
            {
                ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
                configMap.ExeConfigFilename = configName;
                return ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            }
            catch
            {
                return null;
            }
        }
        #endregion 讀取設定檔全部資料

        #region 依照設定檔讀取 Section 資料
        private void ReadDBConfig()
        {
            try
            {
                //載入這套系統要搭配的 Config DB 資訊
                SectionDB SectionDB = config.GetSection(nameof(SectionDB)) as SectionDB;
                DBSettings = SectionDB?.DBSettings;

                //載入這套系統要搭配的 Config Log 資訊
                SectionLog SectionLog = config.GetSection(nameof(SectionLog)) as SectionLog;
                LogSettings = SectionLog?.LogSettings;

                //載入這套系統要搭配的 Config FHt 資訊
                SectionFHt SectionFHt = config.GetSection(nameof(SectionFHt)) as SectionFHt;
                FHtSettings = SectionFHt?.FHtSettings;

                //載入電梯配置
                SectionElevator SectionElevator = config.GetSection(nameof(SectionElevator)) as SectionElevator;
                ElevatorSettings = SectionElevator?.ElevatorSettings ?? new ElevatorSettings();
            }
            catch
            {
            }
        }
        #endregion 依照設定檔讀取 Section 資料

        public int Collect = 0;
        #region
        public void Execute()
        {
            while (!_stopThread)
            {
                try
                {
                    UpdateAGVStatus();
                    AGVSchedulingTask();
                }
                catch (Exception ex)
                {
                    mLog.TraceOut($"Execute Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
                }

                if (Collect > 10)
                {
                    GC.Collect();
                    Collect = 0;
                    mLog.TraceOut($"Execute GC Collect!", Log.LogType.NONE);
                }

                Collect++;
                Thread.Sleep(SleepTime);
            }
        }

        // 停止线程的安全方法
        public void Stop()
        {
            _stopThread = true;
            DispatchThread.Join();  // 等待线程结束
        }
        #endregion

        /// <summary>
        /// 查詢小車狀態
        /// </summary>
        private void UpdateAGVStatus()
        {
            try
            {
                mLog.TraceOut($"========================================== Get AGV Status Start! ==========================================", Log.LogType.NONE);

                var mapCodeSettings = hikAGV.AGVSettings.AGVMapCode;

                //var mapCodes = new []{ "AA","BB","DD","FF"};
                var mapCodes = mapCodeSettings.Split(',');


                foreach (var mapCode in mapCodes)
                {
                    AGVStatusAck AGVAck = GetAGVStatus(mapCode);

                    if (AGVAck?.data?.Count > 0)
                    {
                        mLog.TraceOut($"Get All AGV Status Ack Data! {AGVAck?.ToString()}", Log.LogType.NONE);

                        foreach (AGVStatusData agvData in AGVAck?.data)
                        {
                            string ShuttleStatus = "I";
                            switch (agvData?.status)
                            {
                                case "4"://任務空閒
                                case "7"://充電狀態
                                    if (agvData?.status == "7" && dtChargeStartTime[agvData?.robotCode] == null)
                                    {
                                        dtChargeStartTime[agvData?.robotCode] = DateTime.Now;
                                        mLog.TraceOut($"Shuttle Start Charging!", Log.LogType.NONE);
                                    }

                                    if (agvData?.status == "4")
                                        InsertShuttleChargeActivate(agvData?.robotCode);

                                    InsertAbnormalActivate(agvData?.robotCode);
                                    break;
                                case "3"://任務異常
                                    ShuttleStatus = "A";
                                    if (dtChargeStartTime[agvData?.robotCode] == null)
                                        dtAbnormalStartTime[agvData?.robotCode] = DateTime.Now;

                                    mLog.TraceOut($"任務異常!", Log.LogType.NONE);
                                    break;
                                case "1"://任務完成
                                case "2"://任務執行中
                                    ShuttleStatus = "R";
                                    break;
                                case "5"://機器人暫停
                                case "6"://舉升貨架狀態
                                case "8"://弧線行走中
                                case "9"://充滿維護
                                    mLog.TraceOut($"Normal Status!", Log.LogType.NONE);
                                    break;
                                default://其餘狀態目前認定為異常
                                    mLog.TraceOut($"Default Alarm Status!", Log.LogType.NONE);
                                    break;
                            }

                            agvData.status = ShuttleStatus;
                            mDB.Update_oShuttle(agvData);
                            mLog.TraceOut($"Update AGV Data! {agvData?.ToString()}", Log.LogType.NONE);

                            //電量低於 40 跟 25 上報 FHt
                            CheckBattery(agvData);
                        }
                    }

                    mLog.TraceOut($"========================================== Get AGV Status End! ==========================================", Log.LogType.NONE);
                }

            }
            catch (Exception ex)
            {
                mLog.TraceOut($"Update AGV Status Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }
        }

        /// <summary>
        /// 電量小於 40 及 25 時，個別發送訊息給客戶端
        /// </summary>
        /// <param name="agvData"></param>
        private void CheckBattery(AGVStatusData agvData)
        {
            try
            {
                List<FHtAPI> SendAPI = new List<FHtAPI>();
                int AGVBattery = 0;
                int.TryParse(agvData.battery, out AGVBattery);
                string[] LowBatteries = FHtSettings.LowBattery.Split(',');
                Dictionary<string, object> ShuttleBattery = dicLowBattery[agvData.robotCode];
                if (AGVBattery > 40)
                    dicLowBattery[agvData.robotCode] = null;

                foreach (string sLowBattery in LowBatteries)
                {
                    int LowBattery = 0;
                    int.TryParse(sLowBattery, out LowBattery);
                    if (AGVBattery <= LowBattery)
                    {
                        if (ShuttleBattery.ContainsKey(sLowBattery) == false)
                        {
                            SendAPI.Add(new FHtAPI()
                            {
                                account = FHtSettings.APIAccount,
                                api_key = FHtSettings.APIKey,
                                team_sn = FHtSettings.Teamcode,
                                text_content = $@"車號：{agvData.robotCode}，目前電量為：{AGVBattery}，電量小於 {LowBattery}!",
                            });

                            ShuttleBattery.Add(sLowBattery, SendAPI);
                        }
                    }
                }

                foreach (FHtAPI fHtAPI in SendAPI)
                {
                    mLog.TraceOut($"Send Data! {fHtAPI.ToString()}", Log.LogType.NONE);
                    string Result = PostData(fHtAPI.ToDictionary());
                    mLog.TraceOut($"Return Data! {Result}", Log.LogType.NONE);
                }
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"Check Battery Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }
        }

        protected internal string PostData(Dictionary<string, string> Data)
        {
            HttpResponseMessage response = null;

            try
            {
                HttpContent content = new FormUrlEncodedContent(Data);
                content.Headers.ContentType = new MediaTypeHeaderValue(FHtSettings.ContentType);
                Task<HttpResponseMessage> task = Task.Run(() => client.PostAsync(FHtSettings.WebURL, content));
                task.Wait();
                response = task.Result;
                Task<string> streamReader = Task.Run(() => response.Content.ReadAsStringAsync());
                return streamReader.Result;
            }
            catch (Exception e)
            {
                return string.Empty;
            }
            finally
            {
                response?.Dispose();
            }
        }

        /// <summary>
        /// 生成 AGV 任務
        /// </summary>
        private void AGVSchedulingTask()
        {
            try
            {
                List<oMissionModel> oAllMissions = mDB.Select_oMission();
                if (oAllMissions.Count == 0)
                    return;

                mLog.TraceOut($"========================================== oMission Start! ==========================================", Log.LogType.NONE);
                List<oMissionModel> oMissionRun = oAllMissions.Where(x => x.OkFlag == "R").ToList();
                if (oMissionRun.Count == 2)//判斷只執行 2 筆任務
                {
                    mLog.TraceOut($"Two Missions Is Running! {oMissionRun?.ToString()}", Log.LogType.NONE);
                    return;
                }

                oMissionModel oMission = oAllMissions.Where(x => string.IsNullOrEmpty(x.OkFlag)).FirstOrDefault();
                if (oMission != null)
                {
                    mLog.TraceOut($"Get All Missions Data! {oAllMissions?.ToString()}", Log.LogType.NONE);
                    mLog.TraceOut($"Get oMission Data! {oMission?.ToString()}", Log.LogType.NONE);
                    SchedulingTaskAck ack = GetSchedulingTask(hikAGV.AGVSettings.AGVTaskType, oMission);
                    mLog.TraceOut($"Get Scheduling Task Ack Data! {ack?.ToString()}", Log.LogType.NONE);
                    if (ack?.code == "0")
                    {
                        DateTime drNow = DateTime.Now;
                        oMission.OkFlag = "R";
                        oMission.TaskCode = ack?.data;
                        oMission.BeginTime = drNow.ToString("yyyyMMddHHmmssffffff");
                        ubActivationModel ubActivation = new ubActivationModel()
                        {
                            TaskDateTime = oMission.TaskDateTime,
                            ShuttleStation = oMission.BeginStation,
                            //ShuttleId = oMission.ShuttleId,
                            TaskType = oMission.OkFlag,
                            BeginStation = oMission.BeginStation,
                            EndStation = oMission.EndStation,
                            ReceivingTime = drNow.ToString("yyyyMMddHHmmssffffff"),
                        };

                        mDB.Update_oMissionBeginTime(oMission);
                        mDB.Insert_ubActivation(ubActivation);
                        mLog.TraceOut($"Update oMission OkFlag = R and Insert ubActivation!", Log.LogType.NONE);
                    }
                }

                List<oMissionModel> oMissionDetete = oAllMissions.Where(x => x.OkFlag == "C").ToList();
                DeleteMission(oMissionDetete);

                mLog.TraceOut($"========================================== oMission End! ==========================================", Log.LogType.NONE);
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"AGV Scheduling Task Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }
        }

        /// <summary>
        /// 當 AGV 狀態為空閒且有充電開始時間時，新增充電稼動率
        /// </summary>
        /// <param name="ShuttleID"></param>
        private void InsertShuttleChargeActivate(string ShuttleID)
        {
            try
            {
                if (dtChargeStartTime[ShuttleID] != null)
                {
                    ubActivationModel ubActivation = new ubActivationModel()
                    {
                        TaskDateTime = dtChargeStartTime[ShuttleID]?.ToString("yyyyMMddHHmmssffffff"),
                        ShuttleStation = "C",
                        ShuttleId = ShuttleID,
                        TaskType = "C",
                        BeginStation = "C",
                        EndStation = "C",
                        ReceivingTime = dtChargeStartTime[ShuttleID]?.ToString("yyyyMMddHHmmssffffff"),
                        BeginTime = dtChargeStartTime[ShuttleID]?.ToString("yyyyMMddHHmmssffffff"),
                        EndTime = DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                    };

                    mDB.Insert_ubActivation(ubActivation);
                    dtChargeStartTime[ShuttleID] = null;
                    mLog.TraceOut($"Insert Charge Activate! {ubActivation?.ToString()}", Log.LogType.NONE);
                }
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"Insert Shuttle Charge Activate Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }
        }

        /// <summary>
        /// 任務異常恢復，新增任務異常稼動率
        /// </summary>
        /// <param name="ShuttleID"></param>
        private void InsertAbnormalActivate(string ShuttleID)
        {
            try
            {
                if (dtAbnormalStartTime[ShuttleID] != null)
                {
                    ubActivationModel ubActivation = new ubActivationModel()
                    {
                        TaskDateTime = dtAbnormalStartTime[ShuttleID]?.ToString("yyyyMMddHHmmssffffff"),
                        ShuttleStation = "A",
                        ShuttleId = ShuttleID,
                        TaskType = "A",
                        BeginStation = "A",
                        EndStation = "A",
                        ReceivingTime = dtAbnormalStartTime[ShuttleID]?.ToString("yyyyMMddHHmmssffffff"),
                        BeginTime = dtAbnormalStartTime[ShuttleID]?.ToString("yyyyMMddHHmmssffffff"),
                        EndTime = DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                    };

                    mDB.Insert_ubActivation(ubActivation);
                    dtAbnormalStartTime[ShuttleID] = null;
                    mLog.TraceOut($"Insert Abnormal Activate! {ubActivation?.ToString()}", Log.LogType.NONE);
                }
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"Insert Abnormal Activate Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }
        }

        /// <summary>
        /// 查詢 AGV 狀態
        /// </summary>
        /// <returns></returns>
        private AGVStatusAck GetAGVStatus(string AGVMapCode)
        {
            AGVStatusAck ReturnAck = new AGVStatusAck();

            try
            {
                AGVStatus AGVStatus = new AGVStatus()
                {
                    reqCode = DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                    mapCode = AGVMapCode,
                };

                ReturnAck = hikAGV.AGVStatus(AGVStatus);
            }
            catch (Exception ex)
            {
                ReturnAck = new AGVStatusAck();
                mLog.TraceOut($"Get AGV Status Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }

            return ReturnAck;
        }

        /// <summary>
        /// AGV 生成任務單(不指定車號)
        /// </summary>
        /// <param name="ShuttleID"></param>
        /// <param name="TaskType"></param>
        /// <param name="oMission"></param>
        /// <returns></returns>
        private SchedulingTaskAck GetSchedulingTask(string TaskType, oMissionModel oMission)
        {
            SchedulingTaskAck ReturnAck = new SchedulingTaskAck();

            try
            {
                var rackId = string.IsNullOrEmpty(oMission.RackId) ? "-1" : oMission.RackId;

                // 計算電梯路徑
                var pathCalculator = new ElevatorPathCalculator(ElevatorSettings);
                var fullPath = pathCalculator.CalculatePath(oMission.BeginStation, oMission.EndStation);
                
                // 根據路線查詢對應的 TaskType
                string actualTaskType = pathCalculator.GetTaskType(
                    oMission.BeginStation, 
                    oMission.EndStation,
                    hikAGV.AGVSettings.CrossFloorTaskTypeMap,
                    TaskType);  // 預設 TaskType（同樓層）
                mLog.TraceOut($"TaskType: {actualTaskType}, Route: {pathCalculator.GetFloor(oMission.BeginStation)}>{pathCalculator.GetFloor(oMission.EndStation)}", Log.LogType.NONE);
                
                string PositionCode = string.Join(";", fullPath.Select(p => $"{p},00"));
                mLog.TraceOut($"Calculated Path: {PositionCode}", Log.LogType.NONE);
                SchedulingTask AGVStatus = new SchedulingTask()
                {
                    reqCode = DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                    podCode = rackId,
                    taskTyp = actualTaskType,
                    positionCodePath = PositionCode.Split(';').Select(x => x.Split(','))
                                                     .Select(x => new CodePath { positionCode = x[0], type = x[1] }).ToList(),
                };

                mLog.TraceOut($"SchedulingTask : {JsonConvert.SerializeObject(AGVStatus)}", Log.LogType.NONE);


                ReturnAck = hikAGV.SchedulingTask(AGVStatus);
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"Get Scheduling Task Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }

            return ReturnAck;
        }

        private void DeleteMission(List<oMissionModel> oMissionDetete)
        {
            try
            {
                foreach (oMissionModel DeleteMission in oMissionDetete)
                {
                    // 若任務已派發至 RCS (有 TaskCode)，先呼叫海康取消任務 API
                    if (!string.IsNullOrEmpty(DeleteMission.TaskCode))
                    {
                        CancelTask preCancelRequest = new CancelTask()
                        {
                            reqCode = DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                            taskCode = DeleteMission.TaskCode,
                            forceCancel = "0"  // 軟取消 (預設)
                        };

                        mLog.TraceOut($"Send Cancel Task API! TaskCode: {DeleteMission.TaskCode}", Log.LogType.NONE);
                        CancelTaskAck ack = hikAGV.CancelTask(preCancelRequest);

                        mLog.TraceOut($"Cancel Task API Response: {ack?.ToString()}", Log.LogType.NONE);

                        if (ack?.code != "0")
                        {
                            // 檢查是否為「任務已結束或已取消」的情況，若是則允許刪除本地記錄
                            bool isTaskAlreadyCancelled = !string.IsNullOrEmpty(ack?.message) &&
                                (ack.message.Contains("已结束") || ack.message.Contains("已取消") || ack.message.Contains("不存在"));

                            if (!isTaskAlreadyCancelled)
                            {
                                mLog.TraceOut($"Cancel Task Failed! TaskCode: {DeleteMission.TaskCode}, Message: {ack?.message}", Log.LogType.NONE);
                                continue; // 取消失敗，暫不刪除，等待下次重試
                            }

                            mLog.TraceOut($"Task already cancelled in RCS, proceed to delete local record. TaskCode: {DeleteMission.TaskCode}", Log.LogType.NONE);
                        }
                    }

                    mDB.Delete_oMission(DeleteMission);
                    mLog.TraceOut($"Delete Cancel Mission! {DeleteMission?.ToString()}", Log.LogType.NONE);
                }
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"Delete Mission Exception! [Exception] : {ex.Message}", Log.LogType.NONE);
            }
        }
    }
}