using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.Power;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Graphics;
using Inu.Kernel.Bootstrap;

namespace Inu.Kernel.Bootstrap.Boot;

/// <summary>Registers the selected clock providers and starts kernel timekeeping.</summary>
public static unsafe class TimeStartup
{
    public static Boolean Initialize()
    {
#if INU_COMPONENT_TIME_HPET_CLOCK_SOURCE
        if (!KernelHpetClockSource.Register()) { KernelConsole.WriteLine("NOBT:FAIL:HPET-REGISTER"); return false; }
#endif
#if INU_COMPONENT_TIME_INVARIANT_TSC_CLOCK_SOURCE
        if (!KernelInvariantTscClockSource.Register()) { KernelConsole.WriteLine("NOBT:FAIL:TSC-REGISTER"); return false; }
#endif
#if INU_COMPONENT_TIME_RTC_CMOS_WALL_CLOCK
        if (!KernelRtcCmosProvider.Register()) { KernelConsole.WriteLine("NOBT:FAIL:RTC-REGISTER"); return false; }
#endif
#if INU_COMPONENT_TIME_LOCAL_APIC_INTERRUPT_TIMER
        if (!KernelLocalApicTimerProvider.Register()) { KernelConsole.WriteLine("NOBT:FAIL:LAPIC-TIMER-REGISTER"); return false; }
#endif
        if (!KernelTime.Initialize()) { KernelConsole.WriteLine("NOBT:FAIL:TIME"); return false; }
        KernelConsole.WriteLine("NOBT:TIME");
        KernelTimeCapabilities timeCapabilities = KernelTime.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("HPET: ")) return false;
        if (!KernelConsole.Write(timeCapabilities.HasHpet ? "online @ " : "unavailable")) return false;
        if (timeCapabilities.HasHpet && !KernelConsole.WriteFrequency(KernelHpet.GetFrequencyHz())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("TSC: ")) return false;
        if (!KernelConsole.Write(timeCapabilities.HasTsc ? "available" : "unavailable")) return false;
        if (timeCapabilities.HasTsc)
        {
            if (!KernelConsole.Write(timeCapabilities.HasInvariantTsc ? " / invariant" : " / non-invariant")) return false;
            if (KernelTsc.GetFrequencyHz() != 0UL)
            {
                if (!KernelConsole.Write(" @ ")) return false;
                if (!KernelConsole.WriteFrequency(KernelTsc.GetFrequencyHz())) return false;
            }
        }
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Monotonic clock source: ")) return false;
        if (!KernelConsole.Write(KernelTime.GetClockSourceName())) return false;
        if (!KernelConsole.Write(" @ ")) return false;
        if (!KernelConsole.WriteFrequency(KernelTime.GetClockFrequencyHz())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Local APIC timer: ")) return false;
        if (!KernelConsole.WriteLine(KernelLocalApicTimer.IsAvailable() ? "calibrated" : "unavailable")) return false;
        if (KernelLocalApicTimer.IsAvailable())
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("Local APIC timer frequency: ")) return false;
            if (!KernelConsole.WriteFrequency(KernelLocalApicTimer.GetFrequencyHz())) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("RTC/CMOS: ")) return false;
        if (KernelWallClockSourceServices.TryRead(out KernelRtcDateTime rtc))
        {
            if (!KernelConsole.WriteUInt64(rtc.Year)) return false;
            if (!KernelConsole.Write("-")) return false;
            if (!KernelConsole.WriteUInt64(rtc.Month)) return false;
            if (!KernelConsole.Write("-")) return false;
            if (!KernelConsole.WriteUInt64(rtc.Day)) return false;
            if (!KernelConsole.Write(" ")) return false;
            if (!KernelConsole.WriteUInt64(rtc.Hour)) return false;
            if (!KernelConsole.Write(":")) return false;
            if (!KernelConsole.WriteUInt64(rtc.Minute)) return false;
            if (!KernelConsole.Write(":")) return false;
            if (!KernelConsole.WriteUInt64(rtc.Second)) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
        else if (!KernelConsole.WriteLine("unavailable")) return false;
        if (!KernelStructuredLogging.InfoLine("time","BootStartup.Initialize","HPET, Local APIC timer, TSC, RTC/CMOS and invariant-TSC clock source online.")) return false;
        return true;
    }
}
