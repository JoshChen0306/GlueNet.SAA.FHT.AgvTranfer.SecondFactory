$(function () {
    var insertdata = {};
    var updatedata = {};
    var deletedata = {};
    var originformdata = {};
    var originalData = null;
    var insertCounter = 0;
    var fieldToColumnIndex = {
        'MoveType': 1,
        'FromFloor': 2,
        'ToFloor': 3,
        'TaskType': 4,
        'UseFlag': 5,
        'Remark': 6,
    };

    $(".edit-btn,.delete-btn,#confirm-btn,#cancel-btn").css("visibility", "hidden");

    // 維護按鈕點擊事件
    $("#maintain-btn").on("click", function () {
        originalData = $('#maintain-table').clone(true);

        $(".edit-btn,.delete-btn").css("visibility", function (i, visibility) {
            if (visibility === "hidden") {
                $(this).css("visibility", "visible").hide().fadeIn(300);
            }
            else {
                $(this).fadeOut(300, function () {
                    $(this).css("visibility", "hidden").show();
                });
            }
        })
        $('#maintain-save ,#maintain-cancel,#maintain-btn,#insert-btn').toggleClass('d-none');
    });

    // 儲存按鈕點擊事件
    $('#maintain-save').on('click', function () {
        if (Object.keys(insertdata).length > 0 || Object.keys(updatedata).length > 0 || Object.keys(deletedata).length > 0) {
            $.ajax({
                type: 'POST',
                url: '/TaskTypeRoute/DataChange',
                data: JSON.stringify({
                    insertdata: insertdata,
                    updatedata: updatedata,
                    deletedata: deletedata.ids,
                }),
                contentType: 'application/json',
                success: function (response) {
                    insertdata = {};
                    updatedata = {};
                    deletedata = {};
                    location.reload();
                },
                error: function (jqXHR, textStatus, errorThrown) {
                    var msg = 'AJAX 請求失敗';
                    if (jqXHR.responseJSON && jqXHR.responseJSON.message) {
                        msg = jqXHR.responseJSON.message;
                    }
                    alert(msg);
                    console.error("AJAX 請求失敗: ", textStatus, errorThrown);
                }
            });
        }

        $('#maintain-save ,#maintain-cancel,#maintain-btn ,#insert-btn').toggleClass('d-none');
        $(".edit-btn,.delete-btn").fadeOut(300, function () {
            $(this).css("visibility", "hidden").show();
        });
    });

    // 取消按鈕點擊事件
    $('#maintain-cancel').on('click', function () {
        insertdata = {};
        updatedata = {};
        deletedata = {};
        if (originalData) {
            setTimeout(function () {
                $('#maintain-table').replaceWith(originalData);
                originalData = null;
            }, 150);
        }
        $('#maintain-save ,#maintain-cancel,#maintain-btn').toggleClass('d-none');
        $(".edit-btn,.delete-btn").fadeOut(300, function () {
            $(this).css("visibility", "hidden").show();
        });
    });

    // 編輯按鈕點擊事件
    $(document).on('click', '.edit-btn', function () {
        var form = $(this).closest('tr').next().find('form');
        var icon = $(this).find('i');
        var row = $(this).closest('tr');
        if ($(this).hasClass("collapsed")) {
            var formData = form.serialize();
            if (originformdata !== formData) {
                var formData = form.serializeArray();
                var formDataObject = {};
                $.each(formData, function (i, item) {
                    formDataObject[item.name] = item.value;
                    var columnIndex = fieldToColumnIndex[item.name];
                    if (columnIndex !== undefined) {
                        if (item.name === "UseFlag") {
                            if (item.value === "Y") {
                                row.find('td').eq(columnIndex).html('<span class="badge bg-success">啟用</span>');
                            } else {
                                row.find('td').eq(columnIndex).html('<span class="badge bg-secondary">停用</span>');
                            }
                        } else {
                            row.find('td').eq(columnIndex).text(item.value);
                        }
                    }
                });
                // 檢查同 MoveType + FromFloor + ToFloor 是否重複（排除自己）
                if (isDuplicateRoute(formDataObject.MoveType, formDataObject.FromFloor, formDataObject.ToFloor, row.attr('data-id'))) {
                    alert('路由重複：相同移動類型 + 起點樓層 + 終點樓層 已存在');
                    return;
                }
                updatedata[formDataObject["Id"]] = formDataObject;
            }
        } else {
            icon.removeClass('fa-square-pen');
            icon.addClass('fa-square-check');
            icon.css('color', '#4CCD99');
            $(".edit-btn,.delete-btn,tfoot").css("visibility", "hidden");
            $('#maintain-save ,#maintain-cancel').toggleClass('d-none');
            row.find('.edit-btn,.delete-btn').css('visibility', 'visible');
            originformdata = form.serialize();
        }
    });

    // 刪除按鈕點擊事件
    $(document).on('click', '.delete-btn', function () {
        var form = $(this).closest('tr').next().find('form');
        var row = $(this).closest('tr');

        if (form.is(':visible')) {
            form.parent().collapse('hide');
        }
        else {
            var id = row.attr('data-id');
            if (!deletedata['ids']) {
                deletedata['ids'] = [];
            }
            deletedata['ids'].push(id);
            row.next().hide();
            row.hide();
        }
    });

    // 新增按鈕點擊事件
    $(document).on('click', '#InsertData', function () {
        var btn = $(this).closest('tr');
        if (!$(this).hasClass("collapsed")) {
            $(".edit-btn,.delete-btn").fadeOut(300, function () {
                $(this).css("visibility", "hidden").show();
            });
            $('#maintain-save ,#maintain-cancel').toggleClass('d-none');
            $("#confirm-btn,#cancel-btn").css("visibility", "visible").hide().fadeIn(300);
            btn.hide();
        }
    });

    // 新增確認按鈕點擊事件
    $('#confirm-btn').on('click', function (e) {
        var form = $('#InsertForm');
        var formData = form.serializeArray();
        var formDataObject = {};
        if (!form[0].checkValidity()) {
            alert('請填寫所有必填欄位');
            return;
        }

        $.each(formData, function (i, item) {
            formDataObject[item.name] = item.value;
        });

        // 檢查同 MoveType + FromFloor + ToFloor 是否重複
        if (isDuplicateRoute(formDataObject.MoveType, formDataObject.FromFloor, formDataObject.ToFloor, null)) {
            alert('路由重複：相同移動類型 + 起點樓層 + 終點樓層 已存在');
            return;
        }

        insertCounter++;
        var tempKey = 'new_' + insertCounter;
        CreateNewRow(formData, fieldToColumnIndex, tempKey);
        insertdata[tempKey] = formDataObject;
        form[0].reset();
        $('#insertcollapse').collapse('hide');
    });

    // 表單收起事件
    $(document).on('hide.bs.collapse', '.collapse', function () {
        var btn = $(this).closest('tr').prev().find('button');
        var icon = $(this).closest('tr').prev().find('i').first();
        if (icon.hasClass('fa-square-check')) {
            icon.removeClass('fa-square-check');
            icon.addClass('fa-square-pen');
            icon.css('color', '#40B1FF');
        }
        $("tfoot tr").show();
        $(".edit-btn,.delete-btn,tfoot").not(btn).css("visibility", "visible").hide().fadeIn(300);
        $('#maintain-save ,#maintain-cancel').toggleClass('d-none');
        $("#confirm-btn,#cancel-btn").fadeOut(300, function () {
            $(this).css("visibility", "hidden").show();
        });
    });
});

function CreateNewRow(formData, fieldToColumnIndex, tempKey) {
    var lastRow = ($('tbody tr').length / 2 + 1);

    var useFlagValue = formData.find(item => item.name === 'UseFlag').value;
    var useFlagHtml = useFlagValue === 'Y'
        ? '<span class="badge bg-success">啟用</span>'
        : '<span class="badge bg-secondary">停用</span>';

    var newRow = $(
        '<tr class="text-center" data-id="' + tempKey + '">' +
        '<td class="align-middle">' + lastRow + '</td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle">' + useFlagHtml + '</td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle">' +
        '<button data-bs-toggle="collapse" data-bs-target="#edit-' + tempKey + '" aria-expanded="false" aria-controls="' + tempKey + '" class="btn edit-btn p-0">' +
        '<i class="fa-solid fa-square-pen fa-xl" style="color: #40B1FF;"></i>' +
        '</button> ' +
        '<button class="btn delete-btn p-0" id="delete-' + tempKey + '"><i class="fa-solid fa-square-minus fa-xl" style="color: #E25E3E;"></i></button>' +
        '</td>' +
        '</tr>' +
        '<tr>' +
        '<td colspan="8" class="border-0 p-0">' +
        '<div id="edit-' + tempKey + '" class="collapse ps-3">' +
        '<form>' +
        '<input type="hidden" name="Id" value="0" />' +
        '<div class="row row-cols-3 gy-3 m-0">' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">移動類型</label>' +
        '<select class="form-select p-0 w-50" name="MoveType">' +
        '<option value="Transport">Transport</option>' +
        '<option value="EmptyMove">EmptyMove</option>' +
        '</select>' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">起點樓層</label>' +
        '<select class="form-select p-0 w-50" name="FromFloor">' +
        '<option value="1F">1F</option><option value="2F">2F</option><option value="3F">3F</option><option value="4F">4F</option>' +
        '</select>' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">終點樓層</label>' +
        '<select class="form-select p-0 w-50" name="ToFloor">' +
        '<option value="1F">1F</option><option value="2F">2F</option><option value="3F">3F</option><option value="4F">4F</option>' +
        '</select>' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">TaskType</label>' +
        '<input class="form-control p-0 w-50" name="TaskType" autocomplete="off" />' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">啟用狀態</label>' +
        '<select class="form-select p-0 w-25" name="UseFlag">' +
        '<option value="Y">啟用</option>' +
        '<option value="N">停用</option>' +
        '</select>' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">備註</label>' +
        '<input class="form-control p-0 w-50" name="Remark" autocomplete="off" />' +
        '</div>' +
        '</div>' +
        '</form>' +
        '</div>' +
        '</td>' +
        '</tr>'
    );

    // 填充表單數據
    $.each(formData, function (i, item) {
        var columnIndex = fieldToColumnIndex[item.name];
        if (columnIndex !== undefined && item.name !== 'UseFlag') {
            newRow.find('td.align-middle').eq(columnIndex).text(item.value);
        }
        newRow.find('input[name="' + item.name + '"]').val(item.value);
        newRow.find('select[name="' + item.name + '"]').val(item.value);
    });

    $('#maintain-table tbody').append(newRow);
}

function isDuplicateRoute(moveType, fromFloor, toFloor, excludeId) {
    var isDuplicate = false;
    var deletedIds = [];
    if (typeof deletedata !== 'undefined' && deletedata['ids']) {
        deletedIds = deletedata['ids'];
    }

    // 檢查表格中現有的資料列
    $('#maintain-table tbody tr[data-id]').each(function () {
        var rowId = $(this).attr('data-id');
        if (rowId === excludeId) return;
        if ($(this).is(':hidden')) return;
        if (deletedIds.indexOf(rowId) >= 0) return;

        var rowMoveType = $(this).find('td').eq(1).text().trim();
        var rowFromFloor = $(this).find('td').eq(2).text().trim();
        var rowToFloor = $(this).find('td').eq(3).text().trim();

        if (rowMoveType === moveType && rowFromFloor === fromFloor && rowToFloor === toFloor) {
            isDuplicate = true;
            return false;
        }
    });
    return isDuplicate;
}
