using System;
using System.Web.Http;
using HikAGVWebAPI.App_Start;

namespace HikAGVWebAPI
{
    [Route("[controller]")]
    public partial class HikAGVController : ApiController
    {
        private object objLockAGVCallback = new object();
        private object objLockWarnCallback = new object();
        /// <summary>
        /// AGV 任務執行通知
        /// </summary>
        /// <param name="CallbackModel"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(CallbackRoute + "agvCallback")]
        public CallBackAck AGVCallback(CallBack CallbackModel)
        {
            lock (objLockAGVCallback)
            {
                try
                {
                    mLog.TraceOut($"========================================== AGV Callback Start! ==========================================", Log.LogType.NONE);
                    mLog.TraceOut("Get Call Back Data! " + CallbackModel?.ToString(), Log.LogType.NONE);

                    CallBackAck reponse = new CallBackAck();
                    if (CallbackModel == null)
                    {
                        mLog.TraceOut("AGV Call Back Data Is Null!", Log.LogType.ERROR);

                        return new CallBackAck()
                        {
                            code = "-9",
                            message = "AGV CallBack Data Error",
                            reqCode = "-9",
                        };
                    }

                    string sMethod = CallbackModel.method;
                    string sRackID = CallbackModel.podCode;
                    string sStartPositionCode = CallbackModel.wbCode;
                    string sCurrentPositionCode = CallbackModel.currentPositionCode;
                    string sTaskCode = CallbackModel.taskCode;
                    string sShuttleID = CallbackModel.robotCode;
                    string sMapCode = CallbackModel.mapCode;
                    oMissionModel oMission = mDB.Select_oMissionByTaskCode(sTaskCode);
                    mLog.TraceOut("Get oMission Data! " + oMission?.ToString(), Log.LogType.NONE);
                    reponse = new CallBackAck()
                    {
                        code = "0",
                        message = "",
                        reqCode = CallbackModel.reqCode
                    };

                    ubActivationModel ubActivation = new ubActivationModel()
                    {
                        TaskDateTime = oMission?.TaskDateTime,
                        ShuttleId = sShuttleID,
                        BeginStation = oMission?.BeginStation,
                        EndStation = oMission?.EndStation,
                    };

                    switch (sMethod)
                    {
                        case CallBackMethod.start://更新任務狀態為 R(執行中)
                            oMission.ShuttleId = sShuttleID;
                            UpdateStart(oMission, ubActivation);
                            mLog.TraceOut($"AGV Start Finish!", Log.LogType.NONE);
                            break;
                        case CallBackMethod.outbin:
                            oMission.RackId = sRackID;
                            UpdateOutBin(oMission, ubActivation, sStartPositionCode);
                            mLog.TraceOut($"AGV Outbin Finish!", Log.LogType.NONE);
                            break;
                        case CallBackMethod.end:
                            UpdateEnd(oMission, ubActivation, sCurrentPositionCode, sMapCode);
                            if (oMission?.TaskSource == CrossFloorManager.IDLE_RETURN)
                            {
                                mLog.TraceOut($"[CrossFloor] 歸位任務 Callback end，通知 CrossFloorManager 完成", Log.LogType.NONE);
                                Dispatch.CrossFloor?.OnIdleReturnCompleted();
                            }
                            else if (oMission?.TaskSource == CrossFloorManager.CROSS_FLOOR_DISPATCH)
                            {
                                mLog.TraceOut($"[CrossFloor] 預調度任務 Callback end，通知 CrossFloorManager 完成", Log.LogType.NONE);
                                Dispatch.CrossFloor?.OnCrossFloorDispatchCompleted(oMission);
                            }
                            mLog.TraceOut($"AGV End Finish!", Log.LogType.NONE);
                            break;
                        case CallBackMethod.cancel:
                            UpdateCancel(oMission, sMapCode);
                            if (oMission?.TaskSource == CrossFloorManager.IDLE_RETURN)
                            {
                                mLog.TraceOut($"[CrossFloor] 歸位任務 Callback cancel，通知 CrossFloorManager 重置", Log.LogType.NONE);
                                Dispatch.CrossFloor?.OnIdleReturnCompleted();
                            }
                            else if (oMission?.TaskSource == CrossFloorManager.CROSS_FLOOR_DISPATCH)
                            {
                                mLog.TraceOut($"[CrossFloor] 預調度任務 Callback cancel，通知 CrossFloorManager 重置", Log.LogType.NONE);
                                Dispatch.CrossFloor?.OnCrossFloorDispatchCompleted(oMission);
                            }

                            if ((oMission?.TaskSource == CrossFloorManager.CROSS_FLOOR_DISPATCH
                                 || oMission?.TaskSource == CrossFloorManager.IDLE_RETURN)
                                && !string.IsNullOrEmpty(oMission?.ParentTaskDateTime))
                            {
                                mDB.Update_oMissionOkFlag(oMission.ParentTaskDateTime, "C");
                                mDB.Update_oRequireOkFlag(oMission.ParentTaskDateTime, "C");
                                mLog.TraceOut($"[CrossFloor] 連動取消父任務 ParentTaskDateTime={oMission.ParentTaskDateTime}，oMission/oRequire OkFlag 皆設為 C",
                                    Log.LogType.NONE);
                            }

                            mLog.TraceOut($"AGV Cancel Finish!", Log.LogType.NONE);
                            break;
                        case CallBackMethod.apply:
                            mLog.TraceOut($"AGV Apply Finish!", Log.LogType.NONE);
                            break;
                        default:
                            reponse.code = "-1";
                            reponse.message = $"RCS Wrong Method!";
                            break;
                    }

                    mLog.TraceOut($"========================================== AGV Callback End! ==========================================", Log.LogType.NONE);
                    return reponse;
                }
                catch (Exception ex)
                {
                    mLog.TraceOut($"AGVCallback Exception! [Exception] : {ex.Message}", Log.LogType.ERROR);

                    return new CallBackAck()
                    {
                        code = "-99",
                        message = ex.Message,
                        reqCode = CallbackModel.reqCode,
                    };
                }
            }
        }

        /// <summary>
        /// AGV 任務開始
        /// </summary>
        /// <param name="oMission"></param>
        /// <param name="ubActivation"></param>
        private void UpdateStart(oMissionModel oMission, ubActivationModel ubActivation)//, string sCurrentPositionCode)
        {
            try
            {
                if (oMission != null)
                {
                    mDB.Update_oRequire(oMission);
                    mDB.Update_oMissionShuttleID(oMission);
                    mDB.Update_oShuttleStation(oMission, "R");
                    mLog.TraceOut($"Update oMission Shuttle ID!", Log.LogType.NONE);
                }

                mDB.Update_ubActivation(ubActivation, "BeginTime");
                mLog.TraceOut($"Update Activation Begin Time!", Log.LogType.NONE);
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"UpdateStart Exception! [Exception] : {ex.Message}", Log.LogType.ERROR);
            }
        }

        /// <summary>
        /// AGV 走出儲位
        /// </summary>
        /// <param name="oMission"></param>
        /// <param name="ubActivation"></param>
        /// <param name="sCurrentPositionCode"></param>
        private void UpdateOutBin(oMissionModel oMission, ubActivationModel ubActivation, string sCurrentPositionCode)
        {
            try
            {
                if (oMission != null)
                {
                    mDB.Update_oMissionRackID(oMission);
                    mLog.TraceOut($"Update oMission Rack ID!", Log.LogType.NONE);
                }

                mDB.Update_oPortEmpty(sCurrentPositionCode);
                mLog.TraceOut($"Update oPort Rack ID To Empty And HaveFlag = E! [Position] : {sCurrentPositionCode}", Log.LogType.NONE);
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"UpdateOutBin Exception! [Exception] : {ex.Message}", Log.LogType.ERROR);
            }
        }

        /// <summary>
        /// AGV 任務結束
        /// </summary>
        /// <param name="oMission"></param>
        /// <param name="ubActivation"></param>
        /// <param name="sCurrentPositionCode"></param>
        /// <param name="sMapCode">RCS 回傳的實際抵達地圖代碼（A 方案：即時糾正 oShuttle.MapCode）</param>
        private void UpdateEnd(oMissionModel oMission, ubActivationModel ubActivation, string sCurrentPositionCode, string sMapCode)
        {
            try
            {
                if (oMission != null)
                {
                    string sHaveFlag = string.IsNullOrEmpty(oMission.WorkOrder) ? "1" : "3";
                    oMission.OkFlag = "Y";
                    oMission.EndTime = DateTime.Now.ToString("yyyyMMddHHmmssffffff");
                    mDB.Update_oPort(oMission, sHaveFlag);
                    mDB.Update_oRequire(oMission);
                    mDB.Update_oMissionEndTime(oMission);
                    mDB.Insert_ubMission(oMission);
                    mDB.Delete_oMission(oMission);
                    mDB.Update_oShuttleStation(oMission, "I");
                    mLog.TraceOut($"Update End Job Finish!", Log.LogType.NONE);
                }

                mDB.Update_ubActivation(ubActivation, "EndTime");

                UpdateShuttleMapCodeFromCallback(oMission, sMapCode, "UpdateEnd");
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"UpdateEnd Exception! [Exception] : {ex.Message}", Log.LogType.ERROR);
            }
        }

        /// <summary>
        /// AGV 任務取消（RCS 回報 cancel callback）
        /// 清理 oMission 並歸檔至 ubMission，避免殘留 OkFlag=R 的孤兒記錄
        /// </summary>
        /// <param name="oMission"></param>
        /// <param name="sMapCode">RCS 回傳的實際抵達地圖代碼（A 方案：即時糾正 oShuttle.MapCode）</param>
        private void UpdateCancel(oMissionModel oMission, string sMapCode)
        {
            try
            {
                if (oMission != null)
                {
                    oMission.OkFlag = "C";
                    oMission.EndTime = DateTime.Now.ToString("yyyyMMddHHmmssffffff");
                    mDB.Update_oRequire(oMission);
                    mDB.Update_oMissionEndTime(oMission);
                    mDB.Insert_ubMission(oMission);
                    mDB.Delete_oMission(oMission);
                    mDB.Update_oShuttleStation(oMission, "I");
                    mLog.TraceOut($"Update Cancel Job Finish!", Log.LogType.NONE);
                }

                UpdateShuttleMapCodeFromCallback(oMission, sMapCode, "UpdateCancel");
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"UpdateCancel Exception! [Exception] : {ex.Message}", Log.LogType.ERROR);
            }
        }

        /// <summary>
        /// A 方案：以 RCS callback 的 mapCode 即時糾正 oShuttle.MapCode
        /// 縮短 UpdateAGVStatus 輪詢窗口中被幽靈 MapCode 覆蓋的時間
        /// 空字串 / oMission 為 null 直接略過；SQL 例外僅記 WARN 不中斷 callback 流程
        /// </summary>
        private void UpdateShuttleMapCodeFromCallback(oMissionModel oMission, string sMapCode, string sCaller)
        {
            if (oMission == null) return;
            if (string.IsNullOrEmpty(sMapCode)) return;
            if (string.IsNullOrEmpty(oMission.ShuttleId)) return;

            try
            {
                mDB.Update_oShuttleMapCode(oMission.ShuttleId, sMapCode);
                mLog.TraceOut($"[{sCaller}] Update oShuttle MapCode from callback! [ShuttleId] : {oMission.ShuttleId}, [MapCode] : {sMapCode}", Log.LogType.NONE);
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"[{sCaller}] Update_oShuttleMapCode Exception! [ShuttleId] : {oMission.ShuttleId}, [MapCode] : {sMapCode}, [Exception] : {ex.Message}", Log.LogType.WARN);
            }
        }

        /// <summary>
        /// AGV 告警推送通知
        /// </summary>
        /// <param name = "WarnModel" ></ param >
        /// < returns ></ returns >
        [HttpPost]
        [Route(CallbackRoute + "warnCallback")]
        public WarnCallBackAck WarnCallback(WarnCallBack WarnModel)
        {
            lock (objLockWarnCallback)
            {
                mLog.TraceOut($"========================================== Warn Callback Start! ==========================================", Log.LogType.NONE);
                mLog.TraceOut($"AGV 告警推送通知! {WarnModel?.ToString()}", Log.LogType.NONE);

                if (WarnModel == null)
                {
                    mLog.TraceOut("AGV Warn Call Back Data Is Null!", Log.LogType.ERROR);

                    return new WarnCallBackAck()
                    {
                        code = "-999",
                        message = "AGV Warn Call Back Data Error",
                        reqCode = "-999",
                    };
                }

                WarnCallBackAck reponse = new WarnCallBackAck()
                {
                    code = "0",
                    message = "OK",
                    reqCode = WarnModel.reqCode
                };

                //碰撞條觸發
                CallFHtAPI(WarnModel);

                mLog.TraceOut($"========================================== Warn Callback End! ==========================================", Log.LogType.NONE);
                return reponse;
            }
        }

        /// <summary>
        /// AGV 綁定解綁通知
        /// </summary>
        /// <param name="BindModel"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(CallbackRoute + "bindNotify")]
        public BindNotifyAck bindNotify(BindNotify BindModel)
        {
            mLog.TraceOut($"========================================== Bind Notify Callback Start! ==========================================", Log.LogType.NONE);
            mLog.TraceOut($"AGV 綁定解綁通知! {BindModel?.ToString()}", Log.LogType.NONE);
            BindNotifyAck reponse = new BindNotifyAck()
            {
                code = "0",
                message = "OK",
                reqCode = BindModel.reqCode
            };

            mLog.TraceOut($"========================================== Bind Notify Callback End! ==========================================", Log.LogType.NONE);
            return reponse;
        }

        /// <summary>
        /// AGV 申請回庫倉位(CTU)
        /// </summary>
        /// <param name="ApplyModel"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(CallbackRoute + "applyBin")]
        public ApplyBinAck applyBin(ApplyBin ApplyModel)
        {
            ApplyBinAck reponse = new ApplyBinAck()
            {
                code = "0",
                message = "OK",
                reqCode = ApplyModel.reqCode
            };

            return reponse;
        }

        /// <summary>
        /// 發生碰撞條觸發告警
        /// </summary>
        /// <param name="WarnModel"></param>
        private void CallFHtAPI(WarnCallBack WarnModel)
        {
            try
            {
                foreach (WarnData warnData in WarnModel?.data)
                {
                    if (WarnContent.IndexOf(warnData?.warnContent) > -1)
                    {
                        FHtAPI fHtAPI = new FHtAPI()
                        {
                            account = FHtSettings.APIAccount,
                            api_key = FHtSettings.APIKey,
                            team_sn = FHtSettings.Teamcode,
                            text_content = $@"車號：{warnData.robotCode}，告警訊息：{warnData.warnContent}!",
                        };

                        mLog.TraceOut($"Send Data! {fHtAPI.ToString()}", Log.LogType.NONE);
                        string Result = PostData(fHtAPI.ToDictionary());
                        mLog.TraceOut($"Return Data! {Result}", Log.LogType.NONE);
                    }
                }
            }
            catch (Exception ex)
            {
                mLog.TraceOut($"CallFHtAPI Exception! [Exception] : {ex.Message}", Log.LogType.ERROR);
            }
        }
    }
}