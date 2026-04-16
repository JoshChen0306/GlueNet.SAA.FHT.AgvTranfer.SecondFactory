using System;

namespace SCP.Models;

public partial class ubCancelLog
{
    public DateTime CancelTime { get; set; }

    public string? Operator { get; set; }

    public string TaskDateTime { get; set; } = null!;

    public string? ParentTaskDateTime { get; set; }

    public string TaskSource { get; set; } = null!;

    public string BeginStation { get; set; } = null!;

    public string EndStation { get; set; } = null!;

    public string? TaskCode { get; set; }

    public string? RcsCancelResult { get; set; }

    public string? LinkedTaskDateTimes { get; set; }
}
