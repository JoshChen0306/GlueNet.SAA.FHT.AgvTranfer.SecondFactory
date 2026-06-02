using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace svrPair.Tests
{
    /// <summary>
    /// 整合測試基礎建設：
    /// - 連線目標來自測試 bin 目錄的 Recipe.ini（應指向 DESKTOP-2I3FKA2 開發機）
    /// - 安全防呆：若連線指向 127.0.0.1（現場）一律 Assert.Inconclusive 拒跑
    /// - 隔離：測試資料一律 TaskSource = 'UTEST'，測試專屬站號 A91（起點，平板群組）/ B92（終點）
    /// - TestCleanup 無論斷言成敗一律清除，跑幾次都乾淨、可重複
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected const string UTEST_SOURCE = "UTEST";
        protected const string TEST_BGN = "A91"; // 'A' 屬平板群組 → 會進 05.處理平板配對 → ProcessoNeedToRequire
        protected const string TEST_END = "B92";

        protected string ConnectionString { get; private set; }

        [TestInitialize]
        public void BaseSetup()
        {
            var (dbIp, dbName) = ReadRecipe();

            // ★ 安全防呆：嚴禁對現場資料庫（127.0.0.1）建刪測試資料
            if (dbIp.Trim() == "127.0.0.1" || dbIp.Trim().ToLower() == "localhost")
            {
                Assert.Inconclusive(
                    "偵測到連線指向現場 DB (" + dbIp + ")，整合測試拒跑。請將 Recipe.ini 指向開發機 DESKTOP-2I3FKA2。");
            }

            ConnectionString =
                $"Data Source={dbIp};Initial Catalog={dbName};Persist Security Info=True;User ID=mcs;Password=Zz123456;Connect Timeout=10";

            CleanupTestData();   // 先清殘留，確保乾淨起點
            SeedTestPort();      // 種測試站
        }

        [TestCleanup]
        public void BaseCleanup()
        {
            // 無論斷言成敗一律清除
            CleanupTestData();
        }

        private (string dbIp, string dbName) ReadRecipe()
        {
            // cPair.Initial() 讀的是 current directory 的 Recipe.ini，測試端讀同一份以保持一致
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Recipe.ini");
            if (!File.Exists(path))
                Assert.Inconclusive("找不到 Recipe.ini：" + path);

            string dbIp = "", dbName = "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith("DbIp=")) dbIp = line.Substring("DbIp=".Length).Trim();
                else if (line.StartsWith("DbName=")) dbName = line.Substring("DbName=".Length).Trim();
            }
            return (dbIp, dbName);
        }

        /// <summary>
        /// 種測試站 A91/B92 到 oPort：UseFlag='Y'、BgnToEnd 空、HaveFlag 由各測試自行 Update 調整。
        /// oPort 無 TaskSource 欄，cleanup 以 StationNo 精準刪除。
        /// </summary>
        protected void SeedTestPort()
        {
            // TODO[T1]: 依 oPort 實際必填欄位（Area/Block/Port/StationNo/UseFlag/HaveFlag/Priority...）補齊 INSERT
            // 注意：A91 Block='A'、B92 Block='B'；UseFlag='Y'、BgnToEnd=NULL；確保 B92 無 oPortBinding 綁定
            // ExecNonQuery("INSERT INTO oPort(StationNo, Area, Block, Port, UseFlag, HaveFlag, Priority, BgnToEnd) VALUES(@s,...,NULL)", ...);
        }

        /// <summary>清除所有測試痕跡：UTEST 來源的 oNeed/oRequire/oMission + 測試站 oPort。</summary>
        protected void CleanupTestData()
        {
            ExecNonQuery("DELETE FROM oRequire WHERE TaskSource = @src", P("@src", UTEST_SOURCE));
            ExecNonQuery("DELETE FROM oMission WHERE TaskSource = @src", P("@src", UTEST_SOURCE));
            ExecNonQuery("DELETE FROM oNeed    WHERE TaskSource = @src", P("@src", UTEST_SOURCE));
            ExecNonQuery("DELETE FROM oPort    WHERE StationNo IN (@a, @b)", P("@a", TEST_BGN), P("@b", TEST_END));
        }

        // ── 測試用小工具（一律參數化，示範正確寫法）───────────────────────

        protected static SqlParameter P(string name, object value) => new SqlParameter(name, value ?? DBNull.Value);

        protected int ExecNonQuery(string sql, params SqlParameter[] ps)
        {
            using (var c = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, c))
            {
                if (ps != null) cmd.Parameters.AddRange(ps);
                c.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        protected DataTable Query(string sql, params SqlParameter[] ps)
        {
            using (var c = new SqlConnection(ConnectionString))
            using (var da = new SqlDataAdapter(sql, c))
            {
                if (ps != null) da.SelectCommand.Parameters.AddRange(ps);
                var dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        protected object Scalar(string sql, params SqlParameter[] ps)
        {
            using (var c = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, c))
            {
                if (ps != null) cmd.Parameters.AddRange(ps);
                c.Open();
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>產生 UTEST 專屬、含時間的 TaskDateTime（純數字字串）。idx 用來保證同測試內排序。</summary>
        protected static string MakeTaskDateTime(int idx)
            => DateTime.Now.ToString("yyyyMMddHHmmssfff") + (900000 + idx).ToString();
    }
}
