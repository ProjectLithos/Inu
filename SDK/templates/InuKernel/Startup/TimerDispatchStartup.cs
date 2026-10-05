using System;
using Inu.Kernel.TimerDispatch;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Initializes kernel timer dispatch when the OS selects timer-driven runtime services.</summary>
public static class TimerDispatchStartup
{
    public static Boolean Initialize() => KernelTimerDispatch.Initialize();
}
