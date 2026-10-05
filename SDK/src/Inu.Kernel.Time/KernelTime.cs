using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Time;

/// <summary>Provides the architecture-neutral kernel monotonic clock and programmable timer API.</summary>
public static partial class KernelTime
{
    private static Boolean _initialized, _tscAvailable, _invariantTsc;
    private static KernelClockSource _source;

    /// <summary>Initializes registered time providers and the common source-neutral kernel time services.</summary>
    public static Boolean Initialize()
    {
        _initialized = false;
        _source = KernelClockSource.None;
        _tscAvailable = Native.SupportsTsc();
        _invariantTsc = _tscAvailable && Native.SupportsInvariantTsc();
        if (!KernelClockSourceServices.InitializeRegisteredSources()) return false;
        _source = KernelClockSourceServices.GetActiveSource();
        if (_source == KernelClockSource.None) return false;
        if (!KernelInterruptTimerServices.InitializeRegisteredTimer()) return false;
        if (!KernelWallClockSourceServices.InitializeRegisteredSource()) return false;
        _initialized = true;
        KernelWallClock.Initialize();
        KernelTimeoutService.Initialize();
        return true;
    }
}
