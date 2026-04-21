using Newtonsoft.Json;
using SAA_MsSql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace HikAGVWebAPI.App_Start
{
    public class SQLData
    {
        SqlHelper mSql;

        public SQLData(string DbName, string DbIp)
        {
            mSql = new SqlHelper($"Data Source={DbIp};Initial Catalog={DbName};Persist Security Info=True;User ID=mcs;Password=Zz123456");
        }

        #region 新增類
        public void Insert_ubMission(oMissionModel oMission)
        {
            string sSQL = $@"insert into ubMission
                             select * from oMission
                              where TaskDateTime = {oMission.TaskDateTime} ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Insert_oMission(oMissionModel oMission)
        {
            string parentTDT = string.IsNullOrEmpty(oMission.ParentTaskDateTime) ? "NULL" : $"'{oMission.ParentTaskDateTime}'";
            string sSQL = $@"insert into oMission
                                    (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, ParentTaskDateTime)
                             values ('{oMission.TaskDateTime}', 0, '{oMission.BeginStation}', '{oMission.EndStation}', '{oMission.TaskSource}', '{oMission.RackId}', '', {parentTDT}) ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        /// <summary>
        /// 為系統任務（IDLE_RETURN / CROSS_FLOOR_DISPATCH）寫入 oRequire，
        /// 讓 SCP 畫面可見並可取消。跳過 cPair 的 oNeed→oRequire 轉換流程。
        /// </summary>
        public void Insert_oRequire(oMissionModel oMission)
        {
            string sSQL = $@"insert into oRequire
                                    (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder, AssignFlag)
                             values ('{oMission.TaskDateTime}', '{oMission.BeginStation}', 0, '{oMission.BeginStation}', '{oMission.EndStation}', '{oMission.TaskSource}', '', '', 'Y') ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Insert_ubCancelLog(string taskDateTime, string parentTaskDateTime, string taskSource, string beginStation, string endStation, string taskCode, string rcsCancelResult)
        {
            string parentTDT = string.IsNullOrEmpty(parentTaskDateTime) ? "NULL" : $"'{parentTaskDateTime}'";
            string sSQL = $@"insert into ubCancelLog
                                    (CancelTime, TaskDateTime, ParentTaskDateTime, TaskSource, BeginStation, EndStation, TaskCode, RcsCancelResult)
                             values (GETDATE(), '{taskDateTime}', {parentTDT}, '{taskSource}', '{beginStation}', '{endStation}', '{taskCode}', '{rcsCancelResult}') ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Insert_ubActivation(ubActivationModel ActivationModel)
        {
            string sSQL = $@"insert into ubActivation
                                    (TaskDateTime, ShuttleStation, ShuttleId, TaskType, BeginStation, EndStation, ReceivingTime, BeginTime, EndTime)
                             values ('{ActivationModel.TaskDateTime}','{ActivationModel.ShuttleStation}','{ActivationModel.ShuttleId}','{ActivationModel.TaskType}','{ActivationModel.BeginStation}','{ActivationModel.EndStation}','{ActivationModel.ReceivingTime}','{ActivationModel.BeginTime}','{ActivationModel.EndTime}') ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }
        #endregion 新增類

        #region 搜尋類
        public List<oMissionModel> Select_oMission()
        {
            string sSQL = $@"select * from oMission
                              --where OkFlag is NULL
                              order by TaskDateTime";
            DataTable dt = mSql.QuerySqlByAutoOpen(sSQL).Tables[0];
            string sJson = JsonConvert.SerializeObject(dt);
            return JsonConvert.DeserializeObject<List<oMissionModel>>(sJson);
        }

        public oMissionModel Select_oMissionByTaskCode(string TaskCode)
        {
            string sSQL = $@"select * from oMission
                              where TaskCode = '{TaskCode}' ";
            DataTable dt = mSql.QuerySqlByAutoOpen(sSQL).Tables[0];
            string sJson = JsonConvert.SerializeObject(dt);
            return (JsonConvert.DeserializeObject<List<oMissionModel>>(sJson)).FirstOrDefault();
        }

        public List<oPortModel> Select_oPort(string StationNo)
        {
            string sSQL = $@"select * from oPort
                              where StationNo = '{StationNo}' ";
            DataTable dt = mSql.QuerySqlByAutoOpen(sSQL).Tables[0];
            string sJson = JsonConvert.SerializeObject(dt);
            return JsonConvert.DeserializeObject<List<oPortModel>>(sJson);
        }

        public List<oShuttleModel> Select_oShuttle()
        {
            string sSQL = $@"select * from oShuttle ";
            DataTable dt = mSql.QuerySqlByAutoOpen(sSQL).Tables[0];
            string sJson = JsonConvert.SerializeObject(dt);
            return JsonConvert.DeserializeObject<List<oShuttleModel>>(sJson);
        }

        public List<oTaskTypeRouteModel> Select_oTaskTypeRoute(string moveType)
        {
            string sSQL = $@"select * from oTaskTypeRoute where UseFlag = 'Y' and MoveType = '{moveType}' ";
            DataTable dt = mSql.QuerySqlByAutoOpen(sSQL).Tables[0];
            string sJson = JsonConvert.SerializeObject(dt);
            return JsonConvert.DeserializeObject<List<oTaskTypeRouteModel>>(sJson);
        }

        /// <summary>
        /// 查詢 ubMission 中最近 lookbackSeconds 秒內完成的 CROSS_FLOOR_DISPATCH（供 CrossFloorManager 啟動時重建冷卻狀態）
        /// 篩選：TaskSource = CROSS_FLOOR_DISPATCH AND ShuttleId = 指定車 AND OkFlag in ('Y','C') AND ParentTaskDateTime 非空 AND EndTime 於 lookback 區間內
        /// 回傳：EndTime 最新的一筆；無相符則回傳 null
        /// </summary>
        public oMissionModel Select_RecentCrossFloorDispatchCompletion(string shuttleId, int lookbackSeconds)
        {
            string cutoff = DateTime.Now.AddSeconds(-lookbackSeconds).ToString("yyyyMMddHHmmssffffff");
            string sSQL = $@"select top 1 *
                               from ubMission
                              where TaskSource = 'CROSS_FLOOR_DISPATCH'
                                and ShuttleId = '{shuttleId}'
                                and OkFlag in ('Y', 'C')
                                and ParentTaskDateTime is not null
                                and EndTime >= '{cutoff}'
                              order by EndTime desc";
            DataTable dt = mSql.QuerySqlByAutoOpen(sSQL).Tables[0];
            string sJson = JsonConvert.SerializeObject(dt);
            return (JsonConvert.DeserializeObject<List<oMissionModel>>(sJson)).FirstOrDefault();
        }
        #endregion 搜尋類

        #region 更新類
        public void Update_oShuttle(AGVStatusData agvStatus)
        {
            string sSQL = $@"update oShuttle
                                set Battery = '{agvStatus?.battery}'
                                   ,Status = '{agvStatus?.status}'
                                   ,PosX = '{agvStatus?.posX.PadRight(6, '0')}'
                                   ,PosY = '{agvStatus?.posY.PadRight(6, '0')}'
                                   ,RobotDir = '{agvStatus?.robotDir}'
                                   ,MapCode = '{agvStatus?.mapCode}'
                                   ,UpdateTime = GETDATE()
                              where ShuttleId = {agvStatus?.robotCode} ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        /// <summary>
        /// 依 ShuttleId 即時更新 oShuttle.MapCode（callback end/cancel 時呼叫）
        /// 用於縮短 UpdateAGVStatus 輪詢窗口中被幽靈 MapCode 覆蓋的時間
        /// </summary>
        public void Update_oShuttleMapCode(string shuttleId, string mapCode)
        {
            string sSQL = $@"update oShuttle
                                set MapCode = '{mapCode}'
                                   ,UpdateTime = GETDATE()
                              where ShuttleId = {shuttleId} ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oShuttleStation(oMissionModel oMission, string Status)
        {
            string sSQL = $@"update oShuttle
                                set Status = '{Status}'
                                   ,LastStation = '{oMission?.BeginStation}'
                                   ,BeginStation = '{oMission?.BeginStation}'
                                   ,EndStation = '{oMission?.EndStation}'
                              where ShuttleId = {oMission?.ShuttleId} ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oMissionBeginTime(oMissionModel oMission)
        {
            string sSQL = $@"update oMission
                                set TaskCode = '{oMission.TaskCode}'
                                   ,BeginTime = '{oMission.BeginTime}'
                                   ,OkFlag = '{oMission.OkFlag}'
                              where TaskDateTime = '{oMission.TaskDateTime}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oMissionShuttleID(oMissionModel oMission)
        {
            string sSQL = $@"update oMission
                                set ShuttleId = '{oMission.ShuttleId}' 
                              where TaskDateTime = '{oMission.TaskDateTime}'
                                and TaskCode = '{oMission.TaskCode}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oMissionRackID(oMissionModel oMission)
        {
            string sSQL = $@"update oMission
                                set RackId = '{oMission.RackId}' 
                              where TaskDateTime = '{oMission.TaskDateTime}'
                                and TaskCode = '{oMission.TaskCode}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oMissionEndTime(oMissionModel oMission)
        {
            string sSQL = $@"update oMission
                                set OkFlag = '{oMission.OkFlag}'
                                   ,EndTime = '{oMission.EndTime}'
                              where TaskDateTime = '{oMission.TaskDateTime}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oRequire(oMissionModel oMission)
        {
            string sSQL = $@"update oRequire
                                set OkFlag = '{oMission.OkFlag}'
                              where TaskDateTime = '{oMission.TaskDateTime}'
                                and BeginStation = '{oMission.BeginStation}'
                                and EndStation = '{oMission.EndStation}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        /// <summary>
        /// 依 TaskDateTime 更新 oMission.OkFlag（用於父任務連動取消）
        /// 與 Update_oMissionEndTime 不同：只用 TaskDateTime 一個條件
        /// </summary>
        public void Update_oMissionOkFlag(string taskDateTime, string okFlag)
        {
            string sSQL = $@"update oMission
                                set OkFlag = '{okFlag}'
                                   ,EndTime = '{DateTime.Now:yyyyMMddHHmmssffffff}'
                              where TaskDateTime = '{taskDateTime}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        /// <summary>
        /// 依 TaskDateTime 更新 oRequire.OkFlag（用於父任務連動取消）
        /// 與 Update_oRequire 不同：只用 TaskDateTime 一個條件，涵蓋 MCS 被 cPair 拆成多段的所有 oRequire
        /// </summary>
        public void Update_oRequireOkFlag(string taskDateTime, string okFlag)
        {
            string sSQL = $@"update oRequire
                                set OkFlag = '{okFlag}'
                              where TaskDateTime = '{taskDateTime}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oPortEmpty(string StationNo)
        {
            string sSQL = $@"update oPort
                                set RackId = ''
                                   ,WorkOrder = ''
                                   ,HaveFlag = '0'
                              where StationNo = '{StationNo}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_oPort(oMissionModel oMission, string HaveFlag)
        {
            string sSQL = $@"update oPort
                                set RackId = '{oMission.RackId}'
                                   ,WorkOrder = '{oMission.WorkOrder}'
                                   ,HaveFlag = '{HaveFlag}'
                                   ,PutTime = '{oMission.EndTime}'
                              where StationNo = '{oMission.EndStation}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }

        public void Update_ubActivation(ubActivationModel ActivationModel, string FieldName)
        {
            string sSQL = $@"update ubActivation
                                set ShuttleId = '{ActivationModel.ShuttleId}'
                                   ,{FieldName} = '{DateTime.Now.ToString("yyyyMMddHHmmssffffff")}'
                              where TaskDateTime = '{ActivationModel.TaskDateTime}' ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }
        #endregion 更新類

        #region 刪除類
        public void Delete_oMission(oMissionModel oMission)
        {
            string sSQL = $@"delete oMission
                              where TaskDateTime = {oMission.TaskDateTime} ";
            mSql.WriteSqlByAutoOpen(sSQL);
        }
        #endregion 刪除類

        public string GetTaskDateTimeIncludeRandom(bool bolDateTime)
        {
            Random rdn = new Random();
            Int32 int32Rdn = rdn.Next(100, 980);
            if (bolDateTime)
            {
                return DateTime.Now.ToString("yyyyMMddHHmmssfff") + int32Rdn.ToString();
            }
            else
            {
                return int32Rdn.ToString();
            }
        }
    }
}