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

/// <summary>Initializes SMP and applies the OS-selected CPU roles.</summary>
public static unsafe class SmpStartup
{
    public static Boolean Initialize<TBoot>(TBoot boot)
        where TBoot : IApplicationProcessorTrampolineContext
    {
        if (!KernelSmp.Initialize(boot)) return false;
        // Apply the IDE-authoritative CPU-role masks after processor discovery and before scheduler workers are created.
        if (!Inu.Kernel.Bootstrap.GeneratedConfiguration.ApplyCpuRoles()) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("SMP status: ")) return false;
        if (!KernelConsole.WriteLine(KernelSmp.GetLastStatusName())) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Processors online/total: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelSmp.GetOnlineProcessorCount())) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(KernelSmp.GetProcessorCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Bootstrap processor index: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelSmp.GetBootstrapProcessorIndex())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("AP trampoline: ")) return false;
        if (!KernelConsole.WriteHex(KernelSmp.GetCapabilities().TrampolineAddress)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("smp","BootStartup.Initialize","SMP and per-CPU state online.")) return false;
        return true;
    }
}
