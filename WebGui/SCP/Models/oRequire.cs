using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oRequire
{
    public string TaskDateTime { get; set; } = null!;

    public string ObjStation { get; set; } = null!;

    public long SerialNo { get; set; }

    public string? BeginStation { get; set; }

    public string? EndStation { get; set; }

    public string? TaskSource { get; set; }

    public string? RackId { get; set; }

    public string? WorkOrder { get; set; }

    public string? AssignFlag { get; set; }

    public string? OkFlag { get; set; }

    /// <summary>由 oNeed 帶下的父任務關聯(M→O TaskDateTime)，供 SCP 取消空平板任務時反查源頭；一般任務為 null。</summary>
    public string? ParentTaskDateTime { get; set; }
}
