using System.Collections.Generic;
using System.Linq;
using HikAGVWebAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// ElevatorPathCalculator 完整測試
    /// </summary>
    [TestClass]
    public class ElevatorPathCalculatorTests
    {
        private ElevatorSettings _settings;
        private ElevatorPathCalculator _calculator;

        [TestInitialize]
        public void Setup()
        {
            _settings = new ElevatorSettings();
            _calculator = new ElevatorPathCalculator(_settings);
        }

        #region GetFloor 樓層判斷測試 - 1F

        [TestMethod]
        public void GetFloor_A區域_應該返回1F()
        {
            Assert.AreEqual("1F", _calculator.GetFloor("A1"));
            Assert.AreEqual("1F", _calculator.GetFloor("A2"));
        }

        [TestMethod]
        public void GetFloor_B區域_應該返回1F()
        {
            Assert.AreEqual("1F", _calculator.GetFloor("B1"));
            Assert.AreEqual("1F", _calculator.GetFloor("B2"));
        }

        [TestMethod]
        public void GetFloor_C區域_應該返回1F()
        {
            Assert.AreEqual("1F", _calculator.GetFloor("C1"));
            Assert.AreEqual("1F", _calculator.GetFloor("C2"));
        }

        [TestMethod]
        public void GetFloor_D區域_應該返回1F()
        {
            Assert.AreEqual("1F", _calculator.GetFloor("D1"));
            Assert.AreEqual("1F", _calculator.GetFloor("D2"));
        }

        [TestMethod]
        public void GetFloor_E區域_應該返回1F()
        {
            Assert.AreEqual("1F", _calculator.GetFloor("E1"));
            Assert.AreEqual("1F", _calculator.GetFloor("E2"));
        }

        [TestMethod]
        public void GetFloor_G區域_應該返回1F()
        {
            Assert.AreEqual("1F", _calculator.GetFloor("G1"));
            Assert.AreEqual("1F", _calculator.GetFloor("G2"));
        }

        #endregion

        #region GetFloor 樓層判斷測試 - 2F

        [TestMethod]
        public void GetFloor_H區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("H1"));
            Assert.AreEqual("2F", _calculator.GetFloor("H2"));
        }

        [TestMethod]
        public void GetFloor_M區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("M1"));
            Assert.AreEqual("2F", _calculator.GetFloor("M2"));
        }

        [TestMethod]
        public void GetFloor_N區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("N1"));
        }

        [TestMethod]
        public void GetFloor_O區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("O1"));
            Assert.AreEqual("2F", _calculator.GetFloor("O2"));
        }

        [TestMethod]
        public void GetFloor_P區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("P1"));
            Assert.AreEqual("2F", _calculator.GetFloor("P2"));
        }

        [TestMethod]
        public void GetFloor_Q區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("Q1"));
            Assert.AreEqual("2F", _calculator.GetFloor("Q2"));
        }

        [TestMethod]
        public void GetFloor_R區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("R1"));
            Assert.AreEqual("2F", _calculator.GetFloor("R2"));
        }

        [TestMethod]
        public void GetFloor_S區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("S1"));
            Assert.AreEqual("2F", _calculator.GetFloor("S2"));
        }

        [TestMethod]
        public void GetFloor_T區域_應該返回2F()
        {
            Assert.AreEqual("2F", _calculator.GetFloor("T1"));
            Assert.AreEqual("2F", _calculator.GetFloor("T2"));
        }

        #endregion

        #region GetFloor 樓層判斷測試 - 3F

        [TestMethod]
        public void GetFloor_I區域_應該返回3F()
        {
            Assert.AreEqual("3F", _calculator.GetFloor("I1"));
            Assert.AreEqual("3F", _calculator.GetFloor("I2"));
        }

        [TestMethod]
        public void GetFloor_J區域_應該返回3F()
        {
            Assert.AreEqual("3F", _calculator.GetFloor("J1"));
            Assert.AreEqual("3F", _calculator.GetFloor("J2"));
        }

        #endregion

        #region GetFloor 樓層判斷測試 - 4F

        [TestMethod]
        public void GetFloor_K區域_應該返回4F()
        {
            Assert.AreEqual("4F", _calculator.GetFloor("K1"));
            Assert.AreEqual("4F", _calculator.GetFloor("K2"));
        }

        [TestMethod]
        public void GetFloor_L區域_應該返回4F()
        {
            Assert.AreEqual("4F", _calculator.GetFloor("L1"));
            Assert.AreEqual("4F", _calculator.GetFloor("L2"));
        }

        #endregion

        #region GetFloor 邊界測試

        [TestMethod]
        public void GetFloor_Null輸入_應該返回Null()
        {
            Assert.IsNull(_calculator.GetFloor(null));
        }

        [TestMethod]
        public void GetFloor_空字串_應該返回Null()
        {
            Assert.IsNull(_calculator.GetFloor(""));
        }

        [TestMethod]
        public void GetFloor_小寫站點_應該正確識別()
        {
            Assert.AreEqual("1F", _calculator.GetFloor("a1"));
            Assert.AreEqual("2F", _calculator.GetFloor("m1"));
        }

        #endregion

        #region 同樓層測試

        [TestMethod]
        public void CalculatePath_同樓層2F_M1到O1_應該直接路徑()
        {
            var path = _calculator.CalculatePath("M1", "O1");
            CollectionAssert.AreEqual(new List<string> { "M1", "O1" }, path);
        }

        [TestMethod]
        public void CalculatePath_同樓層1F_A1到C1_應該直接路徑()
        {
            var path = _calculator.CalculatePath("A1", "C1");
            CollectionAssert.AreEqual(new List<string> { "A1", "C1" }, path);
        }

        [TestMethod]
        public void CalculatePath_同樓層3F_I1到J1_應該直接路徑()
        {
            var path = _calculator.CalculatePath("I1", "J1");
            CollectionAssert.AreEqual(new List<string> { "I1", "J1" }, path);
        }

        [TestMethod]
        public void CalculatePath_同樓層4F_K1到L1_應該直接路徑()
        {
            var path = _calculator.CalculatePath("K1", "L1");
            CollectionAssert.AreEqual(new List<string> { "K1", "L1" }, path);
        }

        #endregion

        #region 客梯測試 (1F-3F)

        [TestMethod]
        public void CalculatePath_客梯下行_3F到1F_J1到G1()
        {
            var path = _calculator.CalculatePath("J1", "G1");
            var expected = new List<string> { "J1", "W1", "W2", "U2", "G1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客梯上行_1F到3F_G1到J1()
        {
            var path = _calculator.CalculatePath("G1", "J1");
            var expected = new List<string> { "G1", "U1", "U2", "W2", "J1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客梯下行_2F到1F_H1到G1()
        {
            var path = _calculator.CalculatePath("H1", "G1");
            var expected = new List<string> { "H1", "V1", "V2", "U2", "G1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客梯上行_1F到2F_G1到H1()
        {
            var path = _calculator.CalculatePath("G1", "H1");
            var expected = new List<string> { "G1", "U1", "U2", "V2", "H1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客梯上行_2F到3F_H1到J1()
        {
            var path = _calculator.CalculatePath("H1", "J1");
            var expected = new List<string> { "H1", "V1", "V2", "W2", "J1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客梯下行_3F到2F_J1到H1()
        {
            var path = _calculator.CalculatePath("J1", "H1");
            var expected = new List<string> { "J1", "W1", "W2", "V2", "H1" };
            CollectionAssert.AreEqual(expected, path);
        }

        #endregion

        #region 客貨梯測試 (3F-4F)

        [TestMethod]
        public void CalculatePath_客貨梯上行_3F到4F_J1到K1()
        {
            var path = _calculator.CalculatePath("J1", "K1");
            var expected = new List<string> { "J1", "X1", "X2", "Y2", "K1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客貨梯下行_4F到3F_K1到J1()
        {
            var path = _calculator.CalculatePath("K1", "J1");
            var expected = new List<string> { "K1", "Y1", "Y2", "X2", "J1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客貨梯上行_3F到4F_I1到L1()
        {
            var path = _calculator.CalculatePath("I1", "L1");
            var expected = new List<string> { "I1", "X1", "X2", "Y2", "L1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_客貨梯下行_4F到3F_L1到I1()
        {
            var path = _calculator.CalculatePath("L1", "I1");
            var expected = new List<string> { "L1", "Y1", "Y2", "X2", "I1" };
            CollectionAssert.AreEqual(expected, path);
        }

        #endregion

        #region 雙電梯換乘測試 (1F/2F ↔ 4F)

        [TestMethod]
        public void CalculatePath_雙電梯上行_2F到4F_H1到K1()
        {
            var path = _calculator.CalculatePath("H1", "K1");
            var expected = new List<string> { "H1", "V1", "V2", "W2", "X1", "X2", "Y2", "K1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_雙電梯下行_4F到2F_K1到H1()
        {
            var path = _calculator.CalculatePath("K1", "H1");
            var expected = new List<string> { "K1", "Y1", "Y2", "X2", "W1", "W2", "V2", "H1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_雙電梯上行_1F到4F_G1到K1()
        {
            var path = _calculator.CalculatePath("G1", "K1");
            var expected = new List<string> { "G1", "U1", "U2", "W2", "X1", "X2", "Y2", "K1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_雙電梯下行_4F到1F_K1到G1()
        {
            var path = _calculator.CalculatePath("K1", "G1");
            var expected = new List<string> { "K1", "Y1", "Y2", "X2", "W1", "W2", "U2", "G1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_雙電梯上行_2F到4F_M1到L1()
        {
            var path = _calculator.CalculatePath("M1", "L1");
            var expected = new List<string> { "M1", "V1", "V2", "W2", "X1", "X2", "Y2", "L1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_雙電梯下行_4F到2F_L1到M1()
        {
            var path = _calculator.CalculatePath("L1", "M1");
            var expected = new List<string> { "L1", "Y1", "Y2", "X2", "W1", "W2", "V2", "M1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_雙電梯上行_1F到4F_A1到K1()
        {
            var path = _calculator.CalculatePath("A1", "K1");
            var expected = new List<string> { "A1", "U1", "U2", "W2", "X1", "X2", "Y2", "K1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void CalculatePath_雙電梯下行_4F到1F_K1到A1()
        {
            var path = _calculator.CalculatePath("K1", "A1");
            var expected = new List<string> { "K1", "Y1", "Y2", "X2", "W1", "W2", "U2", "A1" };
            CollectionAssert.AreEqual(expected, path);
        }

        #endregion

        #region 邊界測試

        [TestMethod]
        public void CalculatePath_相同起終點_應該返回兩點()
        {
            var path = _calculator.CalculatePath("M1", "M1");
            CollectionAssert.AreEqual(new List<string> { "M1", "M1" }, path);
        }

        [TestMethod]
        public void CalculatePath_路徑點數驗證_同樓層應為2點()
        {
            var path = _calculator.CalculatePath("M1", "O1");
            Assert.AreEqual(2, path.Count);
        }

        [TestMethod]
        public void CalculatePath_路徑點數驗證_單電梯應為5點()
        {
            var path = _calculator.CalculatePath("J1", "G1");
            Assert.AreEqual(5, path.Count);
        }

        [TestMethod]
        public void CalculatePath_路徑點數驗證_雙電梯應為8點()
        {
            var path = _calculator.CalculatePath("H1", "K1");
            Assert.AreEqual(8, path.Count);
        }

        #endregion

        #region 路徑字串格式測試

        [TestMethod]
        public void CalculatePath_格式化輸出_同樓層()
        {
            var path = _calculator.CalculatePath("M1", "O1");
            var formatted = string.Join(";", path.Select(p => $"{p},00"));
            Assert.AreEqual("M1,00;O1,00", formatted);
        }

        [TestMethod]
        public void CalculatePath_格式化輸出_單電梯()
        {
            var path = _calculator.CalculatePath("J1", "G1");
            var formatted = string.Join(";", path.Select(p => $"{p},00"));
            Assert.AreEqual("J1,00;W1,00;W2,00;U2,00;G1,00", formatted);
        }

        [TestMethod]
        public void CalculatePath_格式化輸出_雙電梯()
        {
            var path = _calculator.CalculatePath("H1", "K1");
            var formatted = string.Join(";", path.Select(p => $"{p},00"));
            Assert.AreEqual("H1,00;V1,00;V2,00;W2,00;X1,00;X2,00;Y2,00;K1,00", formatted);
        }

        #endregion

        #region GetTaskType 路線對照表測試（使用 List<oTaskTypeRouteModel>）

        private const string DefaultTaskType = "F001";

        private static List<oTaskTypeRouteModel> CreateTransportRoutes()
        {
            return new List<oTaskTypeRouteModel>
            {
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "1F", ToFloor = "1F", TaskType = "F002", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "2F", ToFloor = "2F", TaskType = "F001", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "1F", ToFloor = "3F", TaskType = "F13Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "3F", ToFloor = "1F", TaskType = "F31Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "3F", ToFloor = "4F", TaskType = "F34Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "4F", ToFloor = "3F", TaskType = "F43Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "2F", ToFloor = "4F", TaskType = "F24Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "4F", ToFloor = "2F", TaskType = "F42Test", UseFlag = "Y" },
            };
        }

        private static List<oTaskTypeRouteModel> CreateCrossFloorOnlyRoutes()
        {
            return new List<oTaskTypeRouteModel>
            {
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "1F", ToFloor = "3F", TaskType = "F13Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "3F", ToFloor = "1F", TaskType = "F31Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "3F", ToFloor = "4F", TaskType = "F34Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "4F", ToFloor = "3F", TaskType = "F43Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "2F", ToFloor = "4F", TaskType = "F24Test", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "Transport", FromFloor = "4F", ToFloor = "2F", TaskType = "F42Test", UseFlag = "Y" },
            };
        }

        [TestMethod]
        public void GetTaskType_同樓層2F_M1到O1_無SameFloorMap_應該返回預設TaskType()
        {
            var result = _calculator.GetTaskType("M1", "O1", CreateCrossFloorOnlyRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_同樓層1F_A1到C1_無SameFloorMap_應該返回預設TaskType()
        {
            var result = _calculator.GetTaskType("A1", "C1", CreateCrossFloorOnlyRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_同樓層1F_A1到C1_有SameFloorMap_應該返回F002()
        {
            var result = _calculator.GetTaskType("A1", "C1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F002", result);
        }

        [TestMethod]
        public void GetTaskType_同樓層2F_M1到O1_有SameFloorMap_應該返回F001()
        {
            var result = _calculator.GetTaskType("M1", "O1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_同樓層3F_I1到J1_有SameFloorMap但無3F設定_應該返回預設TaskType()
        {
            var result = _calculator.GetTaskType("I1", "J1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_1F到3F_G1到J1_應該返回F13Test()
        {
            var result = _calculator.GetTaskType("G1", "J1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F13Test", result);
        }

        [TestMethod]
        public void GetTaskType_3F到1F_J1到G1_應該返回F31Test()
        {
            var result = _calculator.GetTaskType("J1", "G1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F31Test", result);
        }

        [TestMethod]
        public void GetTaskType_3F到4F_J1到K1_應該返回F34Test()
        {
            var result = _calculator.GetTaskType("J1", "K1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F34Test", result);
        }

        [TestMethod]
        public void GetTaskType_4F到3F_K1到J1_應該返回F43Test()
        {
            var result = _calculator.GetTaskType("K1", "J1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F43Test", result);
        }

        [TestMethod]
        public void GetTaskType_2F到4F_H1到K1_應該返回F24Test()
        {
            var result = _calculator.GetTaskType("H1", "K1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F24Test", result);
        }

        [TestMethod]
        public void GetTaskType_4F到2F_K1到H1_應該返回F42Test()
        {
            var result = _calculator.GetTaskType("K1", "H1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F42Test", result);
        }

        [TestMethod]
        public void GetTaskType_未定義路線_1F到2F_應該返回預設TaskType()
        {
            // 1F>2F 不在路由清單中
            var result = _calculator.GetTaskType("G1", "H1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_空路由清單_應該返回預設TaskType()
        {
            var result = _calculator.GetTaskType("G1", "J1", new List<oTaskTypeRouteModel>(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_Null站點_應該返回預設TaskType()
        {
            var result = _calculator.GetTaskType(null, "J1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_空字串站點_應該返回預設TaskType()
        {
            var result = _calculator.GetTaskType("", "J1", CreateTransportRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_Null路由清單_應該返回預設TaskType()
        {
            var result = _calculator.GetTaskType("G1", "J1", null, DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        #endregion

        #region GetTaskType EmptyMove 空車移動測試

        private static List<oTaskTypeRouteModel> CreateEmptyMoveRoutes()
        {
            return new List<oTaskTypeRouteModel>
            {
                new oTaskTypeRouteModel { MoveType = "EmptyMove", FromFloor = "1F", ToFloor = "4F", TaskType = "EM14", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "EmptyMove", FromFloor = "2F", ToFloor = "4F", TaskType = "EM24", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "EmptyMove", FromFloor = "3F", ToFloor = "4F", TaskType = "EM34", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "EmptyMove", FromFloor = "4F", ToFloor = "1F", TaskType = "EM41", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "EmptyMove", FromFloor = "4F", ToFloor = "2F", TaskType = "EM42", UseFlag = "Y" },
                new oTaskTypeRouteModel { MoveType = "EmptyMove", FromFloor = "4F", ToFloor = "3F", TaskType = "EM43", UseFlag = "Y" },
            };
        }

        [TestMethod]
        public void GetTaskType_EmptyMove_2F到4F_應該返回EM24()
        {
            var result = _calculator.GetTaskType("H1", "K1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("EM24", result);
        }

        [TestMethod]
        public void GetTaskType_EmptyMove_4F到2F_應該返回EM42()
        {
            var result = _calculator.GetTaskType("K1", "H1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("EM42", result);
        }

        [TestMethod]
        public void GetTaskType_EmptyMove_1F到4F_應該返回EM14()
        {
            var result = _calculator.GetTaskType("G1", "K1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("EM14", result);
        }

        [TestMethod]
        public void GetTaskType_EmptyMove_3F到4F_應該返回EM34()
        {
            var result = _calculator.GetTaskType("J1", "K1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("EM34", result);
        }

        [TestMethod]
        public void GetTaskType_EmptyMove_4F到1F_應該返回EM41()
        {
            var result = _calculator.GetTaskType("K1", "G1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("EM41", result);
        }

        [TestMethod]
        public void GetTaskType_EmptyMove_4F到3F_應該返回EM43()
        {
            var result = _calculator.GetTaskType("K1", "J1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("EM43", result);
        }

        [TestMethod]
        public void GetTaskType_EmptyMove_未定義路線_應該返回預設TaskType()
        {
            // EmptyMove 1F→2F 不在清單中
            var result = _calculator.GetTaskType("G1", "H1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("F001", result);
        }

        [TestMethod]
        public void GetTaskType_Transport和EmptyMove同路線不同TaskType()
        {
            // 同一路線 2F→4F，Transport 用 F24Test，EmptyMove 用 EM24
            var transportResult = _calculator.GetTaskType("H1", "K1", CreateTransportRoutes(), DefaultTaskType);
            var emptyMoveResult = _calculator.GetTaskType("H1", "K1", CreateEmptyMoveRoutes(), DefaultTaskType);
            Assert.AreEqual("F24Test", transportResult);
            Assert.AreEqual("EM24", emptyMoveResult);
            Assert.AreNotEqual(transportResult, emptyMoveResult);
        }

        #endregion

        #region GetElevatorExitStation 歸位出口站點測試

        [TestMethod]
        public void GetElevatorExitStation_目標4F_應該返回客貨梯4F等待點()
        {
            // 4F 由客貨梯服務，返回 Y1（4F 客貨梯等待點）
            var result = _calculator.GetElevatorExitStation("H1", "4F");
            Assert.AreEqual("Y1", result);
        }

        [TestMethod]
        public void GetElevatorExitStation_目標3F_應該返回客貨梯或客梯3F等待點()
        {
            // 3F 同時在客梯與客貨梯範圍，優先回傳客貨梯等待點 X1
            var result = _calculator.GetElevatorExitStation("K1", "3F");
            Assert.AreEqual("X1", result);
        }

        [TestMethod]
        public void GetElevatorExitStation_目標2F_應該返回客梯2F等待點()
        {
            // 2F 由客梯服務，返回 V1（2F 客梯等待點）
            var result = _calculator.GetElevatorExitStation("K1", "2F");
            Assert.AreEqual("V1", result);
        }

        [TestMethod]
        public void GetElevatorExitStation_目標1F_應該返回客梯1F等待點()
        {
            // 1F 由客梯服務，返回 U1（1F 客梯等待點）
            var result = _calculator.GetElevatorExitStation("K1", "1F");
            Assert.AreEqual("U1", result);
        }

        [TestMethod]
        public void GetElevatorExitStation_Null目標樓層_應該返回Null()
        {
            var result = _calculator.GetElevatorExitStation("H1", null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetElevatorExitStation_空字串目標樓層_應該返回Null()
        {
            var result = _calculator.GetElevatorExitStation("H1", "");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetElevatorExitStation_不存在樓層_應該返回Null()
        {
            var result = _calculator.GetElevatorExitStation("H1", "5F");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetElevatorExitStation_4F目標站應為客貨梯等待點()
        {
            // 4F 出口站應為客貨梯等待點（AgvSettings 預設 FreightElevatorWaitPoints 包含 4F:Y1）
            string exitStation = _calculator.GetElevatorExitStation("H1", "4F");
            Assert.IsNotNull(exitStation);
            // 電梯等待點為電梯配置中的有效站點，能作為 RCS positionCodePath 的終點
            Assert.IsFalse(string.IsNullOrEmpty(exitStation), "歸位目標站不得為空");
        }

        [TestMethod]
        public void GetElevatorExitStation_不同起點_相同目標樓層_應返回相同出口站()
        {
            // 不論從哪個站點歸位至 4F，都應返回同一個電梯等待點
            string fromH = _calculator.GetElevatorExitStation("H1", "4F");
            string fromI = _calculator.GetElevatorExitStation("I1", "4F");
            Assert.AreEqual(fromH, fromI, "相同樓層目標應有一致的歸位站點");
        }

        #endregion

        #region BuildReturnPath 雙電梯換乘測試（空車移動補齊 3F 出口等待點）

        [TestMethod]
        public void BuildReturnPath_上行_1F到4F_應包含W1出口等待點()
        {
            // Arrange
            string fromFloor = "1F";
            string toFloor = "4F";

            // Act
            var path = _calculator.BuildReturnPath(fromFloor, toFloor);

            // Assert
            var expected = new List<string> { "U1", "U2", "W2", "W1", "X1", "X2", "Y2", "Y1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void BuildReturnPath_上行_2F到4F_應包含W1出口等待點()
        {
            // Arrange
            string fromFloor = "2F";
            string toFloor = "4F";

            // Act
            var path = _calculator.BuildReturnPath(fromFloor, toFloor);

            // Assert
            var expected = new List<string> { "V1", "V2", "W2", "W1", "X1", "X2", "Y2", "Y1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void BuildReturnPath_下行_4F到1F_應包含X1出口等待點()
        {
            // Arrange
            string fromFloor = "4F";
            string toFloor = "1F";

            // Act
            var path = _calculator.BuildReturnPath(fromFloor, toFloor);

            // Assert
            var expected = new List<string> { "Y1", "Y2", "X2", "X1", "W1", "W2", "U2", "U1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void BuildReturnPath_下行_4F到2F_應包含X1出口等待點()
        {
            // Arrange
            string fromFloor = "4F";
            string toFloor = "2F";

            // Act
            var path = _calculator.BuildReturnPath(fromFloor, toFloor);

            // Assert
            var expected = new List<string> { "Y1", "Y2", "X2", "X1", "W1", "W2", "V2", "V1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void BuildReturnPath_單電梯_3F到4F_不受影響()
        {
            // Arrange
            string fromFloor = "3F";
            string toFloor = "4F";

            // Act
            var path = _calculator.BuildReturnPath(fromFloor, toFloor);

            // Assert — 單電梯不經過換乘，維持原路徑
            var expected = new List<string> { "X1", "X2", "Y2", "Y1" };
            CollectionAssert.AreEqual(expected, path);
        }

        [TestMethod]
        public void BuildReturnPath_單電梯_4F到3F_不受影響()
        {
            // Arrange
            string fromFloor = "4F";
            string toFloor = "3F";

            // Act
            var path = _calculator.BuildReturnPath(fromFloor, toFloor);

            // Assert — 單電梯不經過換乘，維持原路徑
            var expected = new List<string> { "Y1", "Y2", "X2", "X1" };
            CollectionAssert.AreEqual(expected, path);
        }

        #endregion
    }
}