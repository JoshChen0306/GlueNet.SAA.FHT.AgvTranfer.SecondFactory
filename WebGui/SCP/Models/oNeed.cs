using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oNeed
{
    public string ObjStation { get; set; } = null!;

    public string? RackId { get; set; }

    public string? WorkOrder { get; set; }

    public string? EndStation { get; set; }

    public string? TaskSource { get; set; }

    public string? TaskDateTime { get; set; }

    public string? AssignFlag { get; set; }

    /// <summary>空平板回收(O→Q)記錄觸發它的 M→O 物料任務 TaskDateTime，供取消時連動清源頭；一般任務為 null。</summary>
    public string? ParentTaskDateTime { get; set; }
}
