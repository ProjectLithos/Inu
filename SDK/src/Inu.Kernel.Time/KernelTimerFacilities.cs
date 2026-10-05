using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Time;

/// <summary>Provides the initialized ACPI HPET clock facility without exposing MMIO programming.</summary>
public static class KernelHpet
{
    /// <summary>Gets whether HPET is initialized and usable.</summary>
    public static Boolean IsAvailable() => KernelTime.GetCapabilities().HasHpet;
    /// <summary>Gets the HPET main-counter frequency in hertz.</summary>
    public static UInt64 GetFrequencyHz() => KernelTime.GetHpetFrequencyHz();
    /// <summary>Gets the HPET counter period in femtoseconds.</summary>
    public static UInt64 GetPeriodFemtoseconds() => KernelTime.GetHpetPeriodFemtoseconds();
}

/// <summary>Provides x64 timestamp-counter capability and calibrated frequency information.</summary>
public static class KernelTsc
{
    /// <summary>Gets whether the processor advertises a timestamp counter.</summary>
    public static Boolean IsAvailable() => KernelTime.GetCapabilities().HasTsc;
    /// <summary>Gets whether CPUID advertises the timestamp counter as invariant.</summary>
    public static Boolean IsInvariant() => KernelTime.GetCapabilities().HasInvariantTsc;
    /// <summary>Gets the HPET-calibrated TSC frequency when calibration succeeded.</summary>
    public static UInt64 GetFrequencyHz() => KernelTime.GetTscFrequencyHz();
    /// <summary>Reads the serialized x64 timestamp counter when supported.</summary>
    public static UInt64 Read() => IsAvailable() ? Native.ReadTimestampCounter() : 0UL;
}

/// <summary>Provides the calibrated processor-local APIC interrupt timer.</summary>
public static class KernelLocalApicTimer
{
    /// <summary>Gets whether the Local APIC timer was calibrated successfully.</summary>
    public static Boolean IsAvailable() => KernelTime.GetCapabilities().HasLocalApicTimer;
    /// <summary>Gets the Local APIC timer frequency after its configured divider.</summary>
    public static UInt64 GetFrequencyHz() => KernelTime.GetLocalApicTimerFrequencyHz();
    /// <summary>Arms one processor-local one-shot interrupt.</summary>
    public static Boolean TryArmOneShot(Byte vector, UInt64 delayNanoseconds) => KernelTime.TryArmOneShot(vector, delayNanoseconds);
    /// <summary>Arms processor-local periodic interrupts.</summary>
    public static Boolean TryArmPeriodic(Byte vector, UInt64 periodNanoseconds) => KernelTime.TryArmPeriodic(vector, periodNanoseconds);
    /// <summary>Stops and masks the processor-local timer.</summary>
    public static Boolean Cancel() => KernelTime.CancelLocalApicTimer();
}
