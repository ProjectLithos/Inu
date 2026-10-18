using System;
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.Console;

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
        if (KernelTimerDispatch.EnableInterruptDelivery()) return true;
        KernelConsole.WriteHostControl("TIMER_IRQ_ENABLE_FAIL");
        return false;
    }
}
