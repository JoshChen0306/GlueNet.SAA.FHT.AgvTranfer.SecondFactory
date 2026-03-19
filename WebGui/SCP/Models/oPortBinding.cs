using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oPortBinding
{
    public string LoadingPort { get; set; } = null!;

    public string? UnloadingPort { get; set; }

    public string? FallbackAreas { get; set; }

    public string? UseFlag { get; set; }
}
