using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oTaskTypeRoute
{
    public int Id { get; set; }

    public string MoveType { get; set; } = null!;

    public string FromFloor { get; set; } = null!;

    public string ToFloor { get; set; } = null!;

    public string TaskType { get; set; } = null!;

    public string? UseFlag { get; set; }

    public string? Remark { get; set; }
}
