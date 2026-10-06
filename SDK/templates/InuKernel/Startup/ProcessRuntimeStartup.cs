using System;
using Inu.Kernel.Processes;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Initializes kernel process bookkeeping when the OS selects process support.</summary>
public static class ProcessRuntimeStartup
{
    public static Boolean Initialize()
    {
        if (KernelProcesses.Initialize()) return true;
        KernelConsole.WriteHostControl("PROCESS_RUNTIME_STARTUP_FAIL");
        return false;
    }
}
