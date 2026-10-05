using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Time;

/// <summary>Optional x64 Local APIC programmable interrupt-timer provider.</summary>
public static unsafe class KernelLocalApicTimerProvider
{
    private const UInt64 CalibrationNanoseconds = 10000000UL;
    private const UInt64 LocalApicSpurious = 0xF0UL, LocalApicLvtTimer = 0x320UL, LocalApicInitialCount = 0x380UL, LocalApicCurrentCount = 0x390UL, LocalApicDivide = 0x3E0UL;
    private const UInt32 LocalApicMasked = 1U << 16, LocalApicPeriodic = 1U << 17, DivideBy16 = 3U;
    private static UInt64 _base, _frequency;

    public static Boolean Register() => KernelInterruptTimerServices.Register(&InitializeProvider, &Program, &CancelProvider, &GetFrequency);

    private static Boolean InitializeProvider()
    {
        _base = 0UL; _frequency = 0UL;
        if (!KernelAcpi.TryGetLocalApicAddress(out _base) || _base == 0UL) return false;
        UInt32 spurious = Native.ReadMmio32(_base + LocalApicSpurious);
        if ((spurious & 0xFFU) < 0x10U) spurious = (spurious & 0xFFFFFF00U) | 0xFFU;
        if (!Native.WriteMmio32(_base + LocalApicSpurious, spurious | 0x100U)) return false;
        if (!Native.WriteMmio32(_base + LocalApicDivide, DivideBy16)) return false;
        if (!Native.WriteMmio32(_base + LocalApicLvtTimer, LocalApicMasked)) return false;
        if (!Native.WriteMmio32(_base + LocalApicInitialCount, 0xFFFFFFFFU)) return false;
        UInt64 start = KernelClockSourceServices.GetActiveNanoseconds();
        if (!WaitActiveNanoseconds(CalibrationNanoseconds)) return false;
        UInt32 current = Native.ReadMmio32(_base + LocalApicCurrentCount);
        if (!Native.WriteMmio32(_base + LocalApicInitialCount, 0U)) return false;
        UInt64 elapsed = KernelClockSourceServices.GetActiveNanoseconds() - start;
        _frequency = KernelTimeMath.CalibrateFrequency((UInt64)(0xFFFFFFFFU-current), elapsed);
        return _frequency != 0UL;
    }

    private static Boolean Program(Byte vector, UInt64 nanoseconds, KernelTimerMode mode)
    {
        if (_base == 0UL || vector < (Byte)32U || nanoseconds == 0UL) return false;
        UInt64 ticks = KernelTimeMath.NanosecondsToTicksCeiling(nanoseconds, _frequency);
        if (ticks == 0UL || ticks > 0xFFFFFFFFU) return false;
        UInt32 lvt = vector; if (mode == KernelTimerMode.Periodic) lvt |= LocalApicPeriodic;
        return Native.WriteMmio32(_base+LocalApicDivide,DivideBy16) && Native.WriteMmio32(_base+LocalApicLvtTimer,lvt) && Native.WriteMmio32(_base+LocalApicInitialCount,(UInt32)ticks);
    }

    private static Boolean CancelProvider() => _base != 0UL && Native.WriteMmio32(_base+LocalApicInitialCount,0U) && Native.WriteMmio32(_base+LocalApicLvtTimer,LocalApicMasked);
    private static UInt64 GetFrequency() => _frequency;

    private static Boolean WaitActiveNanoseconds(UInt64 delay)
    {
        UInt64 start=KernelClockSourceServices.GetActiveNanoseconds();
        while(KernelClockSourceServices.GetActiveNanoseconds()-start<delay) Native.Pause();
        return true;
    }
}

public static partial class KernelTime
{
    public static Boolean TryArmOneShot(Byte vector, UInt64 delayNanoseconds) => KernelInterruptTimerServices.TryProgram(vector,delayNanoseconds,KernelTimerMode.OneShot);
    public static Boolean TryArmPeriodic(Byte vector, UInt64 periodNanoseconds) => KernelInterruptTimerServices.TryProgram(vector,periodNanoseconds,KernelTimerMode.Periodic);
    public static Boolean CancelLocalApicTimer() => KernelInterruptTimerServices.Cancel();
}
