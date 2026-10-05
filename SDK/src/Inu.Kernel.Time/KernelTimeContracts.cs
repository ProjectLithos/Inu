using System;

namespace Inu.Kernel.Time;

/// <summary>Identifies the monotonic time source selected by the kernel.</summary>
public enum KernelClockSource : Byte
{
    /// <summary>No usable monotonic source has been initialized.</summary>
    None,
    /// <summary>The ACPI High Precision Event Timer provides monotonic time.</summary>
    Hpet,
    /// <summary>An invariant x64 timestamp counter calibrated against HPET provides monotonic time.</summary>
    InvariantTsc
}

/// <summary>Identifies how a programmable timer produces interrupts.</summary>
public enum KernelTimerMode : Byte
{
    /// <summary>The timer fires once and stops.</summary>
    OneShot,
    /// <summary>The timer reloads automatically and fires periodically.</summary>
    Periodic
}

/// <summary>Describes the initialized kernel clock and timer facilities.</summary>
public readonly struct KernelTimeCapabilities
{
    /// <summary>Creates a backward-compatible timing capability snapshot.</summary>
    public KernelTimeCapabilities(KernelClockSource source, UInt64 clockFrequencyHz, Boolean localApicTimer, UInt64 localApicTimerFrequencyHz)
        : this(source, clockFrequencyHz, source != KernelClockSource.None, source == KernelClockSource.InvariantTsc, source == KernelClockSource.InvariantTsc, false, localApicTimer, localApicTimerFrequencyHz) { }

    /// <summary>Creates a timing capability snapshot.</summary>
    public KernelTimeCapabilities(KernelClockSource source, UInt64 clockFrequencyHz, Boolean hpet, Boolean tsc, Boolean invariantTsc, Boolean rtcCmos, Boolean localApicTimer, UInt64 localApicTimerFrequencyHz)
    { Source = source; ClockFrequencyHz = clockFrequencyHz; HasHpet = hpet; HasTsc = tsc; HasInvariantTsc = invariantTsc; HasRtcCmos = rtcCmos; HasLocalApicTimer = localApicTimer; LocalApicTimerFrequencyHz = localApicTimerFrequencyHz; }
    /// <summary>Gets the active monotonic clock source.</summary>
    public KernelClockSource Source { get; }
    /// <summary>Gets the active clock frequency in ticks per second.</summary>
    public UInt64 ClockFrequencyHz { get; }
    /// <summary>Gets whether an ACPI HPET clock is initialized.</summary>
    public Boolean HasHpet { get; }
    /// <summary>Gets whether the processor advertises a timestamp counter.</summary>
    public Boolean HasTsc { get; }
    /// <summary>Gets whether CPUID advertises an invariant timestamp counter.</summary>
    public Boolean HasInvariantTsc { get; }
    /// <summary>Gets whether the RTC/CMOS calendar facility is initialized.</summary>
    public Boolean HasRtcCmos { get; }
    /// <summary>Gets whether a calibrated Local APIC interrupt timer is available.</summary>
    public Boolean HasLocalApicTimer { get; }
    /// <summary>Gets the calibrated Local APIC timer frequency after its configured divider.</summary>
    public UInt64 LocalApicTimerFrequencyHz { get; }
}

/// <summary>Defines architecture-neutral monotonic-clock behavior for kernel components.</summary>
public interface IKernelClock
{
    /// <summary>Gets monotonic nanoseconds elapsed since clock initialization.</summary>
    UInt64 GetMonotonicNanoseconds();
    /// <summary>Creates an absolute monotonic deadline from a relative delay.</summary>
    Boolean TryCreateDeadline(UInt64 delayNanoseconds, out UInt64 deadlineNanoseconds);
    /// <summary>Determines whether an absolute monotonic deadline has been reached.</summary>
    Boolean HasReached(UInt64 deadlineNanoseconds);
}

/// <summary>Defines architecture-neutral programmable timer-interrupt behavior.</summary>
public interface IKernelInterruptTimer
{
    /// <summary>Arms a one-shot timer interrupt on the supplied IDT vector.</summary>
    Boolean TryArmOneShot(Byte vector, UInt64 delayNanoseconds);
    /// <summary>Arms a periodic timer interrupt on the supplied IDT vector.</summary>
    Boolean TryArmPeriodic(Byte vector, UInt64 periodNanoseconds);
    /// <summary>Stops and masks the timer.</summary>
    Boolean Cancel();
}
