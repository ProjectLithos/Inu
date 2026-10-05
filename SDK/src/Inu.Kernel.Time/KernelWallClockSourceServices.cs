using System;

namespace Inu.Kernel.Time;

/// <summary>Registration and dispatch boundary between wall-clock consumers and optional calendar-clock providers.</summary>
public static unsafe class KernelWallClockSourceServices
{
    private static Byte _registered, _ready;
    private static delegate*<Boolean> _initialize;
    private static delegate*<KernelRtcDateTime*,Boolean> _tryRead;

    /// <summary>Registers the single calendar source used to anchor wall time.</summary>
    public static Boolean Register(delegate*<Boolean> initialize, delegate*<KernelRtcDateTime*,Boolean> tryRead)
    {
        if (_registered != 0 || initialize == null || tryRead == null) return false;
        _initialize = initialize;
        _tryRead = tryRead;
        _registered = 1;
        _ready = 0;
        return true;
    }

    internal static Boolean InitializeRegisteredSource()
    {
        _ready = 0;
        if (_registered == 0) return true; // Wall time is optional.
        _ready = _initialize() ? (Byte)1 : (Byte)0;
        return true;
    }

    /// <summary>Gets whether a registered calendar provider initialized successfully.</summary>
    public static Boolean IsReady() => _ready != 0;

    /// <summary>Reads the current calendar sample without exposing the concrete provider.</summary>
    public static Boolean TryRead(out KernelRtcDateTime value)
    {
        value = default;
        if (_ready == 0) return false;
        KernelRtcDateTime local = default;
        if (!_tryRead(&local)) return false;
        value = local;
        return true;
    }
}
