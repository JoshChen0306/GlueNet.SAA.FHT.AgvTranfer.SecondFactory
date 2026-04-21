using System.Collections.Generic;
using HikAGVWebAPI;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// SelectNextMission 整合冷卻期檢查的單元測試（對應工作計畫 Task 4）
    /// 測試策略：
    ///   - Tests 1~3：測試純決策函數 CrossFloorManager.DecideNextCrossFloorAction
    ///     （已抽出為 internal static，不依賴 SQLData/HikAGV/Log）
    ///   - Tests 4~5：測試 OnCrossFloorDispatchCompleted(mission) 是否正確驅動 CooldownTracker
    ///     以 CrossFloorManager 實體 + 預設空 ElevatorSettings 建構
    /// </summary>
    [TestClass]
    public class CrossFloorManagerSelectNextMissionTests
    {
        private ElevatorPathCalculator _pathCalculator;

        [TestInitialize]
        public void Setup()
        {
            _pathCalculator = new ElevatorPathCalculator(new ElevatorSettings());
        }

        #region Happy Path — 決策函數行為

        [TestMethod]
        public void Decide_冷卻期命中且需派預調度_回傳CooldownHit並帶父任務()
        {
            // Arrange
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            cooldown.RecordCompletion("PARENT_A");

            // 父任務起點 W1（3F 客貨梯等待點，視為 3F）；車在 1F → 需要預調度
            var parentTask = new oMissionModel
            {
                TaskDateTime = "PARENT_A",
                BeginStation = "I1",
                EndStation = "I2",
                TaskSource = "MCS",
            };
            var pending = new List<oMissionModel> { parentTask };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "1F", _pathCalculator, cooldown);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.CooldownHit, decision.Kind);
            Assert.AreSame(parentTask, decision.Task, "冷卻期命中應回傳 triggerTask (父任務) 而非新建預調度");
        }

        [TestMethod]
        public void Decide_冷卻期未命中且需派預調度_回傳NeedDispatch()
        {
            // Arrange
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            // 無 RecordCompletion → 冷卻期未命中

            var parentTask = new oMissionModel
            {
                TaskDateTime = "PARENT_B",
                BeginStation = "I1",
                EndStation = "I2",
                TaskSource = "MCS",
            };
            var pending = new List<oMissionModel> { parentTask };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "1F", _pathCalculator, cooldown);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.NeedDispatch, decision.Kind);
            Assert.AreSame(parentTask, decision.Task);
            Assert.AreEqual("1F", decision.FromFloor);
            Assert.AreEqual("3F", decision.ToFloor);
        }

        [TestMethod]
        public void Decide_不同parent不受冷卻期影響_照常派預調度()
        {
            // Arrange
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            cooldown.RecordCompletion("PARENT_A");

            // triggerTask 的 TaskDateTime 與冷卻期儲存的 parent 不同
            var triggerTask = new oMissionModel
            {
                TaskDateTime = "PARENT_DIFFERENT",
                BeginStation = "I1",
                EndStation = "I2",
                TaskSource = "MCS",
            };
            var pending = new List<oMissionModel> { triggerTask };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "1F", _pathCalculator, cooldown);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.NeedDispatch, decision.Kind,
                "不同 parent 不受冷卻期影響，應回傳 NeedDispatch 正常派預調度");
        }

        [TestMethod]
        public void OnCrossFloorDispatchCompleted_mission帶ParentTaskDateTime_記錄進冷卻期()
        {
            // Arrange
            var manager = CreateTestManager();
            var mission = new oMissionModel
            {
                TaskSource = CrossFloorManager.CROSS_FLOOR_DISPATCH,
                ParentTaskDateTime = "PARENT_RECORD",
            };

            // Act
            manager.OnCrossFloorDispatchCompleted(mission);

            // Assert
            Assert.IsTrue(manager.CooldownTracker.IsInCooldown("PARENT_RECORD"),
                "預調度完成 + 帶 ParentTaskDateTime → CooldownTracker 應記錄該 parent");
        }

        #endregion

        #region Edge Case

        [TestMethod]
        public void OnCrossFloorDispatchCompleted_mission無ParentTaskDateTime_不記錄()
        {
            // Arrange
            var manager = CreateTestManager();
            var mission = new oMissionModel
            {
                TaskSource = CrossFloorManager.CROSS_FLOOR_DISPATCH,
                ParentTaskDateTime = null,
            };

            // Act
            manager.OnCrossFloorDispatchCompleted(mission);

            // Assert
            Assert.IsFalse(manager.CooldownTracker.IsInCooldown("PARENT_ANY"),
                "ParentTaskDateTime 為 null → CooldownTracker 不應記錄任何 parent");
        }

        #endregion

        // ─── Helpers ──────────────────────────────────────────────

        /// <summary>
        /// 建立最小可用 CrossFloorManager，專門給「OnCrossFloorDispatchCompleted → CooldownTracker」
        /// 這類不涉及 SQL/HikAGV 的路徑使用。若呼叫到 _mDB / _hikAGV 會 NRE。
        /// </summary>
        private CrossFloorManager CreateTestManager()
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
    }
}
