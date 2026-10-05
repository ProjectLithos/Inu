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

/// <summary>Initializes the selected bootstrap console, diagnostics, panic transport and validates the selected final-memory-map capability.</summary>
public static unsafe class BootDiagnosticsStartup
{
    public static Boolean Initialize<TBoot>(TBoot boot)
        where TBoot : IBootFramebufferContext, IFinalMemoryMapBufferContext, IMemoryDescriptorLayoutContext
    {
        KernelConsole.WriteLine("NOBT:ENTRY");
        if (!Inu.Kernel.Console.Console.Run(ConsoleType.Auto, boot)) { KernelConsole.WriteLine("NOBT:FAIL:CONSOLE"); return false; }
        KernelConsole.WriteLine("NOBT:CONSOLE");
        if (!KernelStructuredLogging.Initialize()) { KernelConsole.WriteLine("NOBT:FAIL:LOGINIT"); return false; }
        KernelConsole.WriteLine("NOBT:LOGINIT");
        if (!KernelPanicTransport.Initialize()) { KernelConsole.WriteLine("NOBT:FAIL:PANIC"); return false; }
        KernelConsole.WriteLine("NOBT:PANIC");
        if (!KernelStructuredLogging.TraceLine("console","BootStartup.Initialize","Kernel console initialized; structured diagnostic routing begins.")) return false;
        if (!KernelStructuredLogging.InfoLine("bootstrap","BootStartup.Initialize","Inu KMain started.")) return false;
        if (!boot.HasFinalMemoryMapBuffer() && boot.HasMemoryDescriptorLayout()) { KernelConsole.WriteLine("NOBT:FAIL:MEMMAP"); return false; }
        KernelConsole.WriteLine("NOBT:MEMMAP");
        if (!KernelStructuredLogging.InfoLine("boot","BootStartup.Initialize","Final UEFI memory map retained; ExitBootServices succeeded.")) return false;
        return true;
    }
}
