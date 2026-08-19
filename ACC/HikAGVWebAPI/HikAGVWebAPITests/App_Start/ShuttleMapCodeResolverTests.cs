using System.Collections.Generic;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// ShuttleMapCodeResolver 單元測試（對應工作計畫 P0-1、spec.md AC1~AC8）
    ///
    /// 背景：2026-08-19 客訴根因 —— UpdateAGVStatus 逐張地圖輪詢，
    /// Update_oShuttle 的 where 只有 ShuttleId，同輪多筆時後者覆蓋前者，
    /// 現場設定 AA,BB,DD,FF 使 DD(3F) 恆勝 BB(2F)，
    /// 導致車在 2F 卻被判為 3F、跨樓層任務跳過預調度直接派發。
    ///
    /// 測試策略：純類別，無 SQLData / HikAGV / Log 依賴，可直接 new。
    /// Test 回放以 8/19 實際 log 的回報序列驗證修復後不再重演。
    /// </summary>
    [TestClass]
    public class ShuttleMapCodeResolverTests
    {
        private const string Robot = "20106";

        private static AGVStatusData Report(string mapCode, string posX, string posY, string status = "I")
        {
            return new AGVStatusData
            {
                robotCode = Robot,
                mapCode = mapCode,
                posX = posX,
                posY = posY,
                status = status,
                battery = "80",
                robotDir = "0",
            };
        }

        private static IList<AGVStatusData> Reports(params AGVStatusData[] items)
        {
            return new List<AGVStatusData>(items);
        }

        #region Happy Path

        [TestMethod]
        public void Resolve_本輪只有一筆回報_直接採用且無衝突()
        {
            // Arrange
            var resolver = new ShuttleMapCodeResolver();

            // Act
            var result = resolver.Resolve(Robot, Reports(Report("BB", "100", "200")), dbMapCode: "BB");

            // Assert
            Assert.IsNotNull(result.Winner);
            Assert.AreEqual("BB", result.Winner.mapCode);
            Assert.IsFalse(result.HasConflict);
            Assert.AreEqual("BB", result.AcceptedMapCode);
        }

        [TestMethod]
        public void Resolve_同輪多筆_取座標有變動者為勝出並標記衝突()
        {
            // Arrange：先建立上一輪基準（BB=100,200 / DD=500,600）
            var resolver = new ShuttleMapCodeResolver();
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "BB");

            // Act：BB 座標變動、DD 凍結
            var result = resolver.Resolve(Robot, Reports(Report("BB", "111", "222"), Report("DD", "500", "600")), "BB");

            // Assert
            Assert.IsTrue(result.HasConflict);
            Assert.AreEqual("BB", result.Winner.mapCode);
        }

        [TestMethod]
        public void Resolve_衝突描述含各筆明細與勝出原因()
        {
            // Arrange
            var resolver = new ShuttleMapCodeResolver();
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "BB");

            // Act
            var result = resolver.Resolve(Robot, Reports(Report("BB", "111", "222"), Report("DD", "500", "600")), "BB");

            // Assert
            Assert.IsTrue(result.HasConflict);
            StringAssert.Contains(result.ConflictDetail, "BB");
            StringAssert.Contains(result.ConflictDetail, "DD");
            StringAssert.Contains(result.ConflictDetail, "500");
            // 各筆的判定結果（有變動／凍結）與勝出原因都要在描述裡，
            // 否則現場翻 WARN log 只看得到「有衝突」卻不知道為什麼選了這一筆
            StringAssert.Contains(result.ConflictDetail, "座標凍結");
            StringAssert.Contains(result.ConflictDetail, "座標有變動");
            StringAssert.Contains(result.ConflictDetail, "勝出=BB");
        }

        #endregion Happy Path

        #region Edge Case — 去重規則

        [TestMethod]
        public void Resolve_同輪多筆皆凍結_黏著上一輪認可值()
        {
            // Arrange：認可值為 BB，且 BB/DD 兩張地圖都先建立基準
            //（若只建 BB 的基準，下一輪 DD 會因「首次出現」被視為有變動而勝出，
            //  那會變成在測「新地圖出現」而非本案例要測的「皆凍結」）
            var resolver = new ShuttleMapCodeResolver();
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "BB");

            // Act：兩筆座標都與上一輪相同
            var result = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "BB");

            // Assert
            Assert.AreEqual("BB", result.Winner.mapCode);
        }

        [TestMethod]
        public void Resolve_同輪多筆皆變動_黏著上一輪認可值()
        {
            // Arrange：認可值為 BB，且 BB/DD 兩張地圖都先建立基準（理由同上一個案例）
            var resolver = new ShuttleMapCodeResolver();
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "BB");

            // Act：兩筆座標都變動
            var result = resolver.Resolve(Robot, Reports(Report("BB", "111", "222"), Report("DD", "555", "666")), "BB");

            // Assert
            Assert.AreEqual("BB", result.Winner.mapCode);
        }

        [TestMethod]
        public void Resolve_地圖首次出現_視為有變動而勝出()
        {
            // Arrange：只有 DD 有基準，且 DD 凍結
            var resolver = new ShuttleMapCodeResolver();
            resolver.Resolve(Robot, Reports(Report("DD", "500", "600")), "DD");

            // Act：BB 首次出現，DD 凍結
            var result = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");

            // Assert：勝出者應為新出現的 BB（但因需連續確認，AcceptedMapCode 仍為 DD）
            Assert.AreEqual("BB", result.Winner.mapCode);
            Assert.AreEqual("DD", result.AcceptedMapCode);
        }

        [TestMethod]
        public void Resolve_黏著目標本輪未回報_MapCode維持不變更()
        {
            // Arrange：認可值為 FF
            var resolver = new ShuttleMapCodeResolver();
            resolver.Resolve(Robot, Reports(Report("FF", "900", "900")), "FF");
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "FF");

            // Act：本輪只有 BB/DD 且皆凍結，認可的 FF 沒回報
            var result = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "FF");

            // Assert
            Assert.AreEqual("FF", result.AcceptedMapCode);
        }

        #endregion Edge Case — 去重規則

        #region Boundary — 連續確認

        [TestMethod]
        public void Resolve_MapCode變更需連續三次確認才生效()
        {
            // Arrange：認可值為 DD
            var resolver = new ShuttleMapCodeResolver(confirmCount: 3);
            resolver.Resolve(Robot, Reports(Report("DD", "500", "600")), "DD");

            // Act & Assert：連續三輪勝出者皆為 BB
            var r1 = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");
            Assert.AreEqual("DD", r1.AcceptedMapCode, "第 1 輪不得生效");
            Assert.AreEqual(1, r1.PendingCount);

            var r2 = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");
            Assert.AreEqual("DD", r2.AcceptedMapCode, "第 2 輪不得生效");
            Assert.AreEqual(2, r2.PendingCount);

            var r3 = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");
            Assert.AreEqual("BB", r3.AcceptedMapCode, "第 3 輪應生效");
            Assert.IsTrue(r3.MapCodeAccepted);
        }

        [TestMethod]
        public void Resolve_確認中途換成別的MapCode_重新計數()
        {
            // Arrange：認可值 DD，pending BB 已累積 2 次
            var resolver = new ShuttleMapCodeResolver(confirmCount: 3);
            resolver.Resolve(Robot, Reports(Report("DD", "500", "600")), "DD");
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");

            // Act：勝出者換成 FF（首次出現視為有變動，DD 凍結、BB 也凍結）
            var result = resolver.Resolve(Robot, Reports(Report("FF", "900", "900"), Report("DD", "500", "600")), "DD");

            // Assert
            Assert.AreEqual("DD", result.AcceptedMapCode, "換目標後不得直接生效");
            Assert.AreEqual(1, result.PendingCount, "計數應重設為 1");
        }

        #endregion Boundary — 連續確認

        #region Edge Case — callback 同步

        [TestMethod]
        public void Resolve_DB現值與內部認可值不同_以DB為準並清空計數()
        {
            // Arrange：內部認可 DD，pending BB 累積 2 次
            var resolver = new ShuttleMapCodeResolver(confirmCount: 3);
            resolver.Resolve(Robot, Reports(Report("DD", "500", "600")), "DD");
            resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");
            var before = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "DD");
            Assert.AreEqual(2, before.PendingCount);

            // Act：callback 已把 DB 寫成 FF
            var result = resolver.Resolve(Robot, Reports(Report("BB", "100", "200"), Report("DD", "500", "600")), "FF");

            // Assert：以 DB 的 FF 為新認可值，pending 應清空後重新起算
            Assert.AreEqual("FF", result.AcceptedMapCode);
            Assert.AreEqual(1, result.PendingCount, "同步後 pending 應重新起算");
        }

        #endregion Edge Case — callback 同步

        #region Error

        [TestMethod]
        public void Resolve_空回報清單_回傳Null且不拋例外()
        {
            // Arrange
            var resolver = new ShuttleMapCodeResolver();

            // Act
            var result = resolver.Resolve(Robot, Reports(), dbMapCode: "BB");

            // Assert
            Assert.IsNull(result.Winner);
            Assert.IsFalse(result.HasConflict);
        }

        #endregion Error

        #region Regression — 2026-08-19 實戰序列回放

        /// <summary>
        /// 以 2026-08-19 11:29:04~11:29:24 AGVDispatch log 的實際回報逐輪餵入。
        /// 事故當時 DB MapCode 全程為 DD(3F)，導致 11:29:24 派發 J1→G1 時
        /// CrossFloorManager 誤判同樓層、跳過預調度。
        /// 修復後應自 11:29:08 起認可 BB(2F)，且 11:29:24 仍為 BB。
        /// </summary>
        [TestMethod]
        public void Resolve_回放20260819事故序列_派發當下應認可BB而非DD()
        {
            // Arrange
            var resolver = new ShuttleMapCodeResolver(confirmCount: 3);

            // 11:28:50~11:29:04 只有 DD 回報，車確實在 3F 移動後停住
            resolver.Resolve(Robot, Reports(Report("DD", "253664", "207568", "R")), "DD");
            resolver.Resolve(Robot, Reports(Report("DD", "254308", "207558", "R")), "DD");
            resolver.Resolve(Robot, Reports(Report("DD", "254312", "207558", "R")), "DD");
            var atStop = resolver.Resolve(Robot, Reports(Report("DD", "254312", "207558")), "DD");
            Assert.AreEqual("DD", atStop.AcceptedMapCode, "車尚未到 2F 前應維持 DD");

            // Act：11:29:05 起 BB 出現（車真的到 2F），DD 凍結在 254312
            var t0505 = resolver.Resolve(Robot, Reports(Report("BB", "271005", "269994"), Report("DD", "254312", "207558")), "DD");
            var t0506 = resolver.Resolve(Robot, Reports(Report("BB", "271005", "269994"), Report("DD", "254312", "207558")), "DD");
            var t0508 = resolver.Resolve(Robot, Reports(Report("BB", "271005", "269994"), Report("DD", "254312", "207558")), "DD");

            // Assert：第 3 輪（11:29:08）應翻成 BB
            Assert.AreEqual("DD", t0505.AcceptedMapCode);
            Assert.AreEqual("DD", t0506.AcceptedMapCode);
            Assert.AreEqual("BB", t0508.AcceptedMapCode, "11:29:08 應已認可 BB(2F)");

            // 11:29:11~11:29:13 BB 持續移動，DD 仍凍結
            resolver.Resolve(Robot, Reports(Report("BB", "270749", "269995", "R"), Report("DD", "254312", "207558")), "BB");
            resolver.Resolve(Robot, Reports(Report("BB", "270244", "269997", "R"), Report("DD", "254312", "207558")), "BB");

            // 11:29:14 DD 鏡像 BB 座標後再度凍結（兩筆同時變動 → 黏著 BB）
            resolver.Resolve(Robot, Reports(Report("BB", "270072", "269997", "R"), Report("DD", "270072", "269997", "R")), "BB");
            resolver.Resolve(Robot, Reports(Report("BB", "269814", "270015", "R"), Report("DD", "270072", "269997", "R")), "BB");
            resolver.Resolve(Robot, Reports(Report("BB", "269189", "269990", "R"), Report("DD", "270072", "269997", "R")), "BB");

            // 11:29:24 派發當下
            var atDispatch = resolver.Resolve(Robot, Reports(Report("BB", "269147", "269989", "R"), Report("DD", "270072", "269997", "R")), "BB");

            // Assert：本次客訴的關鍵斷言
            Assert.AreEqual("BB", atDispatch.AcceptedMapCode, "11:29:24 派發時必須讀到 BB(2F)，不得為 DD(3F)");
            Assert.AreEqual("BB", atDispatch.Winner.mapCode);
        }

        #endregion Regression — 2026-08-19 實戰序列回放
    }
}
