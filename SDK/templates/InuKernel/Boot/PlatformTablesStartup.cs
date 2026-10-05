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

/// <summary>Initializes x64 descriptor tables, interrupt descriptors and legacy PIC state.</summary>
public static unsafe class PlatformTablesStartup
{
    public static Boolean Initialize()
    {
        if (!KernelPlatform.InitializeDescriptors()) { KernelConsole.WriteLine("NOBT:FAIL:GDT"); return false; }
        KernelConsole.WriteLine("NOBT:GDT");
        if (!KernelStructuredLogging.DebugLine("architecture","BootStartup.Initialize","GDT and TSS installed.")) return false;
        if (!KernelPlatform.InitializeInterrupts()) { KernelConsole.WriteLine("NOBT:FAIL:IDT"); return false; }
        KernelConsole.WriteLine("NOBT:IDT");
        if (!KernelStructuredLogging.DebugLine("interrupts","BootStartup.Initialize","IDT with 256 vectors installed.")) return false;
        if (!KernelPlatform.DisableLegacyPic()) { KernelConsole.WriteLine("NOBT:FAIL:PIC"); return false; }
        KernelConsole.WriteLine("NOBT:PIC");
        if (!KernelStructuredLogging.InfoLine("interrupts","BootStartup.Initialize","Legacy PIC masked; APIC/MSI controller layer ready.")) return false;
        return true;
    }
}
