using System;
using Inu.Kernel.InterruptDispatch;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Enables runtime interrupt delivery after the OS-selected handlers are ready.</summary>
public static class InterruptRuntimeStartup
{
    public static Boolean Enable() => Interrupts.Run();
}
