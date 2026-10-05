using System;
using Inu.Userland.Runtime;

namespace System.Threading;

/// <summary><inu.api>Timeout constants used by the freestanding threading surface.</inu.api></summary>
public static class Timeout
{
    /// <summary><inu.api>Represents an infinite wait.</inu.api></summary>
    public const Int32 Infinite = -1;
}

/// <summary>
/// <inu.api>Initial freestanding .NET-compatible thread coordination surface for ordinary ring-3 Inu applications.</inu.api>
/// This first tranche exposes cooperative yielding and sleeping for the current execution context; it does not yet
/// promise creation of additional managed user threads.
/// </summary>
public sealed class Thread
{
    private Thread() { }

    /// <summary><inu.api>Yields the remainder of the current execution opportunity to the Inu scheduler.</inu.api></summary>
    public static Boolean Yield() => UserlandThreading.Yield();

    /// <summary>
    /// <inu.api>Suspends progress for at least the requested number of milliseconds while cooperatively yielding.</inu.api>
    /// A value of zero performs one yield. <see cref="Timeout.Infinite"/> yields indefinitely.
    /// </summary>
    public static void Sleep(Int32 millisecondsTimeout)
    {
        if (millisecondsTimeout < Timeout.Infinite) throw new ArgumentOutOfRangeException();
        if (millisecondsTimeout == 0)
        {
            UserlandThreading.Yield();
            return;
        }
        if (millisecondsTimeout == Timeout.Infinite)
        {
            for (;;) UserlandThreading.Yield();
        }

        UInt64 start = UserlandThreading.MonotonicNanoseconds();
        UInt64 delay = (UInt64)(UInt32)millisecondsTimeout * 1000000UL;
        UInt64 deadline = UInt64.MaxValue - start < delay ? UInt64.MaxValue : start + delay;

        // Userland starts only after the monotonic kernel clock is available. Keep the
        // loop defensive anyway: every iteration crosses the scheduler-yield boundary,
        // so this is cooperative rather than a tight userspace spin.
        for (;;)
        {
            UInt64 now = UserlandThreading.MonotonicNanoseconds();
            if (now >= deadline) return;
            UserlandThreading.Yield();
        }
    }
}
