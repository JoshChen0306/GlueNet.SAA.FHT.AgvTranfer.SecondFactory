using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace svrPair.Tests
{
    /// <summary>
    /// Layer B — 韌性測試（對應工作計畫 T2 / spec.md「診斷強化 + 單筆隔離」）。
    ///
    /// 利用 schema 天然炸點：oNeed.RackId = nvarchar(50)，oRequire.RackId = nvarchar(20)。
    /// 一筆 RackId 30 字的 oNeed 對 oNeed 合法、塞進 oRequire 會 truncation 例外，
    /// 「就算參數化也照炸」→ 真實驗證 try/catch 是否真的隔離該筆、不拖垮整輪。
    ///
    /// 紅燈基準：在 T2 實作前，GenerateoRequireByoNeed 會直接拋例外（甚至閃退）→ 測試失敗（Red）。
    /// 綠燈目標：T2 完成後，方法不拋例外、毒筆標 X、好筆續成 → 測試通過（Green）。
    /// </summary>
    [TestClass]
    public class GenerateoRequireByoNeed_ResilienceTests : IntegrationTestBase
    {
        private cPair _cPair;

        [TestInitialize]
        public void Setup()
        {
            _cPair = new cPair();
            _cPair.Initial();
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_PoisonRowThenValidRow_DoesNotThrow()
        {
            // Arrange
            SeedPoisonRow(idx: 1);   // RackId 30 字（oRequire 容不下）
            SeedValidRow(idx: 2);    // 正常筆，應仍被處理

            // Act + Assert：不得向外拋例外
            _cPair.GenerateoRequireByoNeed();
            // 走到這行未拋例外即代表第 1 層 try/catch 生效
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_PoisonRow_IsQuarantinedAsX_ValidRowStillProcessed()
        {
            // Arrange
            string poisonTaskTime = SeedPoisonRow(idx: 1);
            SeedValidRow(idx: 2);

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert ①：毒筆被隔離標 X
            var flag = Scalar(
                "SELECT AssignFlag FROM oNeed WHERE TaskDateTime = @t",
                P("@t", poisonTaskTime));
            Assert.AreEqual("X", (flag ?? "").ToString().Trim(), "毒資料應被隔離標記為 X");

            // Assert ②：正常筆仍成功產生 oRequire（毒筆未拖垮整輪）
            // 正常筆使用第二組站號 A93（避免與毒筆 A91 站號碰撞而被 UpdateoNeedAssignFlag 互相覆寫）。
            var cnt = (int)Scalar(
                "SELECT COUNT(*) FROM oRequire WHERE ObjStation = @s",
                P("@s", TEST_BGN2));
            Assert.IsTrue(cnt >= 1, "毒筆之後的正常筆仍應被處理、產生 oRequire");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_OnException_LogsOffendingRowDetail()
        {
            // P1：驗證 log 內含 ObjStation/EndStation/WorkOrder/RackId 與例外訊息（非空 catch）
            // TODO[T2]: 視 LogManager 是否可在測試注入/攔截 sink 而定；
            //           若不易攔截，改以「人工檢視 log」列入手動驗收，並在此標 Assert.Inconclusive。
            Assert.Inconclusive("待 T2 決定 log 攔截方式後補實作。");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_TransientConnectionError_DoesNotQuarantine()
        {
            // P2：連線類例外應保留重試、不標 X
            // TODO[T2]: 連線類例外較難用真實 DB 穩定觸發；
            //           實作時可考慮（a）暫時性指向不可達 IP 製造連線逾時，或
            //           （b）將例外分類邏輯抽成可單測的純方法後改用單元測試覆蓋。
            Assert.Inconclusive("待 T2 決定連線類例外模擬手法後補實作。");
        }

        // ── fixtures ─────────────────────────────────────────────

        /// <summary>毒筆：RackId 30 字，對 oNeed(nvarchar50) 合法、對 oRequire(nvarchar20) 會 truncation。</summary>
        private string SeedPoisonRow(int idx)
        {
            string t = MakeTaskDateTime(idx);
            string rack30 = new string('R', 30);
            // TODO[T2]: 確認 oNeed 必填欄位齊全；WorkOrder 須非空（A 區空工單會走取消路徑到不了 INSERT）
            ExecNonQuery(
                "INSERT INTO oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) " +
                "VALUES(@obj, @rack, @wo, @end, @src, @t)",
                P("@obj", TEST_BGN), P("@rack", rack30), P("@wo", "UTESTWO"),
                P("@end", TEST_END), P("@src", UTEST_SOURCE), P("@t", t));
            return t;
        }

        /// <summary>正常筆：合法長度、使用第二組站號 A93→B94（與毒筆站號不碰撞），應成功產生 oRequire。</summary>
        private string SeedValidRow(int idx)
        {
            string t = MakeTaskDateTime(idx);
            ExecNonQuery(
                "INSERT INTO oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) " +
                "VALUES(@obj, @rack, @wo, @end, @src, @t)",
                P("@obj", TEST_BGN2), P("@rack", "R001"), P("@wo", "UTESTWO"),
                P("@end", TEST_END2), P("@src", UTEST_SOURCE), P("@t", t));
            return t;
        }

        [TestCleanup]
        public void TearDown()
        {
            if (_cPair != null) _cPair.EndPair();
        }
    }
}
