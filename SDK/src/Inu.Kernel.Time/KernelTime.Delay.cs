using System;

namespace Inu.Kernel.Time;

/// <summary>Provides the architecture-neutral kernel monotonic clock and programmable timer API.</summary>
public static partial class KernelTime
{
    /// <summary>Performs a bounded monotonic busy delay useful only during early bootstrap.</summary>
    public static Boolean DelayNanoseconds(UInt64 nanoseconds)
    {
        if (!TryCreateDeadline(nanoseconds, out UInt64 deadline)) return false;
        while (!HasReached(deadline)) { }
        return true;
    }

    internal static Boolean WaitActiveNanoseconds(UInt64 nanoseconds)
    {
        UInt64 start = KernelClockSourceServices.GetActiveNanoseconds();
        for (UInt64 iteration = 0UL; iteration < 100000000UL; iteration++)
        {
            UInt64 now = KernelClockSourceServices.GetActiveNanoseconds();
            if (now - start >= nanoseconds) return true;
        }
        return false;
    }
}
