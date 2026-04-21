using System;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// 冷卻期核心邏輯單元測試（對應工作計畫 Task 3）
    /// 測試對象：CooldownTracker（純類別，不依賴 SQLData / Log 等外部元件）
    /// </summary>
    [TestClass]
    public class CrossFloorManagerCooldownTests
    {
        #region Happy Path

        [TestMethod]
        public void IsInCooldown_未呼叫過RecordCompletion_回傳false()
        {
            // Arrange
            var tracker = new CooldownTracker(cooldownSeconds: 30);

            // Act
            bool result = tracker.IsInCooldown("P1");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsInCooldown_RecordCompletion後立即查同parent_回傳true()
        {
            // Arrange
            var tracker = new CooldownTracker(cooldownSeconds: 30);
            tracker.RecordCompletion("P1");

            // Act
            bool result = tracker.IsInCooldown("P1");

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsInCooldown_RecordCompletion後查不同parent_回傳false()
        {
            // Arrange
            var tracker = new CooldownTracker(cooldownSeconds: 30);
            tracker.RecordCompletion("P1");

            // Act
            bool result = tracker.IsInCooldown("P2");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RecordCompletion_連續呼叫兩次_以最新一筆為準()
        {
            // Arrange
            var tracker = new CooldownTracker(cooldownSeconds: 30);
            tracker.RecordCompletion("P1");
            tracker.RecordCompletion("P2");

            // Act
            bool p1Result = tracker.IsInCooldown("P1");
            bool p2Result = tracker.IsInCooldown("P2");

            // Assert
            Assert.IsFalse(p1Result, "舊 parent 應該被新紀錄覆蓋");
            Assert.IsTrue(p2Result, "最新 parent 應該在冷卻期內");
        }

        #endregion

        #region Boundary

        [TestMethod]
        public void IsInCooldown_RecordCompletion後31秒查同parent_回傳false過期()
        {
            // Arrange
            DateTime fakeNow = new DateTime(2026, 4, 21, 14, 32, 35);
            var tracker = new CooldownTracker(cooldownSeconds: 30, nowProvider: () => fakeNow);
            tracker.RecordCompletion("P1");
            fakeNow = fakeNow.AddSeconds(31);

            // Act
            bool result = tracker.IsInCooldown("P1");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsInCooldown_RecordCompletion後29秒查同parent_回傳true未過期邊界()
        {
            // Arrange
            DateTime fakeNow = new DateTime(2026, 4, 21, 14, 32, 35);
            var tracker = new CooldownTracker(cooldownSeconds: 30, nowProvider: () => fakeNow);
            tracker.RecordCompletion("P1");
            fakeNow = fakeNow.AddSeconds(29);

            // Act
            bool result = tracker.IsInCooldown("P1");

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region Edge Case

        [TestMethod]
        public void RecordCompletion_parent為null_不記錄()
        {
            // Arrange
            var tracker = new CooldownTracker(cooldownSeconds: 30);
            tracker.RecordCompletion(null);

            // Act
            bool result = tracker.IsInCooldown("P1");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RecordCompletion_parent為空字串_不記錄()
        {
            // Arrange
            var tracker = new CooldownTracker(cooldownSeconds: 30);
            tracker.RecordCompletion(string.Empty);

            // Act - 以空字串查（理論上不會被 RecordCompletion 寫入）
            bool result = tracker.IsInCooldown("");

            // Assert - 空字串 parent 由 IsInCooldown 直接擋掉也回傳 false
            Assert.IsFalse(result);
        }

        #endregion
    }
}
