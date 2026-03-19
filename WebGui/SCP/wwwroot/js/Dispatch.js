import { connection } from './common/hub.js';

console.log("=== Dispatch.js 已載入 ===");
console.log("floorAreaMap from window:", window.floorAreaMap);
// 本地 loadMapData 函數 (避免跨模組 import 問題)
function loadMapDataLocal(area) {
    // 更新 window 層級的當前地圖區域
    window.currentMapArea = area || 'FHT2-1F';

    $.ajax({
        type: "GET",
        url: "/api/Common/ShowMap",
        data: { area: area },
        success: function (data) {
            $("#Map").html(data);
            const mapSrc = area || 'FHT2-1F';
            $('#map-img').attr('src', `/img/${mapSrc}.png`);

            // 初始化所有庫位的 Bootstrap Tooltip
            $('[data-bs-toggle="tooltip"]').tooltip({
                container: 'body',
                trigger: 'hover'
            });

            // 為 M 區和 T 區站點綁定點擊事件（物料管理）
            bindStationLotEvents();
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.error("地圖載入失敗: ", textStatus, errorThrown);
        }
    });
}

// 綁定 M/T/Q 區站點的物料管理點擊事件
function bindStationLotEvents() {
    console.log("=== 綁定 M/T/Q 區站點點擊事件 ===");

    // 標記 M、T、Q 區的站點（支援物料登記）
    $('.station-btn').each(function () {
        var stationNo = $(this).attr('id');
        if (stationNo && (stationNo.startsWith('M') || stationNo.startsWith('T') || stationNo.startsWith('Q') || stationNo.startsWith('R'))) {
            $(this).addClass('lot-manageable');
            $(this).css('cursor', 'pointer');
            console.log("標記可管理站點:", stationNo);
        }
    });
}

// 將函數綁定到 window，供 Map.js 呼叫
window.bindStationLotEvents = bindStationLotEvents;

// 使用事件委派綁定站點點擊事件 - 直接檢查站點 ID，不依賴 class 標記
$(document).on('click', '.station-btn', function (e) {
    var stationNo = $(this).attr('id');

    // 處理 M/T/J/Q/H/K/L 區（物料管理）和 O/P/S/N/G/K/I 區（標記空板/Release）
    var validAreas = ['M', 'T', 'O', 'P', 'S', 'N', 'J', 'G', 'Q', 'R', 'H', 'K', 'I', 'L'];
    var stationArea = stationNo ? stationNo.substring(0, 1).toUpperCase() : '';

    if (!stationNo || validAreas.indexOf(stationArea) === -1) {
        return; // 不是支援的區域，不處理
    }

    e.preventDefault();
    e.stopPropagation();

    console.log("=== 點擊站點:", stationNo, "區域:", stationArea, "===");

    // 從 DOM 取得資料並更新快取
    stationCache[stationNo] = {
        haveFlag: $(this).attr('data-haveflag') || '0',
        workOrder: $(this).attr('data-workorder') || '',
        rackId: $(this).attr('data-rackid') || ''
    };
    console.log("站點資料:", stationCache[stationNo]);

    // 開啟站點物料操作 Modal
    if (typeof window.openStationLotModal === 'function') {
        window.openStationLotModal(stationNo);
    } else {
        console.error("openStationLotModal 函數尚未載入");
        alert("功能載入中，請稍後再試");
    }
});

// 樓層與地圖區域對應
var floorToMapArea = {
    "1F": "FHT2-1F",
    "2F": "FHT2-2F",
    "2F - 站內運輸": "FHT2-2F",
    "2F - 站外運輸": "FHT2-2F",
    "3F": "FHT2-3F",
    "4F": "FHT2-4F"
};

// 全域變數：站點資料快取
var stationCache = {};
var isCacheLoaded = false;
var portBindingList = [];

// 載入上下料區綁定站點清單（有綁定的站點允許 HaveFlag=1）
$.ajax({
    type: "GET",
    url: "/Dispatch/GetPortBindingList",
    async: false,
    success: function (data) {
        portBindingList = data || [];
        console.log("已載入 PortBinding 站點清單:", portBindingList);
    }
});
// 初始化 window 層級的地圖區域追蹤變數（供 Map.js 和 Dispatch.js 共用）
if (!window.currentMapArea) {
    window.currentMapArea = 'FHT2-1F';
}

$(function () {
    var form = $('#DispatchForm');
    var beginSations = [];
    var rowData = {};
    var btnName = "";
    UpdateDispatch();
    $(".Site").prop("disabled", true);

    // 選擇樓層後篩選 Area 選項並切換地圖
    $("#Floor").on("change", function () {
        var selectedFloor = $(this).val();
        var allowedAreas = window.floorAreaMap ? window.floorAreaMap[selectedFloor] || [] : [];

        console.log("選擇樓層:", selectedFloor);
        console.log("允許的區域:", allowedAreas);
        console.log("floorAreaMap:", window.floorAreaMap);

        // 1. 切換地圖
        if (floorToMapArea[selectedFloor]) {
            loadMapDataLocal(floorToMapArea[selectedFloor]);
        }

        // 2. 重置 Area 和後續選項
        $("#Area").val('');
        $("#BeginStation").val('');
        $("#EndStation").val('');
        $(".Site").prop("disabled", true);

        // 3. 顯示/隱藏符合樓層的 Area 選項（已經過權限篩選）
        var visibleCount = 0;
        $("#Area option").each(function () {
            var areaValue = $(this).val();
            if (areaValue === "" || allowedAreas.includes(areaValue)) {
                $(this).show();
                visibleCount++;
            } else {
                $(this).hide();
            }
        });
        console.log("可見的選項數:", visibleCount);

        // 4. 啟用 Area 選擇 (使用 removeAttr 強制移除 disabled)
        $("#Area").removeAttr("disabled");
        console.log("Area disabled 狀態:", $("#Area").prop("disabled"));

        // 輔助函數：選擇第一個可見的區域選項
        function selectFirstVisibleArea() {
            var firstVisible = $("#Area option:visible").not("[value='']").first();
            if (firstVisible.length > 0) {
                var areaValue = firstVisible.val();
                $("#Area").val(areaValue).trigger("change");
                console.log("自動選擇第一個可見區域:", areaValue);
                return true;
            }
            return false;
        }

        // 5. 樓層預設區域選擇
        if (selectedFloor === "2F - 站內運輸") {
            // 2F 站內運輸：區域預設選擇 MT（雷雕區&V cut備貨區），若無則選擇第一個可見區域
            if ($("#Area option[value='MT']:visible").length > 0) {
                $("#Area").val("MT").trigger("change");
                console.log("2F 站內運輸：區域預設選擇 MT");
            } else {
                // MT 不可用，選擇第一個可見的區域（如 Q、M、R）
                selectFirstVisibleArea();
            }
            // 顯示掃描機台按鈕
            $("#machineScanRow").show();
            // 隱藏 Rack 碼和工單欄位（2F 已有建物料流程）
            $("#rackIdRow").hide();
            $("#workOrderRow").hide();
        } else if (selectedFloor === "2F - 站外運輸") {
            // 2F 站外運輸：區域預設選擇 H（成型後），若無則選擇第一個可見區域
            if ($("#Area option[value='H']:visible").length > 0) {
                $("#Area").val("H").trigger("change");
                console.log("2F 站外運輸：區域預設選擇 H（成型後）");
            } else {
                selectFirstVisibleArea();
            }
            // 隱藏掃描機台按鈕（H 區不需要）
            $("#machineScanRow").hide();
            // 隱藏 Rack 碼和工單欄位（H 區已有建物料流程）
            $("#rackIdRow").hide();
            $("#workOrderRow").hide();
        } else if (selectedFloor === "3F") {
            // 3F 樓層：區域預設選擇 J（插針室），若無則選擇第一個可見區域
            if ($("#Area option[value='J']:visible").length > 0) {
                $("#Area").val("J").trigger("change");
                console.log("3F 樓層：區域預設選擇 J（插針室）");
            } else {
                selectFirstVisibleArea();
            }
            // 隱藏掃描機台按鈕（3F 不需要）
            $("#machineScanRow").hide();
            // 隱藏 Rack 碼和工單欄位（3F 已有建物料流程）
            $("#rackIdRow").hide();
            $("#workOrderRow").hide();
        } else if (selectedFloor === "4F") {
            // 4F 樓層：區域預設選擇 L（烘烤前出貨區），若無則選擇第一個可見區域
            if ($("#Area option[value='L']:visible").length > 0) {
                $("#Area").val("L").trigger("change");
                console.log("4F 樓層：區域預設選擇 L（烘烤前出貨區）");
            } else {
                selectFirstVisibleArea();
            }
            // 隱藏掃描機台按鈕（4F 不需要）
            $("#machineScanRow").hide();
            // 隱藏 Rack 碼和工單欄位（4F 已有建物料流程）
            $("#rackIdRow").hide();
            $("#workOrderRow").hide();
        } else {
            // 其他樓層：選擇第一個可見區域
            selectFirstVisibleArea();
            $("#machineScanRow").hide();
            // 顯示 Rack 碼和工單欄位
            $("#rackIdRow").show();
            $("#workOrderRow").show();
        }
    });

    //選擇派送區域選擇完後得事件
    $("#Area").on("change", function () {
        var area = $(this).val();
        var currentFloor = $("#Floor").val();  // 取得當前樓層

        $("#BeginStation").prop("disabled", false);
        $.ajax({
            type: "GET",
            url: "/Dispatch/GetoNeed",
            success: function (data) {
                beginSations = data.map(item => item.objStation);
            },
            error: function (jqXHR, textStatus, errorThrown) {
                console.error("AJAX 請求失敗: ", textStatus, errorThrown);
            }
        });
        // 重置第二個選項的選擇
        $('#BeginStation').val('');
        $('#EndStation').val('');
        if (area == "C") {
            $('#ChangeButton').show();
            $('#RejectdButton').show();
        } else {
            $('#ChangeButton').hide();
            $('#RejectdButton').hide();
        }

        // 2F 站內運輸、2F 站外運輸、H/L/I/J 區：隱藏 Rack碼 和 掃描工單
        // 因為這些資料已在物料登記時設定好
        if (currentFloor === "2F - 站內運輸" || currentFloor === "2F - 站外運輸" ||
            area === "H" || area === "L" || area === "I" || area === "J") {
            $("#rackIdRow").hide();
            $("#workOrderRow").hide();
        } else {
            $("#rackIdRow").show();
            $("#workOrderRow").show();
        }
    });

    //點選派送起點，展開下拉時就會觸發的事件
    $("#BeginStation").on("focus", function () {
        var selectedValue = $("#Area").val();

        if (!selectedValue) {
            alert('請先選擇派送區域');
            return;
        }

        // 如果已有快取，直接使用
        if (isCacheLoaded && Object.keys(stationCache).length > 0) {
            $('#BeginStation option').hide();
            filterBeginStationOptions(selectedValue);
            return;
        }

        console.log('=== 開始載入站點資料 ===');

        // 顯示 Loading 遮罩
        $('#beginStationLoading').show();
        $('#BeginStation').prop('disabled', true);
        $('#BeginStation option').hide();

        $.ajax({
            type: "GET",
            url: "/Dispatch/GetAllStations",
            dataType: "json",
            success: function (stations) {
                console.log('✅ API 成功回傳 ' + stations.length + ' 筆資料');

                // 隱藏 Loading
                $('#beginStationLoading').hide();
                $('#BeginStation').prop('disabled', false);

                if (stations.length === 0) {
                    alert('沒有可用的站點資料');
                    $('#BeginStation option').show();
                    return;
                }

                // 更新快取
                stationCache = {};
                stations.forEach(function (s) {
                    var stationNo = s.stationNo || s.StationNo;
                    stationCache[stationNo] = {
                        haveFlag: s.haveFlag || s.HaveFlag,
                        reserve: s.reserve || s.Reserve || "N",
                        workOrder: s.workOrder || s.WorkOrder,
                        rackId: s.rackId || s.RackId,
                        putTime: s.putTime || s.PutTime,
                        block: s.block || s.Block,
                        machineName: s.machineName || s.MachineName
                    };
                });

                isCacheLoaded = true;
                console.log('快取已更新:', Object.keys(stationCache).length, '個站點');

                filterBeginStationOptions(selectedValue);
            },
            error: function (jqXHR, textStatus, errorThrown) {
                $('#beginStationLoading').hide();
                $('#BeginStation').prop('disabled', false);
                $('#BeginStation option').show();

                console.error('❌ AJAX 失敗:', textStatus);
                alert('無法取得站點資料');
            }
        });
    });

    //選擇完派送起點的值後觸發的事件
    $("#BeginStation").on("change", function () {
        var area = $("#Area").val();
        var selectedValue = $(this).val();
        var currentFloor = $("#Floor").val();
        $('#WorkOrder').val('');

        // 從快取取得選中站點的資料
        var selectedStation = stationCache[selectedValue];

        // MT 區特別處理：根據物料類型決定終點
        if (area === "MT" && selectedStation && selectedStation.workOrder) {
            // ★ 變更起點時，先清空舊終點 ★
            $("#EndStation").val("");

            var workOrder = selectedStation.workOrder;
            var stationPrefix = selectedValue.substring(0, 1);

            if (stationPrefix === "M" && workOrder.includes("^VCUT") && !workOrder.includes("^VCUT^DONE")) {
                // M 區 V Cut 物料 → 自動帶出 T 區空架
                console.log("=== MT區：選擇 V Cut 物料，自動帶出 T 區空架 ===");
                autoSelectEndStation("T", "0", "N");
                $("#machineScanRow").hide();
                // V Cut 物料終點也保持 disabled
                $("#EndStation").prop("disabled", true);
            } else {
                // M 區一般物料 或 T 區已完成物料 → 需掃描機台
                console.log("=== MT區：選擇一般/已完成物料，顯示掃描機台按鈕 ===");
                $("#machineScanRow").show();
                // 如果終點已經有值（透過掃描機台設定），保持 disabled
                if (!$("#EndStation").val()) {
                    $("#EndStation").val("");
                }
                // MT 區終點始終保持 disabled
                $("#EndStation").prop("disabled", true);
            }
            return;  // MT 區處理完畢，不進入 switch
        }

        switch (area) {
            case "A":
                autoSelectEndStation("B", "0", "N");
                break;
            case "C":
                $('#EndStation').val('');
                if (selectedStation && selectedStation.workOrder) {
                    $('#WorkOrder').val(selectedStation.workOrder);
                }
                break;
            case "D":
                autoSelectEndStation("E", "0", "N");
                break;
            case "J":
                autoSelectEndStation("G", "0", "N");  // J區（3F 插針室）→ G區（1F 電梯暫存區）
                break;
            case "G":
                autoSelectEndStation("J", "0", "N");  // G區（1F 電梯暫存區）→ J區（3F 插針室）
                break;
            case "H":
                autoSelectEndStation("K", "0", "N");  // H區（2F成型後）→ K區（4F烘烤前入貨區）
                break;
            case "L":
                autoSelectEndStation("I", "0", "N");  // L區（4F出貨區）→ I區（3F品檢區）
                break;
            case "M":
                // 雷雕區：根據選擇的物料類型決定終點
                // 檢查所選物料是否為 V Cut 物料
                if (selectedStation && selectedStation.workOrder) {
                    var workOrder = selectedStation.workOrder;
                    if (workOrder.includes("^VCUT") && !workOrder.includes("^VCUT^DONE")) {
                        // V Cut 物料 → 自動帶出 T 區空架
                        console.log("=== 選擇 V Cut 物料，自動帶出 T 區空架 ===");
                        autoSelectEndStation("T", "0", "N");
                        $("#machineScanRow").hide();  // 隱藏掃描機台按鈕
                    } else {
                        // 一般物料 → 顯示掃描機台按鈕，讓人員掃描機台
                        console.log("=== 選擇一般物料，顯示掃描機台按鈕 ===");
                        $("#machineScanRow").show();
                        // 清空終點選擇
                        $("#EndStation").val("");
                    }
                }
                break;
            case "T":
                // V cut區可選: O(左上料), P(右上料), S(清洗區) - 需要供單號
                // 顯示掃描機台按鈕
                $("#machineScanRow").show();
                break;
            case "Q":
                autoSelectEndStation("S", "0", "N");  // 出料區 → 清洗區
                break;
            case "R":
                autoSelectEndStation("N", "0", "N");  // 廢料區 → 廢料回收區
                break;
            case "L":
                autoSelectEndStation("I", "0", "N");  // L區（4F烘烤後）→ I區（3F品檢區）
                // L 區派送時，隱藏 Rack碼 和 掃描工單（這些資料已在物料登記時設定）
                $("#rackIdRow").hide();
                $("#workOrderRow").hide();
                break;
            case "I":
                autoSelectEndStationInRange("L", "0", "N", 1, 4);  // I區（3F品檢區）→ L1-L4
                // I 區派送時，隱藏 Rack碼 和 掃描工單（這些資料已在物料登記時設定）
                $("#rackIdRow").hide();
                $("#workOrderRow").hide();
                break;
            case "E":
                break;
        }

        // 非 H/L/I/J 區時，恢復顯示 Rack碼 和 掃描工單 欄位
        // 但 2F 站內/站外運輸樓層例外，保持隱藏
        if (currentFloor !== "2F - 站內運輸" && currentFloor !== "2F - 站外運輸" &&
            area !== "H" && area !== "I" && area !== "L" && area !== "J") {
            $("#rackIdRow").show();
            $("#workOrderRow").show();
        }

        if (area == "C") {
            // C 區：終點可選，供單號禁用
            $("#EndStation").prop("disabled", false);
            $("#WorkOrder").prop("disabled", true);
        } else if (area == "H") {
            // H 區（2F 站外運輸）：終點自動選擇 K 區，保持 disabled
            $("#EndStation").prop("disabled", true);
            $("#WorkOrder").prop("disabled", true);
        } else if (area == "M" || area == "T") {
            // M (雷雕區) 和 T (V Cut區)：終點可選，供單號必填
            console.log("=== M/T 區：啟用供單號 ===");
            console.log("area:", area, "selectedValue:", selectedValue);

            // M 區 V Cut 物料特別處理：終點自動帶出 T，需保持 disabled
            var isVcutMaterial = false;
            if (area === "M" && stationCache[selectedValue] && stationCache[selectedValue].workOrder) {
                var wo = stationCache[selectedValue].workOrder;
                if (wo.includes("^VCUT") && !wo.includes("^VCUT^DONE")) {
                    isVcutMaterial = true;
                }
            }

            if (isVcutMaterial) {
                // V Cut 物料：終點不可選
                $("#EndStation").prop("disabled", true);
            } else {
                // 一般物料 或 T 區：終點可選
                $("#EndStation").prop("disabled", false);
            }

            $("#WorkOrder").prop("disabled", false);  // 啟用供單號輸入
            console.log("WorkOrder disabled 狀態:", $("#WorkOrder").prop("disabled"));
        } else if (area == "MT") {
            // MT 區（雷雕區 & V Cut 備貨區）：終點 disabled，透過掃描機台選擇
            console.log("=== MT 區：終點 disabled，透過掃描機台選擇 ===");
            $("#EndStation").prop("disabled", true);  // 終點設為 disabled
            $("#machineScanRow").show();
        } else if (area == "Q" || area == "R") {
            // Q (出料區) 和 R (廢料區)：終點自動選擇，供單號禁用
            $("#EndStation").prop("disabled", true);
            $("#WorkOrder").prop("disabled", true);
        } else {
            $("#EndStation").prop("disabled", true);
            $("#WorkOrder").prop("disabled", false);
        }

        // 從快取讀取 RackId
        if (selectedStation && selectedStation.rackId) {
            $('#RackId').val(selectedStation.rackId);
        }
    });

    //點選派送終點展開下拉選單時觸發的事件
    $("#EndStation").on("focus", function () {
        var selectedValue = $('#BeginStation').val();
        $('#EndStation option').hide();

        if (!selectedValue) {
            $('#EndStation option').show();
            return;
        }

        var firstChar = selectedValue.substring(0, 1);

        switch (firstChar) {
            case "A":
                filterEndStationOptions("B", "0");
                break;
            case "B":
                filterEndStationOptions("C", null);
                break;
            case "D":
                filterEndStationOptions("E", "0");
                break;
            case "H":
                filterEndStationOptions("K", "0");
                break;
            case "L":
                filterEndStationOptions("I", "0");
                break;
            case "M":
                // 顯示 O, P, T 區空架
                filterEndStationOptions("O", "0");
                filterEndStationOptions("P", "0");
                filterEndStationOptions("T", "0");
                break;
            case "T":
                // 顯示 O, P, S 區空架
                filterEndStationOptions("O", "0");
                filterEndStationOptions("P", "0");
                filterEndStationOptions("S", "0");
                break;
            case "Q":
                filterEndStationOptions("S", "0");
                break;
            case "R":
                filterEndStationOptions("N", "0");
                break;
            default:
                $('#EndStation option').show();
                break;
        }
    });

    //點選工單欄位後會全選
    $('#WorkOrder').on('click', function () {
        $(this).select();
    });

    // 當 WorkOrder 輸入框失去焦點時觸發事件
    $('#WorkOrder').on('blur', function () {
        var inputValue = $(this).val();
        var area = $("#Area").val();
        var InterfaceName = $(`[data-name='${inputValue}']`);

        if (InterfaceName.length > 0 && area === "D") {
            var workOrderData = InterfaceName.attr('data-workorder');
            $(this).val(workOrderData);
        }
    });

    //點擊確認檢查各項欄位輸出是否有問題
    $(".ConfirmButton").on("click", function (event) {
        var inputs = form.find('select[required]');
        var allValid = true;
        var area = $("#Area").val();
        var beginStation = $("#BeginStation").val();
        var endStation = $("#EndStation").val();
        var modal = "";
        btnName = event.target.id;

        inputs.each(function () {
            if (!this.checkValidity()) {
                alert('請選擇派送站點');
                allValid = false;
                return false;
            }
        });

        if (allValid) {
            // MT 區 或 M 區 特別處理：工單已在起點物料資料中，自動帶入
            if ((area === "MT" || area === "M") && beginStation) {
                var beginStationCache = stationCache[beginStation];
                if (beginStationCache && beginStationCache.workOrder) {
                    // 自動填入工單（移除 ^VCUT 標記）
                    var autoWorkOrder = beginStationCache.workOrder.replace("^VCUT^DONE", "").replace("^VCUT", "");
                    $("#WorkOrder").val(autoWorkOrder);
                    console.log("MT/M 區自動填入工單:", autoWorkOrder);
                }
            }
            // J 區（3F 插針室）特別處理：工單已在物料登記时輸入，自動帶入
            else if (area === "J" && beginStation) {
                var beginStationCache = stationCache[beginStation];
                if (beginStationCache && beginStationCache.workOrder) {
                    // 自動填入工單
                    $("#WorkOrder").val(beginStationCache.workOrder);
                    console.log("J 區自動填入工單:", beginStationCache.workOrder);
                }
            }
            // Q 區（出貨區）特別處理：工單已在物料登記時輸入，自動帶入
            else if (area === "Q" && beginStation) {
                var beginStationCache = stationCache[beginStation];
                if (beginStationCache && beginStationCache.workOrder) {
                    // 自動填入工單
                    $("#WorkOrder").val(beginStationCache.workOrder);
                    console.log("Q 區自動填入工單:", beginStationCache.workOrder);
                }
            }
            // R 區（廢料區）特別處理：工單選填，如有則自動帶入
            else if (area === "R" && beginStation) {
                var beginStationCache = stationCache[beginStation];
                if (beginStationCache && beginStationCache.workOrder) {
                    // 自動填入工單
                    $("#WorkOrder").val(beginStationCache.workOrder);
                    console.log("R 區自動填入工單:", beginStationCache.workOrder);
                }
            }
            // H 區（2F 成型後）特別處理：工單已在物料登記時輸入，自動帶入
            else if (area === "H" && beginStation) {
                var beginStationCache = stationCache[beginStation];
                if (beginStationCache && beginStationCache.workOrder) {
                    // 自動填入工單
                    $("#WorkOrder").val(beginStationCache.workOrder);
                    console.log("H 區自動填入工單:", beginStationCache.workOrder);
                }
            }
            // L 區（4F 烘烤後）特別處理：工單已在物料登記時輸入，自動帶入
            else if (area === "L" && beginStation) {
                var beginStationCache = stationCache[beginStation];
                if (beginStationCache && beginStationCache.workOrder) {
                    // 自動填入工單
                    $("#WorkOrder").val(beginStationCache.workOrder);
                    console.log("L 區自動填入工單:", beginStationCache.workOrder);
                }
            }
            // I 區（3F 品檢區）特別處理：工單已在物料登記時輸入，自動帶入
            else if (area === "I" && beginStation) {
                var beginStationCache = stationCache[beginStation];
                if (beginStationCache && beginStationCache.workOrder) {
                    // 自動填入工單
                    $("#WorkOrder").val(beginStationCache.workOrder);
                    console.log("I 區自動填入工單:", beginStationCache.workOrder);
                }
            }
            // 需要輸入工單/供單號的區域：A, M, T（如果快取已有工單則自動帶入並跳過驗證）
            else if (beginStation && (beginStation.substring(0, 1) === 'A' || beginStation.substring(0, 1) === 'M' || beginStation.substring(0, 1) === 'T')) {
                var beginStationCache = stationCache[beginStation];
                // M/T 區：如果快取中已有工單資料，自動帶入並跳過驗證
                if ((beginStation.substring(0, 1) === 'M' || beginStation.substring(0, 1) === 'T') && beginStationCache && beginStationCache.workOrder) {
                    var autoWorkOrder = beginStationCache.workOrder.replace("^VCUT^DONE", "").replace("^VCUT", "");
                    $("#WorkOrder").val(autoWorkOrder);
                    console.log("自動填入工單:", autoWorkOrder);
                } else {
                    // 快取中沒有工單資料，需要驗證
                    var workOrder = $("#WorkOrder").val();
                    if (!workOrder || !workOrder.trim()) {
                        // M 和 T 區顯示「供單號」，其他區顯示「工單'
                        var fieldName = (beginStation.substring(0, 1) === 'M' || beginStation.substring(0, 1) === 'T') ? '供單號' : '工單';
                        alert('請輸入' + fieldName);
                        allValid = false;
                        return false;
                    }
                }
            }

            // 驗證：終點站必選
            if (!endStation || endStation === "" || endStation === "選擇站點") {
                // 根據區域顯示不同的提示訊息
                var endAreaName = "目標區域";
                if (area === "Q") {
                    endAreaName = "清洗區";
                }
                alert('沒有可用的派送終點，請確認' + endAreaName + '有空位');
                allValid = false;
            }

            // 從快取驗證站點狀態
            var beginStationData = stationCache[beginStation];
            var endStationData = stationCache[endStation];

            if (allValid && beginStationData && beginStationData.haveFlag === "0") {
                alert('派送起點為空貨架，請重新選擇站點');
                allValid = false;
            } else if (allValid && endStationData && endStationData.haveFlag !== "0" && area !== "C") {
                // 有 oPortBinding 綁定且為空平板時允許通過
                var isBindingPort = portBindingList.indexOf(endStation) !== -1;
                if (!(isBindingPort && endStationData.haveFlag === "1")) {
                    alert('派送終點已有貨架，請重新選擇站點');
                    allValid = false;
                }
            }
        }

        if (!allValid) {
            return;
        }

        // 手動顯示 modal
        if (btnName == "ConfirmButton") {
            modal = $("#dispatchModalToggle");
        } else {
            modal = $("#ReLoginModalToggle");
            $("#userId").val("");
            $("#password").val("");
        }
        var myModal = new bootstrap.Modal(modal, {
            keyboard: false
        });
        myModal.show();
    });

    $("#reLoginButton").on("click", function () {
        var userId = $("#userId").val();
        var password = $("#password").val();
        $.ajax({
            type: "POST",
            url: "/Dispatch/ReLogin",
            contentType: "application/json",
            data: JSON.stringify({ userId, password }),
            success: function (response) {
                var modal = ""
                if (btnName == "RejectdButton") {
                    modal = $("#RejectModalToggle")
                } else {
                    modal = $("#dispatchModalToggle")
                }

                var myModal = bootstrap.Modal.getOrCreateInstance(modal, {
                    keyboard: false
                });
                myModal.show();
            },
            error: function (error) {
                alert("帳號密碼錯誤或權限不足")
                $("#ReLoginModalToggle").modal("hide");
            }
        });

    })

    //下料完成按鈕事件
    $("#UnloadButton").on("click", function () {
        var inputs = form.find('select[required]');
        var allValid = true;
        var area = $("#Area").val();
        var beginStation = $("#BeginStation").val();
        var endStation = $("#EndStation").val();

        if (area !== "F") {
            alert("下料區才可做下料完成!!")
            allValid = false;
        } else if (!beginStation) {
            alert("請輸入下料起點")
            allValid = false;
        }

        if (!allValid) {
            return;
        }

        // 手動顯示 modal
        var myModal = new bootstrap.Modal($('#unloadModalToggle'), {
            keyboard: false
        });
        myModal.show();
    });

    //點擊派車發送給後端派車資訊
    $(".SubmitButton").on("click", function () {
        var area = $("#Area").val();
        var status = $("#Status").val();
        // 暫時啟用被禁用的元素
        $("#EndStation").prop("disabled", false);
        $("#WorkOrder").prop("disabled", false);
        // 獲取表單資料
        var formData = form.serializeArray();
        // 將表單數據轉換為 JSON 格式
        var jsonData = {};
        $.each(formData, function () {
            jsonData[this.name] = this.value;
        });
        jsonData["btnName"] = btnName;
        jsonData["Status"] = status;
        // 恢復被禁用的元素
        $("#EndStation").prop("disabled", true);
        $("#WorkOrder").prop("disabled", true);

        var url = area === "F" ? "/Dispatch/UpdateoPort" : "/Dispatch/InsertoNeed";
        // 使用 AJAX 發送表單資料到後端
        $.ajax({
            type: "POST",
            url: url, // 替換為你的後端 URL
            data: JSON.stringify(jsonData),
            contentType: "application/json",
            success: function (response) {
                // 處理成功響應
                console.log("表單資料已成功送出", response);

                // 關閉確認派送 Modal
                var confirmModal = bootstrap.Modal.getInstance($('#dispatchModalToggle'));
                if (confirmModal) confirmModal.hide();
                var rejectModal = bootstrap.Modal.getInstance($('#RejectModalToggle'));
                if (rejectModal) rejectModal.hide();

                // 先儲存當前樓層，避免 reset 後遺失
                var currentFloor = $("#Floor").val();
                console.log("派送成功，保持當前樓層:", currentFloor);

                form[0].reset();

                // 清除站點快取，防止重複派工（下次會重新從 API 載入）
                isCacheLoaded = false;
                stationCache = {};
                console.log("已清除站點快取");

                // 恢復樓層選擇並觸發 change 事件重新載入地圖
                if (currentFloor) {
                    $("#Floor").val(currentFloor).trigger("change");
                    console.log("已恢復樓層選擇:", currentFloor);
                }

                var myModal = bootstrap.Modal.getOrCreateInstance($('#dispatchModalToggle2'), {
                    keyboard: false
                });
                myModal.show();
            },
            error: function (error) {
                // 處理錯誤響應
                console.error("表單資料送出失敗", error);
                // 關閉確認派送 Modal
                var confirmModal = bootstrap.Modal.getInstance($('#dispatchModalToggle'));
                if (confirmModal) confirmModal.hide();
                var rejectModal = bootstrap.Modal.getInstance($('#RejectModalToggle'));
                if (rejectModal) rejectModal.hide();
                if (error.responseJSON && error.responseJSON.message) { alert("派送失敗:" + error.responseJSON.message) }

            }
        });
    });

    $(document).on("click", ".ConfirmCancle", function () {
        rowData["index"] = $(this).closest("tr").index();

    })
    $("#CancleButton").on("click", function () {

        var $row = $("#DispatchStatus").find("tr").eq(rowData["index"])
        var status = $row.find("td:eq(4)").text();
        //if (status === "執行中") {
        //    alert("任務已執行。");
        //    // 關閉 Modal 視窗
        //    $('#cancleModal').modal('hide');
        //    return;
        //}

        var data = {}
        data["beginStation"] = $row.find("td:eq(1)").text();
        data["endStation"] = $row.find("td:eq(2)").text();

        $.ajax({
            type: "POST",
            url: "/Dispatch/DeleteoNeed",
            data: JSON.stringify(data),
            contentType: "application/json",
            success: function (response) {
                // 處理成功響應
                console.log("表單資料已成功送出", response);
                $('#cancleModal').modal('hide');
            },
            error: function (error) {
                // 處理錯誤響應
                console.error("表單資料送出失敗", error);
            }
        });

    })

    connection.on("SendDispatchChange", function () {
        UpdateDispatch();
        // 清除快取，強制重新載入
        stationCache = {};
        isCacheLoaded = false;
    });

    // 啟動相機按鈕
    $("#barcode-scan-btn").on("click", function () {
        console.log("啟動相機按鈕")
        startBarcodeScanner()
    })

    // 停止掃描按鈕
    $("#barcode-scan-stop-btn").on("click", function () {
        console.log("停止掃描按鈕")
        stopScanner()
    })

    // 使用條碼按鈕
    $("#barcode-scan-use-code-btn").on("click", function () {
        console.log("使用條碼按鈕")
        useScannedCode()
    })

    // ========== 掃描機台功能（2F 專用） ==========

    // 掃描模式：workOrder 或 machine
    var scanMode = "workOrder";

    // 掃描機台按鈕
    $("#machine-scan-btn").on("click", function () {
        console.log("點擊掃描機台按鈕");
        scanMode = "machine";
        startMachineScanner();
    });

    // 手動輸入機台號碼按鈕（測試用）
    $("#machine-manual-btn").on("click", function () {
        console.log("點擊手動輸入機台號碼按鈕");
        var machineInput = prompt("請輸入機台號碼（如 O1、P1）：");
        if (machineInput && machineInput.trim() !== "") {
            processManualMachineInput(machineInput.trim());
        }
    });

    // 處理手動輸入的機台號碼
    function processManualMachineInput(inputMachineName) {
        console.log("手動輸入機台號碼:", inputMachineName);

        // 轉大寫處理
        inputMachineName = inputMachineName.toUpperCase();

        // 比對 MachineName 找到對應站點
        var matchedStation = null;
        for (var stationNo in stationCache) {
            var station = stationCache[stationNo];
            // 直接比對站點名稱
            if (stationNo.toUpperCase() === inputMachineName) {
                matchedStation = stationNo;
                break;
            }
            // 比對 MachineName
            if (station.machineName && station.machineName.toUpperCase().includes(inputMachineName)) {
                matchedStation = stationNo;
                break;
            }
        }

        // 驗證 1：站點是否存在
        if (!matchedStation) {
            alert("找不到對應機台：" + inputMachineName + "\n請輸入有效的站點名稱（如 O1、P1）");
            return;
        }

        // 驗證 2：是否為有效的終點區域（O/P/S 區）
        var areaPrefix = matchedStation.substring(0, 1);
        if (areaPrefix !== "O" && areaPrefix !== "P" && areaPrefix !== "S") {
            alert("無效的終點區域：" + matchedStation + "\n只能派送到 O 區、P 區或 S 區");
            return;
        }

        // 驗證 3：站點是否為空架 (HaveFlag = 0)
        // 有 oPortBinding 綁定的站點允許 HaveFlag=1（空平板，後端會自動回收）
        var stationData = stationCache[matchedStation];
        if (stationData && stationData.haveFlag !== "0") {
            var isBindingPort = portBindingList.indexOf(matchedStation) !== -1;
            if (isBindingPort && stationData.haveFlag === "1") {
                // 有綁定且是空平板，允許選擇（後端 svrPair 會自動回收）
                console.log("站點 " + matchedStation + " 有空平板，但有綁定設定，允許派送");
            } else {
                alert("終點站點已有貨物：" + matchedStation + "\n請選擇空架");
                return;
            }
        }

        console.log("找到有效站點:", matchedStation);

        // 設定終點（保持 disabled 狀態，使用者無法手動變更）
        $("#EndStation").val(matchedStation);
        $("#EndStation").prop("disabled", true);  // 保持 disabled

        // 取得終點區域
        var destinationArea = matchedStation.substring(0, 1).toUpperCase();

        // 依據終點區域過濾起點選項
        filterBeginStationByDestination(destinationArea);

        // 啟用起點選擇
        $("#BeginStation").prop("disabled", false);

        // 提示使用者
        alert("已設定派送終點：" + matchedStation);
    }

    // 掃描機台掃描器
    function startMachineScanner() {
        // 顯示掃描 Modal
        var scanModal = new bootstrap.Modal(document.getElementById('barcodeModal'));
        scanModal.show();

        setTimeout(() => {
            initMachineScanner();
        }, 500);
    }

    // 初始化機台掃描器
    function initMachineScanner() {
        const scannerDiv = document.getElementById('qr-reader');
        if (!scannerDiv) return;

        const config = {
            fps: 10,
            qrbox: { width: 250, height: 250 },
            showTorchButtonIfSupported: true
        };

        const html5QrcodeScanner = new Html5QrcodeScanner("qr-reader", config, false);

        html5QrcodeScanner.render(function onScanSuccess(decodedText) {
            console.log('機台掃描成功:', decodedText);

            // 停止掃描器
            html5QrcodeScanner.clear();

            // 關閉掃描 Modal
            bootstrap.Modal.getInstance(document.getElementById('barcodeModal')).hide();

            // 使用共用函數處理掃描結果（與手動輸入使用相同驗證邏輯）
            processManualMachineInput(decodedText);

            scanMode = "workOrder";
        });
    }

    // ========== 站點物料管理功能 ==========

    // 目前操作的站點
    var currentLotStation = null;
    // 掃描目標欄位
    var currentScanTarget = null;
    // 操作模式：register, edit
    var lotOperationMode = null;

    // 開啟站點物料操作 Modal
    function openStationLotModal(stationNo) {
        console.log("開啟站點物料操作 Modal:", stationNo);
        currentLotStation = stationNo;

        // 從快取取得站點資訊
        var stationInfo = stationCache[stationNo];
        if (!stationInfo) {
            alert("找不到站點資訊：" + stationNo);
            return;
        }

        // 更新 Modal 顯示
        $("#stationLotName").text(stationNo);

        // 狀態顯示
        var statusText = {
            "0": "空架",
            "1": "空板",
            "3": "料盤"
        }[stationInfo.haveFlag] || "未知";
        $("#stationLotStatus").text(statusText);

        // 工單和貨架顯示
        var workOrderDisplay = stationInfo.workOrder || "無";
        // 判斷物料標記狀況
        var isVcutDone = stationInfo.workOrder && stationInfo.workOrder.includes("^VCUT^DONE");
        var isVcutMaterial = stationInfo.workOrder && stationInfo.workOrder.includes("^VCUT") && !isVcutDone;
        // 顯示時移除標記
        workOrderDisplay = workOrderDisplay.replace("^VCUT^DONE", "").replace("^VCUT", "");
        if (workOrderDisplay.length > 30) {
            workOrderDisplay = workOrderDisplay.substring(0, 30) + "...";
        }
        $("#stationLotCurrentWorkOrder").text(workOrderDisplay);
        $("#stationLotCurrentRackId").text(stationInfo.rackId || "無");

        // 判斷站點區域
        var stationArea = currentLotStation.substring(0, 1).toUpperCase();

        // V Cut 標記顯示（M 區和 T 區有料時顯示）
        if (stationInfo.haveFlag !== "0" && (stationArea === "M" || stationArea === "T")) {
            $("#vcutTagRow").show();
            if (isVcutDone) {
                $("#stationLotVcutTag").text("已加工完成").addClass("fw-bold").css("color", "#9b59b6");
            } else if (isVcutMaterial) {
                $("#stationLotVcutTag").text("待加工").addClass("text-warning fw-bold").css("color", "");
            } else {
                $("#stationLotVcutTag").text("否").removeClass("text-warning fw-bold").css("color", "");
            }
        } else {
            $("#vcutTagRow").hide();
        }

        // 清空輸入欄位
        $("#registerLotWorkOrder").val("");
        $("#registerLotRackId").val("");
        $("#registerLotVcut").prop("checked", false);

        // T 區會自動標记為已加工完成，不需要顯示 checkbox
        // J/Q/R/H/I/K/L 區不需要 V Cut 標記
        if (stationArea === "T" || stationArea === "J" || stationArea === "Q" || stationArea === "R" || stationArea === "H" || stationArea === "I" || stationArea === "K" || stationArea === "L") {
            $("#vcutCheckboxRow").hide();
        } else if (stationArea === "M") {
            $("#vcutCheckboxRow").show();
        } else {
            $("#vcutCheckboxRow").hide();
        }
        $("#lotFormSection").addClass("d-none");

        // 動態生成按鈕
        var footer = $("#stationLotFooter");
        footer.html('<button type="button" class="btn btn-secondary rounded-pill" data-bs-dismiss="modal">關閉</button>');

        // 判斷區域類型：M/T/J/Q/H/K/I 區為物料登記區，O/P/S/N/G/K/I 區為 Release 操作區
        var releaseAreas = ['O', 'P', 'S', 'N', 'G', 'K', 'I'];
        var isReleaseArea = releaseAreas.indexOf(stationArea) !== -1;

        if (isReleaseArea) {
            // I 區特別處理：支援物料登記（用於 NG 回送）
            if (stationArea === "I") {
                if (stationInfo.haveFlag === "3") {
                    // 料盤 - 可標記為空板
                    footer.prepend('<button type="button" class="btn btn-warning rounded-pill me-2" id="btnMarkEmptyTray">📦 標記空板</button>');
                } else if (stationInfo.haveFlag === "1") {
                    // 空板 - 可 Release 回送 或 物料登記（NG 回送）
                    footer.prepend('<button type="button" class="btn btn-success rounded-pill me-2" id="btnRelease">🚚 Release 回送</button>');
                    footer.prepend('<button type="button" class="btn btn-primary rounded-pill me-2" id="btnRegisterLot">📋 物料登記</button>');
                } else if (stationInfo.haveFlag === "0") {
                    // 空架 - 可物料登記
                    footer.prepend('<button type="button" class="btn btn-primary rounded-pill me-2" id="btnRegisterLot">📋 物料登記</button>');
                }
            } else {
                // O/P/S/N/G/K 區 - 顯示「標記空板」和「Release」按鈕
                if (stationInfo.haveFlag === "3") {
                    // 料盤 - 可標記為空板
                    footer.prepend('<button type="button" class="btn btn-warning rounded-pill me-2" id="btnMarkEmptyTray">📦 標記空板</button>');
                } else if (stationInfo.haveFlag === "1") {
                    // 空板 - 可 Release 回送
                    footer.prepend('<button type="button" class="btn btn-success rounded-pill me-2" id="btnRelease">🚚 Release 回送</button>');
                }
                // HaveFlag=0 (空架) 時不顯示任何操作按鈕
            }
        } else {
            // M/T/J/Q/R 區 - 物料登記操作
            if (stationInfo.haveFlag === "0" || stationInfo.haveFlag === "1") {
                // 空架或空板 - 顯示「物料登記」
                footer.prepend('<button type="button" class="btn btn-primary rounded-pill me-2" id="btnRegisterLot">📋 物料登記</button>');
            } else if (stationArea === "T" && stationInfo.haveFlag === "3") {
                // T 區特別處理：根據 WorkOrder 是否包含 DONE 移除標記
                var workOrder = stationInfo.workOrder || "";
                if (workOrder.indexOf("DONE") === -1) {
                    // 未完成加工：顯示「標記空板」（讓人員取走物料去加工）
                    footer.prepend('<button type="button" class="btn btn-warning rounded-pill me-2" id="btnMarkEmptyTray">📦 標記空板</button>');
                } else {
                    // 已完成加工：顯示「物料修改」和「清除物料」
                    footer.prepend('<button type="button" class="btn btn-danger rounded-pill me-2" id="btnClearLot">🗑️ 清除物料</button>');
                    footer.prepend('<button type="button" class="btn btn-warning rounded-pill me-2" id="btnEditLot">✏️ 物料修改</button>');
                }
            } else {
                // M/J/Q/R 區 - 有物料時顯示「物料修改」和「清除物料」
                footer.prepend('<button type="button" class="btn btn-danger rounded-pill me-2" id="btnClearLot">🗑️ 清除物料</button>');
                footer.prepend('<button type="button" class="btn btn-warning rounded-pill me-2" id="btnEditLot">✏️ 物料修改</button>');
            }
        }

        // 顯示 Modal
        var modal = new bootstrap.Modal(document.getElementById('stationLotModal'));
        modal.show();
    }

    // 將函數綁定到 window，供地圖點擊使用
    window.openStationLotModal = openStationLotModal;

    // 物料登記按鈕
    $(document).on("click", "#btnRegisterLot", function () {
        console.log("點擊物料登記");
        lotOperationMode = "register";
        $("#lotFormSection").removeClass("d-none");
    });

    // 物料修改按鈕
    $(document).on("click", "#btnEditLot", function () {
        console.log("點擊物料修改");
        lotOperationMode = "edit";

        // 帶入現有值
        var stationInfo = stationCache[currentLotStation];
        if (stationInfo) {
            var workOrder = stationInfo.workOrder || "";
            var isVcutDone = workOrder.includes("^VCUT^DONE");
            var isVcut = workOrder.includes("^VCUT") && !isVcutDone;
            // 移除標記以便編輯
            workOrder = workOrder.replace("^VCUT^DONE", "").replace("^VCUT", "");
            $("#registerLotWorkOrder").val(workOrder);
            $("#registerLotRackId").val(stationInfo.rackId || "");
            // M 區才顯示 V Cut checkbox
            var stationArea = currentLotStation.substring(0, 1).toUpperCase();
            if (stationArea === "M") {
                $("#vcutCheckboxRow").show();
                $("#registerLotVcut").prop("checked", isVcut);
            } else {
                $("#vcutCheckboxRow").hide();
            }
        }

        $("#lotFormSection").removeClass("d-none");
    });

    // 清除物料按鈎 - 開啟確認視窗
    $(document).on("click", "#btnClearLot", function () {
        console.log("點擊清除物料");
        $("#clearLotStationName").text(currentLotStation);

        // 隱藏站點操作 Modal，顯示確認 Modal
        bootstrap.Modal.getInstance(document.getElementById('stationLotModal')).hide();
        setTimeout(() => {
            var confirmModal = new bootstrap.Modal(document.getElementById('clearLotConfirmModal'));
            confirmModal.show();
        }, 300);
    });

    // 確認清除物料
    $(document).on("click", "#confirmClearLotBtn", function () {
        console.log("確認清除物料:", currentLotStation);

        $.ajax({
            type: "POST",
            url: "/Dispatch/ClearLot",
            contentType: "application/json",
            data: JSON.stringify({ stationNo: currentLotStation }),
            success: function (response) {
                alert("清除成功！站點：" + currentLotStation);
                bootstrap.Modal.getInstance(document.getElementById('clearLotConfirmModal')).hide();
                // 重新載入地圖
                refreshMap();
            },
            error: function (jqXHR, textStatus, errorThrown) {
                var message = "清除失敗";
                if (jqXHR.responseJSON && jqXHR.responseJSON.message) {
                    message = jqXHR.responseJSON.message;
                }
                alert(message);
                bootstrap.Modal.getInstance(document.getElementById('clearLotConfirmModal')).hide();
            }
        });
    });

    // ===== O/P/S/N 區操作：標記空板 =====
    $(document).on("click", "#btnMarkEmptyTray", function () {
        console.log("點擊標記空板:", currentLotStation);

        $.ajax({
            type: "POST",
            url: "/Dispatch/MarkEmptyTray",
            contentType: "application/json",
            data: JSON.stringify({
                stationNo: currentLotStation
            }),
            success: function (response) {
                alert("已標記為空板！站點：" + currentLotStation);
                bootstrap.Modal.getInstance(document.getElementById('stationLotModal')).hide();
                // 重新載入地圖
                refreshMap();
            },
            error: function (error) {
                var message = error.responseJSON?.message || "標記失敗";
                alert(message);
            }
        });
    });

    // ===== O/P/S/N 區操作：Release 回送 =====
    $(document).on("click", "#btnRelease", function () {
        console.log("點擊 Release 回送:", currentLotStation);

        $.ajax({
            type: "POST",
            url: "/Dispatch/Release",
            contentType: "application/json",
            data: JSON.stringify({
                stationNo: currentLotStation
            }),
            success: function (response) {
                alert("Release 成功！\n起點：" + currentLotStation + "\n終點：" + response.endStation);
                bootstrap.Modal.getInstance(document.getElementById('stationLotModal')).hide();
                // 重新載入地圖
                refreshMap();
            },
            error: function (error) {
                var message = error.responseJSON?.message || "Release 失敗";
                alert(message);
            }
        });
    });

    // 注意：不要在這裡添加對 btn-primary 的點擊處理，因為 #btnRegisterLot 也是 btn-primary
    // 專用的提交按鈕是 #submitLotBtn

    // 在表單區域顯示時，加入提交按鈕並隱藏原按鈕
    function showLotFormWithSubmit() {
        var footer = $("#stationLotFooter");

        // 隱藏「物料登記」和「物料修改」按鈕
        $("#btnRegisterLot").hide();
        $("#btnEditLot").hide();

        // 如果還沒有提交按鈕，加入一個
        if (footer.find("#submitLotBtn").length === 0) {
            footer.prepend('<button type="button" class="btn btn-success rounded-pill me-2" id="submitLotBtn">✅ 確認儲存</button>');
        }
    }

    // 修改物料登記/修改按鈕，加入提交按鈕
    $(document).on("click", "#btnRegisterLot, #btnEditLot", function () {
        setTimeout(() => {
            showLotFormWithSubmit();
        }, 100);
    });

    // 提交按鈕
    $(document).on("click", "#submitLotBtn", function () {
        submitLotForm();
    });

    // 提交物料表單
    function submitLotForm() {
        var workOrder = $("#registerLotWorkOrder").val().trim();
        var rackId = $("#registerLotRackId").val().trim();
        var isVcutMaterial = $("#registerLotVcut").is(":checked");

        // 驗證：工單必填（R 區例外，工單選填）
        var stationArea = currentLotStation.substring(0, 1).toUpperCase();
        console.log("submitLotForm - currentLotStation:", currentLotStation, "stationArea:", stationArea);
        if (!workOrder && stationArea !== "R") {
            alert("請輸入工單條碼");
            $("#registerLotWorkOrder").focus();
            return;
        }

        console.log("提交物料表單:", currentLotStation, workOrder, rackId, "V Cut:", isVcutMaterial);

        $.ajax({
            type: "POST",
            url: "/Dispatch/RegisterLot",
            contentType: "application/json",
            data: JSON.stringify({
                stationNo: currentLotStation,
                workOrder: workOrder,
                rackId: rackId,
                isVcutMaterial: isVcutMaterial ? "true" : "false"
            }),
            success: function (response) {
                alert((lotOperationMode === "edit" ? "修改" : "登記") + "成功！站點：" + currentLotStation);
                bootstrap.Modal.getInstance(document.getElementById('stationLotModal')).hide();
                // 重新載入地圖
                refreshMap();
            },
            error: function (error) {
                var message = error.responseJSON?.message || "操作失敗";
                alert(message);
            }
        });
    }

    // 重新載入地圖
    function refreshMap() {
        // 使用 window 層級變數追蹤的當前地圖區域
        var area = window.currentMapArea || 'FHT2-1F';
        console.log("refreshMap - 重新載入地圖:", area);
        loadMapDataLocal(area);
        stationCache = {};
        isCacheLoaded = false;
    }

    // 掃描工單條碼按鈕 (使用事件委派)
    $(document).on("click", "#scanWorkOrderBtn", function () {
        console.log("掃描工單條碼按鈕被點擊");
        currentScanTarget = "workOrder";
        startLotScanner();
    });

    // 掃描貨架條碼按鈕 (使用事件委派)
    $(document).on("click", "#scanRackIdBtn", function () {
        console.log("掃描貨架條碼按鈕被點擊");
        currentScanTarget = "rackId";
        startLotScanner();
    });

    // 物料掃描器
    function startLotScanner() {
        // 隱藏站點操作 Modal
        var stationModal = bootstrap.Modal.getInstance(document.getElementById('stationLotModal'));
        if (stationModal) {
            stationModal.hide();
        }

        // 顯示掃描 Modal
        setTimeout(() => {
            var scanModal = new bootstrap.Modal(document.getElementById('barcodeModal'));
            scanModal.show();
            setTimeout(() => {
                initLotScanner();
            }, 500);
        }, 300);
    }

    // 初始化物料掃描器
    function initLotScanner() {
        const scannerDiv = document.getElementById('qr-reader');
        if (!scannerDiv) return;

        const config = {
            fps: 10,
            qrbox: { width: 250, height: 250 },
            showTorchButtonIfSupported: true
        };

        const html5QrcodeScanner = new Html5QrcodeScanner("qr-reader", config, false);

        html5QrcodeScanner.render(function onScanSuccess(decodedText) {
            console.log('物料掃描成功:', decodedText);

            // 停止掃描器
            html5QrcodeScanner.clear();

            // 根據目標欄位填入值
            if (currentScanTarget === "workOrder") {
                $("#registerLotWorkOrder").val(decodedText);
            } else if (currentScanTarget === "rackId") {
                $("#registerLotRackId").val(decodedText);
            }

            // 關閉掃描 Modal
            bootstrap.Modal.getInstance(document.getElementById('barcodeModal')).hide();

            // 重新顯示站點操作 Modal
            setTimeout(() => {
                var stationModal = new bootstrap.Modal(document.getElementById('stationLotModal'));
                stationModal.show();
            }, 300);

            currentScanTarget = null;
        });
    }

    // 自動選擇預設樓層（從後端傳入）- 必須在所有事件綁定完成之後執行
    // 自動選擇預設樓層（從後端傳入）- 必須在所有事件綁定完成之後執行
    if (window.defaultFloor && window.defaultFloor !== "") {
        console.log("自動選擇預設樓層:", window.defaultFloor);
        $("#Floor").val(window.defaultFloor);
    }

    // Fallback: 如果沒有選擇任何樓層（且有選項），自動選擇第一個
    if (!$("#Floor").val() && $("#Floor option").length > 0) {
        $("#Floor").prop('selectedIndex', 0);
        console.log("Fallback: 自動選擇第一個樓層:", $("#Floor").val());
    }

    // 觸發 change 事件，確保後續的地圖載入和區域篩選邏輯被執行
    if ($("#Floor").val()) {
        $("#Floor").trigger("change");
    }
});

/**
 * 更新站點資料快取
 */
function updateStationCache(stations) {
    stationCache = {};
    stations.forEach(function (s) {
        stationCache[s.stationNo] = {
            haveFlag: s.haveFlag,
            reserve: s.reserve,
            workOrder: s.workOrder,
            rackId: s.rackId,
            putTime: s.putTime,
            block: s.block,
            machineName: s.machineName
        };
    });
    isCacheLoaded = true;
    console.log('站點資料已更新:', Object.keys(stationCache).length, '個站點');
}

/**
 * 篩選起點選項
 */
function filterBeginStationOptions(selectedValue) {
    var workoderMap = new Map();

    // 檢查使用者是否有 ROUTE_2F_VCUT 權限（用於 V Cut 物料過濾）
    var hasVcutPermission = window.userRouteIds && window.userRouteIds.includes("ROUTE_2F_VCUT");
    // 檢查使用者是否有 ROUTE_2F_MT_TO_OP 權限（用於 T 區 V Cut 完成品 -> O/P 區）
    var hasMtOpPermission = window.userRouteIds && window.userRouteIds.includes("ROUTE_2F_MT_TO_OP");
    console.log("V Cut 權限檢查:", hasVcutPermission, "MT_TO_OP 權限檢查:", hasMtOpPermission, "路線清單:", window.userRouteIds);

    // MT 選項特別處理：顯示三種物料類型
    // 1. M 區一般物料（不含 ^VCUT）→ 派送到 O/P 區
    // 2. M 區 V Cut 物料（含 ^VCUT 但不含 ^VCUT^DONE）→ 派送到 T 區（需要 ROUTE_2F_VCUT 權限）
    // 3. T 區已完成物料（含 ^VCUT^DONE）→ 派送到 O/P 區（需要 ROUTE_2F_VCUT 權限）
    if (selectedValue === "MT") {
        $('#BeginStation option').filter(function () {
            var tracname = $(this).val();
            if (!tracname) return false;

            var station = stationCache[tracname];
            if (!station || station.haveFlag !== "3") return false;

            var workOrder = station.workOrder || "";
            var isValid = false;
            var vcutLabel = "";

            if (tracname.startsWith("M")) {
                if (workOrder.includes("^VCUT") && !workOrder.includes("^VCUT^DONE")) {
                    // M 區 V Cut 物料（待加工）→ 派送到 T 區
                    // ★ 需要 ROUTE_2F_VCUT 權限才能看到 ★
                    if (hasVcutPermission) {
                        isValid = true;
                        vcutLabel = " [V Cut待加工]";
                    }
                } else if (!workOrder.includes("^VCUT")) {
                    // M 區一般物料 → 派送到 O/P 區
                    isValid = true;
                }
            } else if (tracname.startsWith("T")) {
                // T 區：只顯示含 ^VCUT^DONE 標記的已加工物料 → 派送到 O/P 區
                // ★ 需要 ROUTE_2F_VCUT 或 ROUTE_2F_MT_TO_OP 權限才能看到 ★
                if ((hasVcutPermission || hasMtOpPermission) && workOrder.includes("^VCUT^DONE")) {
                    isValid = true;
                    vcutLabel = " [V cut加工完成]";
                }
            }

            if (isValid) {
                // 更新顯示文字
                var displayWorkOrder = workOrder.replace("^VCUT^DONE", "").replace("^VCUT", "");
                if (displayWorkOrder.length > 35) {
                    displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                }
                $(this).text(tracname + " - " + displayWorkOrder + vcutLabel);
            }
            return isValid;
        }).show();
        return;
    }

    switch (selectedValue.substring(0, 1)) {
        case "C":
            // C 區：只顯示 B 區且 HaveFlag = 3 的站點
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                if (tracname.charAt(0) !== "B" || station.haveFlag != 3) return false;

                var haveflag = station.haveFlag;
                var workorder = (station.workOrder && station.workOrder.split("^")[3]) || "undefined";
                var lot = (station.workOrder && station.workOrder.split("^")[2]) || "undefined";
                var puttime = station.putTime;

                if (tracname.startsWith("B") && haveflag === "3") {
                    if (!workoderMap.has(workorder) || puttime < workoderMap.get(workorder).puttime) {
                        workoderMap.set(workorder, { tracname, lot, puttime });
                    }
                    return true;
                }
                return false;
            }).each(function () {
                var tracname = $(this).val();
                var station = stationCache[tracname];
                if (!station) return;

                var workorder = (station.workOrder && station.workOrder.split("^")[3]) || "undefined";
                if (workoderMap.has(workorder) && workoderMap.get(workorder).tracname === tracname) {
                    var lot = workoderMap.get(workorder).lot;
                    $(this).text(`${tracname}-${workorder}-${lot}`);
                    $(this).show();
                } else {
                    $(this).hide();
                }
            });
            break;
        case "H":  // H 區（成型後）作為起點：只顯示有料的站點 (HaveFlag = 3)
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // H 區且有料 (HaveFlag = 3)
                if (!tracname.startsWith("H") || station.haveFlag !== "3") return false;

                // 更新顯示文字：StationNo + WorkOrder
                var workOrder = station.workOrder || "";
                var displayWorkOrder = workOrder;
                // 截斷過長的文字
                if (displayWorkOrder.length > 35) {
                    displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                }
                $(this).text(tracname + " - " + displayWorkOrder);
                return true;
            }).show();
            break;

        case "M":
            // M 區（雷雕區）作為起點：顯示有料的站點
            // 更新顯示格式為 StationNo + WorkOrder
            // ★ V Cut 物料需要 ROUTE_2F_VCUT 權限 ★
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // M 區且有料 (HaveFlag = 3)
                if (!tracname.startsWith("M") || station.haveFlag !== "3") return false;

                var workOrder = station.workOrder || "";

                // ★ V Cut 權限檢查：ROUTE_2F_VCUT 只能看到「V Cut 待加工」物料
                var isVcutPending = workOrder.includes("^VCUT") && !workOrder.includes("^VCUT^DONE");

                if (isVcutPending) {
                    if (!hasVcutPermission) {
                        return false;  // 隱藏 V Cut 待加工物料
                    }
                } else {
                    // ★ 其他物料（一般物料 or V Cut 已完成）：只有 ROUTE_2F_MT_TO_OP 可見 ★
                    if (!hasMtOpPermission) {
                        return false;
                    }
                }

                // 更新顯示文字
                // 移除 ^VCUT 標記以便顯示
                var displayWorkOrder = workOrder.replace("^VCUT^DONE", "").replace("^VCUT", "");
                // 截斷過長的文字
                if (displayWorkOrder.length > 35) {
                    displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                }
                // 標示 V Cut 物料
                var vcutLabel = "";
                if (workOrder.includes("^VCUT^DONE")) {
                    vcutLabel = " [已完成]";
                } else if (workOrder.includes("^VCUT")) {
                    vcutLabel = " [V Cut]";
                }
                $(this).text(tracname + " - " + displayWorkOrder + vcutLabel);
                return true;
            }).show();
            break;

        case "T":
            // T 區（V Cut區）作為起點：顯示有 ^VCUT^DONE 標記的物料
            // ★ T 區（V Cut區）需要 ROUTE_2F_VCUT 權限才能看到 ★
            if (!hasVcutPermission) {
                // 無權限，不顯示任何 T 區選項
                break;
            }
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // T 區且有料且標記為已加工完成
                if (!tracname.startsWith("T") || station.haveFlag !== "3") return false;

                var workOrder = station.workOrder || "";
                return workOrder.includes("^VCUT^DONE");
            }).show();
            break;

        case "J":
            // J 區（3F 插針室）作為起點：顯示有料的站點 (HaveFlag = 3)
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // J 區且有料 (HaveFlag = 3)
                if (!tracname.startsWith("J") || station.haveFlag !== "3") return false;

                // 更新顯示文字：StationNo + WorkOrder
                var workOrder = station.workOrder || "";
                var displayWorkOrder = workOrder;
                // 截斷過長的文字
                if (displayWorkOrder.length > 35) {
                    displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                }
                $(this).text(tracname + " - " + displayWorkOrder);
                return true;
            }).show();
            break;

        case "Q":
            // Q 區（出貨區）作為起點：顯示有料的站點 (HaveFlag = 3)
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // Q 區且有料 (HaveFlag = 3)
                if (!tracname.startsWith("Q") || station.haveFlag !== "3") return false;

                // 更新顯示文字：StationNo + WorkOrder
                var workOrder = station.workOrder || "";
                var displayWorkOrder = workOrder;
                // 截斷過長的文字
                if (displayWorkOrder.length > 35) {
                    displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                }
                $(this).text(tracname + " - " + displayWorkOrder);
                return true;
            }).show();
            break;

        case "R":
            // R 區（廢料區）作為起點：顯示有料的站點 (HaveFlag = 3)
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // R 區且有料 (HaveFlag = 3)
                if (!tracname.startsWith("R") || station.haveFlag !== "3") return false;

                // 更新顯示文字：StationNo + WorkOrder（如有）
                var workOrder = station.workOrder || "";
                var displayText = tracname;
                if (workOrder) {
                    var displayWorkOrder = workOrder;
                    if (displayWorkOrder.length > 35) {
                        displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                    }
                    displayText = tracname + " - " + displayWorkOrder;
                }
                $(this).text(displayText);
                return true;
            }).show();
            break;

        case "L":
            // L 區（4F 烘烤後）作為起點：只顯示 L1-L4 有料且非回送物料
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // L 區且有料 (HaveFlag = 3)
                if (!tracname.startsWith("L") || station.haveFlag !== "3") return false;

                // 只顯示 L1-L4
                var portNum = parseInt(tracname.replace("L", ""));
                if (portNum < 1 || portNum > 4) return false;

                // ★ 排除回送物料 ★
                var workOrder = station.workOrder || "";
                if (workOrder.includes("^RETURN") || workOrder.includes("^NG")) {
                    return false;  // 回送物料不可再次派送
                }

                // 更新顯示文字：StationNo + WorkOrder
                var displayWorkOrder = workOrder;
                if (displayWorkOrder.length > 35) {
                    displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                }
                $(this).text(tracname + " - " + displayWorkOrder);
                return true;
            }).show();
            break;

        case "I":
            // I 區（3F 品檢區）作為起點：顯示有料的站點 (HaveFlag = 3)
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // I 區且有料 (HaveFlag = 3)
                if (!tracname.startsWith("I") || station.haveFlag !== "3") return false;

                // 更新顯示文字：StationNo + WorkOrder
                var workOrder = station.workOrder || "";
                var displayWorkOrder = workOrder;
                if (displayWorkOrder.length > 35) {
                    displayWorkOrder = displayWorkOrder.substring(0, 35) + "...";
                }
                $(this).text(tracname + " - " + displayWorkOrder);
                return true;
            }).show();
            break;

        default:
            // 其他區：顯示符合區域且 HaveFlag 不為 0 的站點
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                return tracname.startsWith(selectedValue) && station.haveFlag !== "0";
            }).show();
            break;
    }
}

/**
 * 依據終點區域過濾起點選項（用於掃描機台後的帳料選擇）
 * @param {string} destinationArea - 終點區域代號（O、P、T）
 */
function filterBeginStationByDestination(destinationArea) {
    $('#BeginStation option').hide();

    if (destinationArea === "T") {
        // 終點是 T 區（V Cut區）：只顯示 M 區含 ^VCUT 標記的物料
        $('#BeginStation option').filter(function () {
            var tracname = $(this).val();
            if (!tracname || !tracname.startsWith("M")) return false;

            var station = stationCache[tracname];
            if (!station || station.haveFlag !== "3") return false;

            var workOrder = station.workOrder || "";
            // 含 ^VCUT 但不含 ^VCUT^DONE（待加工）
            return workOrder.includes("^VCUT") && !workOrder.includes("^VCUT^DONE");
        }).show();
    } else if (destinationArea === "O" || destinationArea === "P") {
        // 終點是 O/P 區：顯示 M 區一般物料 + T 區已加工完成
        $('#BeginStation option').filter(function () {
            var tracname = $(this).val();
            if (!tracname) return false;

            var station = stationCache[tracname];
            if (!station || station.haveFlag !== "3") return false;

            var workOrder = station.workOrder || "";

            if (tracname.startsWith("M")) {
                // M 區：只顯示不含 ^VCUT 標記的一般物料
                return !workOrder.includes("^VCUT");
            } else if (tracname.startsWith("T")) {
                // T 區：只顯示含 ^VCUT^DONE 標記的已加工物料
                return workOrder.includes("^VCUT^DONE");
            }
            return false;
        }).show();
    } else if (destinationArea === "S") {
        // 終點是 S 區（清洗區）：【僅顯示】 T 區已加工完成物料
        $('#BeginStation option').filter(function () {
            var tracname = $(this).val();
            if (!tracname || !tracname.startsWith("T")) return false;

            var station = stationCache[tracname];
            if (!station || station.haveFlag !== "3") return false;

            var workOrder = station.workOrder || "";
            // T 區：只顯示含 ^VCUT^DONE 標記的已加工物料
            return workOrder.includes("^VCUT^DONE");
        }).show();
    }
}

/**
 * 自動選擇終點站（統一處理邏輯）
 */
function autoSelectEndStation(prefix, requiredHaveFlag, requiredReserve) {
    var found = false;
    $('#EndStation option').each(function () {
        if (found) return false; // 已找到就跳出

        var tracname = $(this).val();
        if (!tracname || !tracname.startsWith(prefix)) return true;

        var station = stationCache[tracname];
        if (!station) return true;

        var haveFlagMatch = !requiredHaveFlag || station.haveFlag === requiredHaveFlag;
        var reserveMatch = !requiredReserve || station.reserve === requiredReserve;

        if (haveFlagMatch && reserveMatch) {
            $('#EndStation').val(tracname);
            found = true;
            return false;
        }
    });

    if (!found) {
        console.warn(`找不到符合條件的 ${prefix} 區站點`);
    }
}

/**
 * 篩選終點選項（用於 EndStation focus 事件）
 */
function filterEndStationOptions(prefix, requiredHaveFlag) {
    $('#EndStation option').filter(function () {
        var tracname = $(this).val();
        if (!tracname || !tracname.startsWith(prefix)) return false;

        if (!requiredHaveFlag) return true; // 不需檢查 HaveFlag

        var station = stationCache[tracname];
        if (!station) return false;

        return station.haveFlag === requiredHaveFlag;
    }).show();
}

/**
 * 自動選擇終點站（限定編號範圍）
 * @param {string} prefix - 終點區域前綴
 * @param {string} requiredHaveFlag - HaveFlag 條件
 * @param {string} requiredReserve - Reserve 梺件 (Y/N)
 * @param {number} minPort - 最小站點編號
 * @param {number} maxPort - 最大站點編號
 */
function autoSelectEndStationInRange(prefix, requiredHaveFlag, requiredReserve, minPort, maxPort) {
    var found = false;
    $('#EndStation option').each(function () {
        if (found) return false;

        var tracname = $(this).val();
        if (!tracname || !tracname.startsWith(prefix)) return true;

        var station = stationCache[tracname];
        if (!station) return true;

        // 檢查站點編號範圍
        var portNum = parseInt(tracname.replace(prefix, ""));
        if (portNum < minPort || portNum > maxPort) return true;

        var haveFlagMatch = !requiredHaveFlag || station.haveFlag === requiredHaveFlag;
        var reserveMatch = !requiredReserve || station.reserve === requiredReserve;

        if (haveFlagMatch && reserveMatch) {
            $('#EndStation').val(tracname);
            found = true;
            return false;
        }
    });

    if (!found) {
        console.warn(`找不到符合條件的 ${prefix}${minPort}-${prefix}${maxPort} 區站點`);
    }
}

//即時更新右側任務列表
function UpdateDispatch() {
    $.ajax({
        type: "GET",
        url: "/Dispatch/UpdateDispatch",
        success: function (data) {
            $('#DispatchStatus').html(data);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.error("AJAX 請求失敗: ", textStatus, errorThrown);
        }
    });
}

// 條碼掃描相關的 JavaScript 代碼

// 使用 html5-qrcode 的條碼掃描函數
async function startBarcodeScanner() {
    console.log('開始啟動 html5-qrcode 掃描器');

    try {
        // 檢查函式庫是否載入
        if (typeof Html5QrcodeScanner === 'undefined') {
            alert('條碼掃描函式庫未載入，請重新整理頁面');
            return;
        }

        showScannerModal();

        // 等待模態視窗完全顯示後再初始化掃描器
        setTimeout(() => {
            initHtml5QrcodeScanner();
        }, 500);

    } catch (error) {
        console.error('條碼掃描器啟動失敗:', error);
        alert('啟動失敗: ' + error.message);
    }
}

function showScannerModal() {
    console.log('顯示掃描器模態視窗');
    const modalElement = document.getElementById('barcodeModal');
    if (!modalElement) {
        alert('找不到條碼掃描模態視窗');
        return;
    }
    const modal = new bootstrap.Modal(modalElement);
    modal.show();
}

function initHtml5QrcodeScanner() {
    const scannerDiv = document.getElementById('qr-reader');
    if (!scannerDiv) {
        alert('找不到掃描器容器');
        return;
    }

    // html5-qrcode 配置
    const config = {
        fps: 10,    // 每秒掃描次數
        qrbox: {    // 掃描框大小
            width: 250,
            height: 250
        },
        showTorchButtonIfSupported: true,
        // 相機配置
        aspectRatio: 1.0,
        disableFlip: false
    };

    // 建立掃描器實例
    const html5QrcodeScanner = new Html5QrcodeScanner(
        "qr-reader",
        config,
        false // verbose
    );

    // 掃描成功回調
    function onScanSuccess(decodedText, decodedResult) {
        console.log('掃描成功:', decodedText);
        console.log('掃描結果詳情:', decodedResult);

        // 停止掃描器
        html5QrcodeScanner.clear().then(() => {
            console.log('掃描器已清理');
        }).catch(error => {
            console.error('清理掃描器時發生錯誤:', error);
        });

        // 填入工單欄位
        const workOrderInput = document.getElementById('WorkOrder');
        if (workOrderInput) {
            workOrderInput.value = decodedText;
            $(workOrderInput).trigger('blur');
        }

        // 顯示成功訊息
        showScanResult(decodedText, decodedResult.result.format?.formatName || '未知格式');

        // 延遲關閉模態視窗
        setTimeout(() => {
            stopScanner();
        }, 2000);
    }

    // 掃描錯誤回調（可選）
    function onScanFailure(error) {
        // 這是正常的，不需要處理每個掃描失敗
        // console.log('掃描失敗:', error);
    }

    // 開始渲染掃描器
    html5QrcodeScanner.render(onScanSuccess, onScanFailure);

    // 存儲掃描器實例以便後續操作
    window.currentScanner = html5QrcodeScanner;

    console.log('html5-qrcode 掃描器已啟動');
}

function showScanResult(code, format) {
    const modalBody = document.querySelector('#barcodeModal .modal-body');
    if (modalBody) {
        // 移除之前的結果
        const existingResult = modalBody.querySelector('.scan-result');
        if (existingResult) {
            existingResult.remove();
        }

        const resultDiv = document.createElement('div');
        resultDiv.className = 'alert alert-success mt-3 scan-result';
        resultDiv.innerHTML = `
                <h5><i class="fa-solid fa-check-circle"></i> 掃描成功！</h5>
                <p><strong>內容：</strong>${code}</p>
                <p><strong>格式：</strong>${format}</p>
            `;
        modalBody.appendChild(resultDiv);
    }
}

// 停止掃描器並清理資源
function stopScanner() {
    console.log('停止條碼掃描器');

    // 清理 html5-qrcode 掃描器
    if (window.currentScanner) {
        window.currentScanner.clear().then(() => {
            console.log('掃描器已成功清理');
            window.currentScanner = null;
        }).catch(error => {
            console.error('清理掃描器時發生錯誤:', error);
            window.currentScanner = null;
        });
    }

    // 隱藏模態視窗
    const modal = bootstrap.Modal.getInstance(document.getElementById('barcodeModal'));
    if (modal) {
        modal.hide();
    }

    // 清理模態視窗內容
    setTimeout(() => {
        const modalBody = document.querySelector('#barcodeModal .modal-body');
        if (modalBody) {
            const scanResult = modalBody.querySelector('.scan-result');
            if (scanResult) {
                scanResult.remove();
            }
        }
    }, 500);
}