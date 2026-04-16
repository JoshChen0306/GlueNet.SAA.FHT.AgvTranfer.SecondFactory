using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oMission
{
    public string TaskDateTime { get; set; } = null!;

    public long SerialNo { get; set; }

    public string BeginStation { get; set; } = null!;

    public string EndStation { get; set; } = null!;

    public string TaskSource { get; set; } = null!;

    public string? TaskCode { get; set; }

    public long? ShuttleId { get; set; }

    public string? RackId { get; set; }

    public string? WorkOrder { get; set; }

    public string? Remark1 { get; set; }

    public string? Remark2 { get; set; }

    public string? Remark3 { get; set; }

    public string? OkFlag { get; set; }

    public string? BeginTime { get; set; }

    public string? EndTime { get; set; }

    public string? ParentTaskDateTime { get; set; }
}
