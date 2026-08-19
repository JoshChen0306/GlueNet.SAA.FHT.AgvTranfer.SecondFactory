using System.Collections.Generic;
using HikAGVWebAPI;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// 車輛樓層可信度閘門的單元測試（對應工作計畫 P0-3、spec.md AC9~AC11）
    ///
    /// 背景：2026-08-19 客訴中，oShuttle.MapCode 被幽靈回報覆蓋成 DD(3F)，
    /// DecideNextCrossFloorAction 因此把起點在 3F 的 J1→G1 判為同樓層直接派發，
    /// 車實際停在 2F，RCS 找不到車可指派，任務卡在 OkFlag=R 達 19 分鐘。
    ///
    /// 除了修正資料來源（P0-1/P0-2），決策層也要有閘門：樓層取不到就不派。
    /// 另修正既有缺陷：currentFloor 與 GetFloor(BeginStation) 同時為 null 時，
    /// 原本的 == 比較會因 null == null 成立而誤判為同樓層。
    ///
    /// 測試策略：
    ///   - DecideNextCrossFloorAction 為 public static 純函數，可直接呼叫
    ///   - SelectNextMissionForFloor 為本次抽出的測試接縫，樓層由外部給定，
    ///     不需 SQLData（既有測試皆以 mDB: null 建構 CrossFloorManager）
    /// </summary>
    [TestClass]
    public class CrossFloorManagerFloorTrustTests
    {
        private ElevatorPathCalculator _pathCalculator;

        [TestInitialize]
        public void Setup()
        {
            _pathCalculator = new ElevatorPathCalculator(new ElevatorSettings());
        }

        private static CrossFloorManager CreateManager()
        {
            return new CrossFloorManager(
                shuttleId: "3",
                idleReturnTimeoutSeconds: 60,
                idleReturnFloor: "1F",
                mapCodeFloorMapping: "AA:1F,BB:2F,DD:3F,FF:4F",
                mDB: null,
                mLog: new Log(),
                hikAGV: null,
                elevatorSettings: new ElevatorSettings());
        }

        private static oMissionModel Mcs(string taskDateTime, string begin, string end)
        {
            return new oMissionModel
            {
                TaskDateTime = taskDateTime,
                BeginStation = begin,
                EndStation = end,
                TaskSource = "MCS",
            };
        }

        #region AC9 — 樓層為 null 不得誤判同樓層

        [TestMethod]
        public void Decide_車輛樓層與站點樓層皆無法判定_不得回傳SameFloor()
        {
            // Arrange：Z 不在 StationFloorMapping 中，GetFloor("Z9") 回 null；
            //          currentFloor 也為 null → 原本的 null == null 會誤判為同樓層
            var pending = new List<oMissionModel> { Mcs("T1", "Z9", "Z1") };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: null, pathCalculator: _pathCalculator, cooldownTracker: null);

            // Assert
            Assert.AreNotEqual(CrossFloorDecisionKind.SameFloor, decision.Kind,
                "currentFloor 與站點樓層皆為 null 時不得判為同樓層");
            Assert.AreEqual(CrossFloorDecisionKind.None, decision.Kind);
        }

        [TestMethod]
        public void Decide_車輛樓層無法判定但站點樓層已知_不得回傳SameFloor也不得預調度()
        {
            // Arrange：J1 為 3F，但車輛樓層取不到
            var pending = new List<oMissionModel> { Mcs("T1", "J1", "G1") };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: null, pathCalculator: _pathCalculator, cooldownTracker: null);

            // Assert：樓層不明時連預調度都不能產生（起點樓層算錯會派出錯誤路徑）
            Assert.AreEqual(CrossFloorDecisionKind.None, decision.Kind);
        }

        [TestMethod]
        public void Decide_車輛樓層為空字串_不得回傳SameFloor()
        {
            // Arrange
            var pending = new List<oMissionModel> { Mcs("T1", "Z9", "Z1") };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "", pathCalculator: _pathCalculator, cooldownTracker: null);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.None, decision.Kind);
        }

        #endregion AC9 — 樓層為 null 不得誤判同樓層

        #region AC10 — 樓層不可信時不派發

        [TestMethod]
        public void SelectNextMissionForFloor_樓層不可信_回傳Null()
        {
            // Arrange
            var manager = CreateManager();
            var pending = new List<oMissionModel> { Mcs("T1", "J1", "G1") };

            // Act：MapCode 不在對照表（例如 XX）→ 樓層取不到
            var result = manager.SelectNextMissionForFloor(pending, currentFloor: null, mapCodeForLog: "XX");

            // Assert
            Assert.IsNull(result, "樓層不可信時不得派發任何 MCS 任務");
        }

        [TestMethod]
        public void SelectNextMissionForFloor_樓層不可信_不得產生預調度()
        {
            // Arrange：車在 2F 的任務與 3F 的任務並存，若誤走預調度會寫入 oMission
            var manager = CreateManager();
            var pending = new List<oMissionModel>
            {
                Mcs("T1", "H3", "K1"),
                Mcs("T2", "J1", "G1"),
            };

            // Act
            var result = manager.SelectNextMissionForFloor(pending, currentFloor: null, mapCodeForLog: "XX");

            // Assert：回傳 null 代表未走到 DispatchCrossFloor，
            // 決策層亦須為 None（DispatchCrossFloor 才是寫 oMission 的唯一入口）
            Assert.IsNull(result);
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: null, pathCalculator: _pathCalculator, cooldownTracker: null);
            Assert.AreEqual(CrossFloorDecisionKind.None, decision.Kind);
        }

        #endregion AC10 — 樓層不可信時不派發

        #region AC11 — 系統任務不受樓層可信度影響

        [TestMethod]
        public void SelectNextMissionForFloor_樓層不可信但有預調度系統任務_仍回傳該系統任務()
        {
            // Arrange
            var manager = CreateManager();
            var systemTask = new oMissionModel
            {
                TaskDateTime = "SYS1",
                BeginStation = "Y1",
                EndStation = "X1",
                TaskSource = CrossFloorManager.CROSS_FLOOR_DISPATCH,
            };
            var pending = new List<oMissionModel> { Mcs("T1", "J1", "G1"), systemTask };

            // Act
            var result = manager.SelectNextMissionForFloor(pending, currentFloor: null, mapCodeForLog: "XX");

            // Assert：系統任務已寫入 oMission，與樓層可信度無關
            Assert.IsNotNull(result);
            Assert.AreEqual("SYS1", result.TaskDateTime);
        }

        [TestMethod]
        public void SelectNextMissionForFloor_樓層不可信但有歸位系統任務_仍回傳該系統任務()
        {
            // Arrange
            var manager = CreateManager();
            var idleReturn = new oMissionModel
            {
                TaskDateTime = "SYS2",
                BeginStation = "V1",
                EndStation = "Y1",
                TaskSource = CrossFloorManager.IDLE_RETURN,
            };
            var pending = new List<oMissionModel> { idleReturn };

            // Act
            var result = manager.SelectNextMissionForFloor(pending, currentFloor: null, mapCodeForLog: "XX");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("SYS2", result.TaskDateTime);
        }

        #endregion AC11 — 系統任務不受樓層可信度影響

        #region Regression — 樓層可信時行為不變

        [TestMethod]
        public void Decide_樓層可信時同樓層任務仍優先派發()
        {
            // Arrange：H3 為 2F，J1 為 3F，車在 2F
            var pending = new List<oMissionModel>
            {
                Mcs("T1", "J1", "G1"),
                Mcs("T2", "H3", "K1"),
            };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "2F", pathCalculator: _pathCalculator, cooldownTracker: null);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.SameFloor, decision.Kind);
            Assert.AreEqual("H3", decision.Task.BeginStation);
        }

        /// <summary>
        /// 2026-08-19 客訴情境：車實際在 2F，待派清單含 3F 起點的 J1→G1 與 2F 起點的 H3→K1。
        /// 修復後樓層資料正確（2F），應優先派 H3→K1，而非把 J1→G1 誤判為同樓層。
        /// </summary>
        [TestMethod]
        public void Decide_車在2F時不得將3F起點任務判為同樓層()
        {
            // Arrange
            var pending = new List<oMissionModel>
            {
                Mcs("20260819110344501485", "H3", "K1"),
                Mcs("20260819111742286442", "J1", "G1"),
            };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "2F", pathCalculator: _pathCalculator, cooldownTracker: null);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.SameFloor, decision.Kind);
            Assert.AreEqual("H3", decision.Task.BeginStation, "車在 2F 應派 2F 起點的 H3→K1，不得派 3F 起點的 J1→G1");
        }

        [TestMethod]
        public void SelectNextMissionForFloor_樓層可信時維持原有派發行為()
        {
            // Arrange
            var manager = CreateManager();
            var pending = new List<oMissionModel> { Mcs("T1", "H3", "K1") };

            // Act
            var result = manager.SelectNextMissionForFloor(pending, currentFloor: "2F", mapCodeForLog: "BB");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("H3", result.BeginStation);
        }

        #endregion Regression — 樓層可信時行為不變
    }
}
