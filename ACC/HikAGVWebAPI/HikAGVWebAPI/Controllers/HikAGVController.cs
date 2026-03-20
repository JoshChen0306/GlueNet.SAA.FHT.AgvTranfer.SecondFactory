using HikAGVWebAPI.App_Start;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Web.Http;
using System.Configuration;
using System.Collections.Generic;
using System.Net.Http.Headers;
using HikAGVDll;
using System.Linq;

namespace HikAGVWebAPI
{
    [Route("[controller]")]
    public partial class HikAGVController : ApiController
    {
        private readonly Configuration config;//抓取 Config 檔案資料
        private readonly string ConfigFileName = string.Format("{0}\\Config\\FHtSetting.config", string.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath) ? AppDomain.CurrentDomain.BaseDirectory : AppDomain.CurrentDomain.RelativeSearchPath);
        private DBSettings DBSettings = new DBSettings();
        private LogSettings LogSettings = new LogSettings();
        private FHtSettings FHtSettings = new FHtSettings();
        private HikAGV hikAGV = new HikAGV();//海康接口
        private List<string> WarnContent => hikAGV.AGVSettings.WarnContent.Split(',').ToList();

        private SQLData mDB;//SQL Server 連線
        private Log mLog;//AGV 接口 Log 路徑
        private const string CallbackRoute = "agv/agvCallbackService/";//Callback Route
        private const string Route = "rcms/services/rest/hikRpcService/";//API Route
        private const string AGVStatusRoute = "rcms-dps/rest/";//API AGV Status Route
        private static readonly HttpClient client = new HttpClient();//上拋客戶端

        public HikAGVController()
        {
            config = LoadExternalConfig(ConfigFileName);
            ReadDBConfig();
            InitialData();
        }

        /// <summary>
        /// 初始化資料
        /// </summary>
        private void InitialData()
        {
            try
            {
                mDB = new SQLData(DBSettings.DBName, DBSettings.DBIP);//SQL Server 連線
                mLog = new Log(LogSettings.LogPath, "AGVAPI");
                mLog.KeepDate = LogSettings.KeepDate;
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
            }
            catch
            {
            }
        }
        #endregion 依照設定檔讀取 Section 資料

        [HttpGet]
        [Route("Hello")]
        public string Hello()
        {
            //mLog.TraceOut("Hello", Log.LogType.NONE);
            return "Hello";
        }

        protected internal string PostData(Dictionary<string, string> Data)
        {
            if (FHtSettings.TestMode == "true")
            {
                mLog.TraceOut($"[TestMode] PostData skipped (FHtSettings.TestMode=true)", Log.LogType.NONE);
                return "TestMode";
            }

            HttpResponseMessage response = null;

            try
            {
                HttpContent content = new FormUrlEncodedContent(Data);
                content.Headers.ContentType = new MediaTypeHeaderValue(FHtSettings.ContentType);
                Task <HttpResponseMessage> task = Task.Run(() => client.PostAsync(FHtSettings.WebURL, content));
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

        //[HttpPost]
        //[Route("CallShuttle")]
        //public ResultModel CallShuttle(LotInfoModel LotInfo)
        //{
        //    return agv.CallShuttle(LotInfo);
        //}
    }
}