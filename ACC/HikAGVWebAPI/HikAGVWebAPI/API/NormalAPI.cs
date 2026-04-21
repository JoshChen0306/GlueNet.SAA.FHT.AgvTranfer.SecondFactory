using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace HikAGVWebAPI
{
    [Route("[controller]")]
    public partial class HikAGVController : ApiController
    {
        //public const string Route = "rcms/services/rest/hikRpcService/";
        //public const string AGVStatusRoute = "rcms-dps/rest/";

        // ★ 模擬用：跨樓層車輛 AGV3 目前所在 MapCode（任務完成後自動更新）
        private static string _mockAgv3MapCode = "FF"; // 初始：3F

        // ★ 模擬用：電梯等待點 → MapCode 對照（對應 FHtSetting.config 的電梯設定）
        private static readonly Dictionary<string, string> _elevatorWaitPointToMapCode
            = new Dictionary<string, string>
            {
                { "U1", "AA" }, // 客梯 1F
                { "V1", "BB" }, // 客梯 2F
                { "W1", "DD" }, // 客梯 3F
                { "X1", "DD" }, // 貨梯 3F
                { "Y1", "FF" }, // 貨梯 4F
            };

        /// <summary>
        /// 生成任務單
        /// </summary>
        /// <param name="SchedulingTask"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "genAgvSchedulingTask")]
        public SchedulingTaskAck GenAgvSchedulingTask(SchedulingTask SchedulingTask)
        {
            // 1. 產生模擬的任務單號 (與 Dispatch 邏輯一致)
            string mockTaskCode = DateTime.Now.ToString("yyyyMMddHHmmssffffff");

            // 2. 啟動背景任務模擬 AGV 行為 (不卡住主執行緒，立刻回傳 Ack)
            Task.Run(async () =>
            {
                await SimulateAgvMovement(mockTaskCode, SchedulingTask);
            });

            SchedulingTaskAck reponse = new SchedulingTaskAck()
            {
                code = "0",
                message = "成功",
                reqCode = SchedulingTask.reqCode,
                data = mockTaskCode,
            };

            return reponse;
        }

        // ★★★ 新增：模擬 AGV 行走的邏輯 ★★★
        private async Task SimulateAgvMovement(string taskCode, SchedulingTask taskInfo)
        {
            try
            {
                // 解析起點與終點 (依據您 Dispatch 傳送的格式 "起點,00;終點,00")
                // 注意：需確保您的 SchedulingTask 模型結構能正確解析 positionCodePath
                string startStation = taskInfo.positionCodePath.FirstOrDefault()?.positionCode;
                string endStation = taskInfo.positionCodePath.LastOrDefault()?.positionCode;
                string robotCode = "3"; // 模擬跨樓層車號（對應 HikAGV.config CrossFloorShuttleId）

                using (var httpClient = new HttpClient())
                {
                    string callbackUrl = "http://localhost:54632/agv/agvCallbackService/agvCallback"; // 請確認您的 Port

                    // --- 階段 1: 模擬任務開始 (Start) ---
                    await Task.Delay(2000); // 模擬 2 秒後車子開始動
                    var startPayload = new
                    {
                        reqCode = DateTime.Now.Ticks.ToString(),
                        taskCode = taskCode,
                        method = "start",
                        robotCode = robotCode,
                        wbCode = startStation // 起點
                    };
                    await PostCallback(httpClient, callbackUrl, startPayload);

                    // --- 階段 2: 模擬行走與到達 (End) ---
                    await Task.Delay(5000); // 模擬走 5 秒到達終點
                    var endPayload = new
                    {
                        reqCode = DateTime.Now.Ticks.ToString(),
                        taskCode = taskCode,
                        method = "end",
                        robotCode = robotCode,
                        currentPositionCode = endStation // 終點
                    };
                    await PostCallback(httpClient, callbackUrl, endPayload);

                    // ★ end callback 後，更新 AGV3 的模擬 MapCode（讓下次 AGVStatus 輪詢回傳正確位置）
                    //if (endStation != null &&
                    //    _elevatorWaitPointToMapCode.TryGetValue(endStation, out string destMapCode))
                    //{
                    //    _mockAgv3MapCode = destMapCode;
                    //}
                }
            }
            catch (Exception ex)
            {
                // 這裡建議寫 Log 方便除錯
                // System.Diagnostics.Debug.WriteLine("模擬失敗: " + ex.Message);
            }
        }

        private async Task PostCallback(HttpClient client, string url, object payload)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }

        /// <summary>
        /// 繼續執行任務
        /// </summary>
        /// <param name="ContinueTask"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "continueTask")]
        public ContinueTaskAck ContinueTask(ContinueTask ContinueTask)
        {
            ContinueTaskAck reponse = new ContinueTaskAck()
            {
                code = "0",
                message = "成功",
                reqCode = ContinueTask.reqCode
            };

            return reponse;
        }

        /// <summary>
        /// 取消任務
        /// </summary>
        /// <param name="CancelTask"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "cancelTask")]
        public CancelTaskAck CancelTask(CancelTask CancelTask)
        {
            CancelTaskAck reponse = new CancelTaskAck()
            {
                code = "0",
                message = "成功",
                reqCode = CancelTask.reqCode
            };

            return reponse;
        }

        /// <summary>
        /// 任務優先權設置
        /// </summary>
        /// <param name="TaskPriority"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "setTaskPriority")]
        public TaskPriorityAck SetTaskPriority(TaskPriority TaskPriority)
        {
            TaskPriorityAck reponse = new TaskPriorityAck()
            {
                code = "0",
                message = "成功",
                reqCode = TaskPriority.reqCode
            };

            return reponse;
        }

        /// <summary>
        /// 貨架與位置綁定、解綁
        /// </summary>
        /// <param name="PodAndBerth"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "bindPodAndBerth")]
        public PodAndBerthAck BindPodAndBerth(PodAndBerth PodAndBerth)
        {
            PodAndBerthAck reponse = new PodAndBerthAck()
            {
                code = "0",
                message = "成功",
                reqCode = PodAndBerth.reqCode
            };

            return reponse;
        }

        /// <summary>
        /// 貨架與物料綁定、解綁
        /// </summary>
        /// <param name="PodAndMat"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "bindPodAndMat")]
        public PodAndMatAck BindPodAndMat(PodAndMat PodAndMat)
        {
            PodAndMatAck reponse = new PodAndMatAck()
            {
                code = "0",
                message = "成功",
                reqCode = PodAndMat.reqCode
            };

            return reponse;
        }

        /// <summary>
        /// 位置禁用與啟用
        /// </summary>
        /// <param name="LockPosition"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "lockPosition")]
        public LockPositionAck LockPosition(LockPosition LockPosition)
        {
            LockPositionAck reponse = new LockPositionAck()
            {
                code = "0",
                message = "成功",
                reqCode = LockPosition.reqCode
            };

            return reponse;
        }

        /// <summary>
        /// 地圖位置信息同步
        /// </summary>
        /// <param name="SyncMapDatas"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "syncMapDatas")]
        public SyncMapDatasAck SyncMapDatas(SyncMapDatas SyncMapDatas)
        {
            SyncMapDatasAck reponse = new SyncMapDatasAck()
            {
                code = "0",
                message = "成功",
                reqCode = SyncMapDatas.reqCode,
                data = new List<SyncDatas>()
                        {
                            new SyncDatas()
                            {
                                berthType = "3",
                                cooX = "17000.0",
                                cooY = "18000.0",
                                dataTyp = "1",
                                direction = "0",
                                mapCode = "AA",
                                mapDataCode = "1234567890",
                                positionCode = "0987654321",
                            },
                            new SyncDatas()
                            {
                                berthType = "3",
                                cooX = "14000.0",
                                cooY = "21999.0",
                                dataTyp = "10",
                                direction = "0",
                                mapCode = "AA",
                                mapDataCode = "0987654321",
                                positionCode = "1234567890",
                            },
                        },
            };

            return reponse;
        }

        /// <summary>
        /// 查詢貨架儲位與物料批次關係
        /// </summary>
        /// <param name="QryPodBerthAndMat"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "queryPodBerthAndMat")]
        public QryPodBerthAndMatAck QueryPodBerthAndMat(QryPodBerthAndMat QryPodBerthAndMat)
        {
            QryPodBerthAndMatAck reponse = new QryPodBerthAndMatAck()
            {
                code = "0",
                message = "成功",
                reqCode = QryPodBerthAndMat.reqCode,
                data = new List<QueryPod>()
                        {
                            new QueryPod()
                            {
                                areaCode = "",
                                materialLot = "",
                                podCode = "100001",
                                mapDataCode = "P02",
                                positionCode = "P02",
                            },
                            new QueryPod()
                            {
                                areaCode = "",
                                materialLot = "",
                                podCode = "100002",
                                mapDataCode = "P03",
                                positionCode = "P03",
                            },
                        },
            };

            return reponse;
        }

        /// <summary>
        /// 倉位禁用與啟用
        /// </summary>
        /// <param name="BlockStageBin"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "blockStgBin")]
        public BlockStageBinAck BlockStgBin(BlockStageBin BlockStageBin)
        {
            BlockStageBinAck reponse = new BlockStageBinAck()
            {
                code = "0",
                message = "成功",
                reqCode = BlockStageBin.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 容器與倉位綁定、解綁
        /// </summary>
        /// <param name="CtnrAndBin"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "bindCtnrAndBin")]
        public CtnrAndBinAck BindCtnrAndBin(CtnrAndBin CtnrAndBin)
        {
            CtnrAndBinAck reponse = new CtnrAndBinAck()
            {
                code = "0",
                data = string.Empty,
                message = "成功",
                reqCode = CtnrAndBin.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 查詢任務狀態
        /// </summary>
        /// <param name="TaskStatus"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "queryTaskStatus")]
        public TaskStatusAck QueryTaskStatus(TaskStatus TaskStatus)
        {
            TaskStatusAck reponse = new TaskStatusAck()
            {
                code = "0",
                message = "成功",
                reqCode = TaskStatus.reqCode,
                data = new List<TaskData>()
                        {
                            new TaskData()
                            {
                                taskCode = "234",
                                taskStatus = "2",
                                agvCode = "",
                                taskTyp = "F01",
                            },
                            new TaskData()
                            {
                                taskCode = "123",
                                taskStatus = "9",
                                agvCode = "",
                                taskTyp = "P01",
                            },
                        },
            };

            return reponse;
        }

        /// <summary>
        /// 查詢 AGV 狀態
        /// </summary>
        /// <param name="AGVStatus"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(AGVStatusRoute + "queryAgvStatus")]
        public AGVStatusAck AGVStatus(AGVStatus AGVStatus)
        {
            AGVStatusAck reponse = new AGVStatusAck()
            {
                code = "0",
                message = "成功",
                reqCode = AGVStatus.reqCode,
                data = new List<AGVStatusData>()
                        {
                            new AGVStatusData()
                            {
                                robotCode = "1",
                                robotDir = "180",
                                robotIp = "",
                                battery = "25",
                                posX = "1.0",
                                posY = "2.0",
                                mapCode = "",
                                speed = "",
                                status = "1",
                                exclType = "0",
                                stop = "1",
                                podCode = "200001",
                                podDir = "90",
                                path = new string[] { "10000,20000,90", "20000,30000,-90", "20000,30000,180", "30000,40000,0" },
                            },
                            new AGVStatusData()
                            {
                                robotCode = "3",
                                robotDir = "180",
                                robotIp = "",
                                battery = "25",
                                posX = "1.0",
                                posY = "2.0",
                                mapCode = _mockAgv3MapCode, // ★ 動態：反映任務完成後的位置
                                speed = "",
                                status = "4",
                                exclType = "0",
                                stop = "1",
                                podCode = "200001",
                                podDir = "90",
                                path = new string[] { },
                            },
                        },
            };

            return reponse;
        }

        /// <summary>
        /// 停止 AGV
        /// </summary>
        /// <param name="StopRobot"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "stopRobot")]
        public StopRobotAck StopRobot(StopRobot StopRobot)
        {
            StopRobotAck reponse = new StopRobotAck()
            {
                code = "0",
                message = "成功",
                reqCode = StopRobot.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 恢復 AGV
        /// </summary>
        /// <param name="ResumeRobot"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "resumeRobot")]
        public ResumeRobotAck ResumeRobot(ResumeRobot ResumeRobot)
        {
            ResumeRobotAck reponse = new ResumeRobotAck()
            {
                code = "0",
                message = "成功",
                reqCode = ResumeRobot.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 區域清空/釋放
        /// </summary>
        /// <param name="BlockArea"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "blockArea")]
        public BlockAreaAck BlockArea(BlockArea BlockArea)
        {
            BlockAreaAck reponse = new BlockAreaAck()
            {
                code = "0",
                message = "成功",
                reqCode = BlockArea.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 預調度對外接口
        /// </summary>
        /// <param name="PreScheduleTask"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "genPreScheduleTask")]
        public PreScheduleTaskAck GenPreScheduleTask(PreScheduleTask PreScheduleTask)
        {
            PreScheduleTaskAck reponse = new PreScheduleTaskAck()
            {
                code = "0",
                message = "成功",
                reqCode = PreScheduleTask.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 清空巷道
        /// </summary>
        /// <param name="ClearRoadWay"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "clearRoadWay")]
        public ClearRoadWayAck ClearRoadWay(ClearRoadWay ClearRoadWay)
        {
            ClearRoadWayAck reponse = new ClearRoadWayAck()
            {
                code = "0",
                message = "成功",
                reqCode = ClearRoadWay.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 料箱出庫 TPS (CTU+分撥牆)
        /// </summary>
        /// <param name="GetOutPod"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "getOutPod")]
        public GetOutPodAck GetOutPod(GetOutPod GetOutPod)
        {
            GetOutPodAck reponse = new GetOutPodAck()
            {
                code = "0",
                message = "成功",
                reqCode = GetOutPod.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 料箱回庫 TPS (CTU+分撥牆)
        /// </summary>
        /// <param name="ReturnPod"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "returnPod")]
        public ReturnPodAck ReturnPod(ReturnPod ReturnPod)
        {
            ReturnPodAck reponse = new ReturnPodAck()
            {
                code = "0",
                message = "成功",
                reqCode = ReturnPod.reqCode,
                data = string.Empty,
            };

            return reponse;
        }

        /// <summary>
        /// 料箱順序出庫 (CTU)
        /// </summary>
        /// <param name="GroupTaskBatch"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "genCtuGroupTaskBatch")]
        public GroupTaskBatchAck GenCtuGroupTaskBatch(GroupTaskBatch GroupTaskBatch)
        {
            GroupTaskBatchAck reponse = new GroupTaskBatchAck()
            {
                code = "0",
                message = "成功",
                reqCode = GroupTaskBatch.reqCode,
            };

            return reponse;
        }

        /// <summary>
        /// 料箱取放回調 (CTU)
        /// </summary>
        /// <param name="BoxApplyPass"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "boxApplyPass")]
        public BoxApplyPassAck BoxApplyPass(BoxApplyPass BoxApplyPass)
        {
            BoxApplyPassAck reponse = new BoxApplyPassAck()
            {
                code = "0",
                message = "成功",
                reqCode = BoxApplyPass.reqCode,
                data = string.Empty,
            };

            return reponse;
        }

        /// <summary>
        /// 指定料箱終點 (CTU)
        /// </summary>
        /// <param name="ContainerDestination"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(Route + "bindCtnrDestination")]
        public ContainerDestinationAck BindCtnrDestination(ContainerDestination ContainerDestination)
        {
            ContainerDestinationAck reponse = new ContainerDestinationAck()
            {
                code = "0",
                message = "成功",
                reqCode = ContainerDestination.reqCode,
                data = string.Empty,
            };

            return reponse;
        }
    }
}