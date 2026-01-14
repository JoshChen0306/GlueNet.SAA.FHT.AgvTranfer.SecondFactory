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
                              where ShuttleId = {agvStatus?.robotCode} ";
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