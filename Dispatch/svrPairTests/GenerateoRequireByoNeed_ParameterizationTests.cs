using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace svrPair.Tests
{
    /// <summary>
    /// Layer A — 參數化正確性 + Layer C — Happy path 回歸
    /// （對應工作計畫 T6 / spec.md「SQL 參數化正確性」「Happy path 回歸」）。
    ///
    /// 紅燈基準：T6（cPair 全檔參數化）實作前，含單引號的工單會打斷拼接 SQL → 拋例外 → 測試失敗（Red）。
    /// 綠燈目標：T6 完成後，特殊字元原值完整 round-trip、不拋例外、注入字串無害化 → 通過（Green）。
    ///
    /// 前置條件（由 IntegrationTestBase.SeedTestPort 提供，需於 T1 補齊）：
    /// oPort 內 A91/B92 皆 UseFlag='Y'、BgnToEnd 空，使配對筆能通過
    /// CheckoPortBgnToEndIsNullAndUseFlagAsY 進到 INSERT oRequire。
    /// </summary>
    [TestClass]
    public class GenerateoRequireByoNeed_ParameterizationTests : IntegrationTestBase
    {
        private cPair _cPair;

        [TestInitialize]
        public void Setup()
        {
            _cPair = new cPair();
            _cPair.Initial();
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_WorkOrderWithSingleQuote_PreservedAndNoThrow()
        {
            // Arrange — 單引號是這次閃退的元兇字元
            const string wo = "O'Brien";
            string t = SeedPairRow(idx: 1, workOrder: wo, rackId: "R001");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert — oRequire 出現該筆且 WorkOrder 原值完整
            var stored = Scalar(
                "SELECT WorkOrder FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.IsNotNull(stored, "含單引號工單應成功產生 oRequire（改前此處會拋例外）");
            Assert.AreEqual(wo, stored.ToString(), "WorkOrder 原值應完整保存");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_SqlInjectionString_NeutralisedAndTableSurvives()
        {
            // ⚠️ 安全鐵則：此測試在 T6 參數化「尚未」實作時會以紅燈失敗（屬預期），
            //    但 payload 絕不可帶 ';' / DROP / DELETE / UPDATE 等可形成第二條語句的內容，
            //    否則字串拼接版的 cPair 會真的執行破壞性語句（曾因 'x');DROP TABLE oRequire;--' 真的 drop 掉 oRequire）。
            //    這裡採用「含單引號 + OR」的注入字串：拼接時只會造成語法錯誤（不執行），參數化後則原樣存入。
            const string wo = "x' OR '1'='1";
            string t = SeedPairRow(idx: 1, workOrder: wo, rackId: "R002");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert ①：原值被當純文字存入（證明已中和、未被當 SQL 解釋）
            var stored = Scalar(
                "SELECT WorkOrder FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual(wo, (stored ?? "").ToString(), "注入字串應原樣存入、未被執行");

            // Assert ②：oRequire 資料表仍存在
            var exists = Scalar("SELECT OBJECT_ID('oRequire')");
            Assert.IsTrue(exists != null && exists != System.DBNull.Value, "oRequire 表必須仍存在");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_RackIdWithSingleQuote_PreservedAndNoThrow()
        {
            // P1
            const string rack = "RK'01";
            string t = SeedPairRow(idx: 1, workOrder: "UTESTWO", rackId: rack);

            _cPair.GenerateoRequireByoNeed();

            var stored = Scalar(
                "SELECT RackId FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual(rack, (stored ?? "").ToString(), "含單引號 RackId 原值應完整保存");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_NormalRow_ProducesoRequireAndFlags()
        {
            // Layer C — Happy path 回歸：確保參數化沒改壞正常路徑
            string t = SeedPairRow(idx: 1, workOrder: "UTESTWO", rackId: "R001");

            _cPair.GenerateoRequireByoNeed();

            // oRequire 正確產生
            var cnt = (int)Scalar(
                "SELECT COUNT(*) FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual(1, cnt, "正常筆應產生一筆 oRequire");

            // oNeed 標 Y
            var flag = Scalar("SELECT AssignFlag FROM oNeed WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual("Y", (flag ?? "").ToString().Trim(), "正常筆 oNeed 應標 Y");

            // oPort 已註冊 BgnToEnd
            var bgn = Scalar("SELECT BgnToEnd FROM oPort WHERE StationNo = @s", P("@s", TEST_BGN));
            Assert.AreEqual(TEST_BGN + ">" + TEST_END, (bgn ?? "").ToString().Trim(), "oPort 應註冊路徑");
        }

        // ── fixtures ─────────────────────────────────────────────

        /// <summary>種一筆 A91→B92 配對 oNeed（WorkOrder 非空，確保不走空工單取消路徑）。</summary>
        private string SeedPairRow(int idx, string workOrder, string rackId)
        {
            string t = MakeTaskDateTime(idx);
            ExecNonQuery(
                "INSERT INTO oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) " +
                "VALUES(@obj, @rack, @wo, @end, @src, @t)",
                P("@obj", TEST_BGN), P("@rack", rackId), P("@wo", workOrder),
                P("@end", TEST_END), P("@src", UTEST_SOURCE), P("@t", t));
            return t;
        }

        [TestCleanup]
        public void TearDown()
        {
            if (_cPair != null) _cPair.EndPair();
        }
    }
}
