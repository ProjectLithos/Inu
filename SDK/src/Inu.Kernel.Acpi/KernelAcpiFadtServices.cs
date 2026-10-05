using System;

namespace Inu.Kernel.Acpi;

/// <summary>Registration boundary for an optional FADT platform-information provider.</summary>
public static unsafe class KernelAcpiFadtServices
{
    private static Byte _registered;
    private static Byte _ready;
    private static delegate*<Boolean> _initialize;
    private static delegate*<AcpiFadtInfo*,Boolean> _getInfo;

    public static Boolean Register(delegate*<Boolean> initialize, delegate*<AcpiFadtInfo*,Boolean> getInfo)
    {
        if (_registered != 0 || initialize == null || getInfo == null) return false;
        _initialize = initialize; _getInfo = getInfo; _registered = 1; return true;
    }

    public static Boolean Initialize()
    {
        if (_ready != 0) return true;
        if (_registered == 0 || !_initialize()) return false;
        _ready = 1; return true;
    }

    public static Boolean IsAvailable() => _ready != 0;
    public static Boolean TryGetInfo(out AcpiFadtInfo info)
    {
        info = default;
        if (_ready == 0) return false;
        AcpiFadtInfo local = default;
        if (!_getInfo(&local)) return false;
        info = local; return true;
    }
}
