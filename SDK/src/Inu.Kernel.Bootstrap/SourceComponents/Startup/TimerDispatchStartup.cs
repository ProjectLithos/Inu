using System;
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.Console;
using Inu.Kernel.Time;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Initializes kernel timer dispatch when the OS selects timer-driven runtime services.</summary>
public static class TimerDispatchStartup
{
    public static Boolean Initialize()
    {
        if (KernelTimerDispatch.Initialize()) return true;
        KernelConsole.WriteHostControl("TIMER_DISPATCH_STARTUP_FAIL");
        return false;
    }

    /// <summary>Arms the periodic Local APIC timer after STI and device interrupt callbacks are ready.</summary>
    public static Boolean EnableHardwareInterrupts()
    {
        Native.WritePort8(0xE9, (Byte)'M'); Native.WritePort8(0xE9, 0x40);
        if (!KernelConsole.Write("Timer IRQ vector/frequency Hz: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelTimerDispatch.GetVector())) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(KernelTime.GetLocalApicTimerFrequencyHz())) return false;
        if (!KernelConsole.WriteLine(String.Empty)) return false;
        if (KernelTimerDispatch.EnableInterruptDelivery())
        {
            Native.WritePort8(0xE9, (Byte)'M'); Native.WritePort8(0xE9, 0x41);
            return true;
        }
        Native.WritePort8(0xE9, (Byte)'M'); Native.WritePort8(0xE9, 0x4F);
        KernelConsole.WriteHostControl("TIMER_IRQ_ENABLE_FAIL");
        return false;
    }
}
