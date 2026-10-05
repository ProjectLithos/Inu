using System;

namespace Inu.Kernel.Time;

/// <summary>Provides the architecture-neutral kernel monotonic clock and programmable timer API.</summary>
public static partial class KernelTime
{
    /// <summary>Gets monotonic nanoseconds elapsed since timing-service initialization.</summary>
    public static UInt64 GetMonotonicNanoseconds() => _initialized ? KernelClockSourceServices.GetActiveNanoseconds() : 0UL;

    /// <summary>Reads the active high-resolution counter and its frequency without converting it to wall time.</summary>
    public static Boolean TryReadHighResolutionCounter(out UInt64 ticks, out UInt64 frequencyHz)
    {
        ticks = 0UL; frequencyHz = 0UL; return _initialized && KernelClockSourceServices.TryReadActiveRaw(out ticks, out frequencyHz);
    }

    /// <summary>Creates an absolute monotonic deadline from a relative nanosecond delay.</summary>
    public static Boolean TryCreateDeadline(UInt64 delayNanoseconds, out UInt64 deadlineNanoseconds)
    {
        deadlineNanoseconds = 0UL; if (!_initialized) return false;
        UInt64 now = GetMonotonicNanoseconds(); if (0xFFFFFFFFFFFFFFFFUL - now < delayNanoseconds) return false;
        deadlineNanoseconds = now + delayNanoseconds; return true;
    }

    /// <summary>Determines whether an absolute monotonic deadline has been reached.</summary>
    public static Boolean HasReached(UInt64 deadlineNanoseconds) => _initialized && GetMonotonicNanoseconds() >= deadlineNanoseconds;
}
