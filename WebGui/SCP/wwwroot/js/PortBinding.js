$(function () {
    var insertdata = {};
    var updatedata = {};
    var deletedata = {};
    var originformdata = {};
    var originalData = null;
    var fieldToColumnIndex = {
        'LoadingPort': 1,
        'UnloadingPort': 2,
        'FallbackAreas': 3,
        'UseFlag': 4,
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
                url: '/PortBinding/DataChange',
                data: JSON.stringify({
                    insertdata: insertdata,
                    updatedata: updatedata,
                    deletedata: deletedata.loadingPort,
                }),
                contentType: 'application/json',
                success: function (response) {
                    insertdata = {};
                    updatedata = {};
                    deletedata = {};
                },
                error: function (jqXHR, textStatus, errorThrown) {
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
                updatedata[formDataObject["LoadingPort"]] = formDataObject;
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
            var loadingPort = row.find('td:eq(1)').text();
            if (!deletedata['loadingPort']) {
                deletedata['loadingPort'] = [];
            }
            deletedata['loadingPort'].push(loadingPort);
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
            alert('請選擇上料區及下料區站點');
            return;
        }

        $.each(formData, function (i, item) {
            formDataObject[item.name] = item.value;
        });

        // 檢查上料區站點是否重複
        var newLoadingPort = formDataObject["LoadingPort"];
        var isDuplicate = false;
        $('#maintain-table tbody tr').each(function () {
            if ($(this).find('td').length <= 1) return;
            var existingId = $(this).find('td').eq(1).text().trim();
            if (existingId === newLoadingPort) {
                isDuplicate = true;
                return false;
            }
        });

        if (isDuplicate) {
            alert('上料區站點重複，請使用其他站點');
            return;
        }

        CreateNewRow(formData, fieldToColumnIndex);
        insertdata[formDataObject["LoadingPort"]] = formDataObject;
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

function CreateNewRow(formData, fieldToColumnIndex) {
    var loadingPort = formData.find(item => item.name === 'LoadingPort').value;
    var lastRow = ($('tbody tr').length / 2 + 1);

    var useFlagValue = formData.find(item => item.name === 'UseFlag').value;
    var useFlagHtml = useFlagValue === 'Y'
        ? '<span class="badge bg-success">啟用</span>'
        : '<span class="badge bg-secondary">停用</span>';

    var newRow = $(
        '<tr class="text-center">' +
        '<td class="align-middle">' + lastRow + '</td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle"></td>' +
        '<td class="align-middle">' + useFlagHtml + '</td>' +
        '<td class="align-middle">' +
        '<button data-bs-toggle="collapse" data-bs-target="#edit-' + loadingPort + '" aria-expanded="false" aria-controls="' + loadingPort + '" class="btn edit-btn p-0">' +
        '<i class="fa-solid fa-square-pen fa-xl" style="color: #40B1FF;"></i>' +
        '</button> ' +
        '<button class="btn delete-btn p-0" id="delete-' + loadingPort + '"><i class="fa-solid fa-square-minus fa-xl" style="color: #E25E3E;"></i></button>' +
        '</td>' +
        '</tr>' +
        '<tr>' +
        '<td colspan="6" class="border-0 p-0">' +
        '<div id="edit-' + loadingPort + '" class="collapse ps-3">' +
        '<form>' +
        '<div class="row row-cols-2 gy-3 m-0">' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">上料區站點</label>' +
        '<input class="form-control-plaintext w-25" name="LoadingPort" readonly />' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">下料區站點</label>' +
        '<input class="form-control p-0 w-50" name="UnloadingPort" autocomplete="off" />' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">回收候補區域</label>' +
        '<input class="form-control p-0 w-50" name="FallbackAreas" autocomplete="off" />' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">啟用狀態</label>' +
        '<select class="form-select p-0 w-25" name="UseFlag">' +
        '<option value="Y">啟用</option>' +
        '<option value="N">停用</option>' +
        '</select>' +
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
