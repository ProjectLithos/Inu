using System;

namespace Inu.Kernel.Time;

/// <summary>Provides the architecture-neutral kernel monotonic clock and programmable timer API.</summary>
public static partial class KernelTime
{
    public static Boolean IsInitialized => _initialized;
    /// <summary>Gets the selected monotonic clock source.</summary>
    public static KernelClockSource GetClockSource() => _source;
    /// <summary>Gets a printable name for the selected monotonic clock source.</summary>
    public static String GetClockSourceName() => _source == KernelClockSource.InvariantTsc ? "InvariantTSC" : _source == KernelClockSource.Hpet ? "HPET" : "None";
    /// <summary>Gets the active clock frequency in ticks per second.</summary>
    public static UInt64 GetClockFrequencyHz() => KernelClockSourceServices.GetActiveFrequencyHz();
    /// <summary>Gets the HPET frequency in hertz when the HPET provider is selected and initialized.</summary>
    public static UInt64 GetHpetFrequencyHz() => KernelClockSourceServices.GetFrequencyHz(KernelClockSource.Hpet);
    /// <summary>Gets the HPET main-counter period in femtoseconds when initialized.</summary>
    public static UInt64 GetHpetPeriodFemtoseconds() => KernelClockSourceServices.GetPeriodFemtoseconds(KernelClockSource.Hpet);
    /// <summary>Gets the calibrated invariant-TSC frequency when that provider initialized successfully.</summary>
    public static UInt64 GetTscFrequencyHz() => KernelClockSourceServices.GetFrequencyHz(KernelClockSource.InvariantTsc);
    /// <summary>Gets the calibrated Local APIC timer frequency after divide-by-16 configuration.</summary>
    public static UInt64 GetLocalApicTimerFrequencyHz() => KernelInterruptTimerServices.GetFrequencyHz();
    /// <summary>Gets a capability snapshot for scheduling and diagnostics.</summary>
    public static KernelTimeCapabilities GetCapabilities() => new(_source, GetClockFrequencyHz(), KernelClockSourceServices.IsReady(KernelClockSource.Hpet), _tscAvailable, _invariantTsc, KernelWallClockSourceServices.IsReady(), KernelInterruptTimerServices.IsReady(), KernelInterruptTimerServices.GetFrequencyHz());
}
