using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Time;

/// <summary>Selectable invariant-TSC monotonic clock implementation calibrated through the clock-source contract.</summary>
public static unsafe class KernelInvariantTscClockSource
{
    private const UInt64 CalibrationNanoseconds = 10000000UL;
    private static UInt64 _start, _frequency;

    /// <summary>Registers the invariant-TSC implementation with the kernel clock core.</summary>
    public static Boolean Register() => KernelClockSourceServices.Register(KernelClockSource.InvariantTsc, 200, &Initialize, &GetNanoseconds, &TryReadRaw, &GetFrequencyHz, &GetPeriodFemtoseconds);

    private static Boolean Initialize()
    {
        if (!Native.SupportsTsc() || !Native.SupportsInvariantTsc()) return false;
        if (!KernelClockSourceServices.TryGetSourceNanoseconds(KernelClockSource.Hpet, out UInt64 referenceStart)) return false;
        UInt64 tscStart = Native.ReadTimestampCounter();
        UInt64 referenceNow = referenceStart;
        for (UInt64 iteration = 0UL; iteration < 100000000UL; iteration++)
        {
            if (!KernelClockSourceServices.TryGetSourceNanoseconds(KernelClockSource.Hpet, out referenceNow)) return false;
            if (referenceNow - referenceStart >= CalibrationNanoseconds) break;
        }
        UInt64 elapsedNanoseconds = referenceNow - referenceStart;
        if (elapsedNanoseconds < CalibrationNanoseconds) return false;
        UInt64 elapsedTsc = Native.ReadTimestampCounter() - tscStart;
        _frequency = KernelTimeMath.CalibrateFrequency(elapsedTsc, elapsedNanoseconds);
        if (_frequency < 1000000UL) { _frequency = 0UL; return false; }
        _start = Native.ReadTimestampCounter();
        return true;
    }

    private static UInt64 GetNanoseconds() => KernelTimeMath.TicksToNanoseconds(Native.ReadTimestampCounter() - _start, _frequency);
    private static UInt64 GetFrequencyHz() => _frequency;
    private static UInt64 GetPeriodFemtoseconds() => 0UL;
    private static Boolean TryReadRaw(UInt64* ticks, UInt64* frequencyHz)
    {
        if (ticks == null || frequencyHz == null || _frequency == 0UL) return false;
        *ticks = Native.ReadTimestampCounter() - _start; *frequencyHz = _frequency; return true;
    }
}
