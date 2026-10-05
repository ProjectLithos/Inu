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

/// <summary>Initializes physical/virtual memory, heap and the NativeAOT runtime.</summary>
public static unsafe class MemoryRuntimeStartup
{
    public static Boolean Initialize<TBoot>(TBoot boot)
        where TBoot : IFinalMemoryMapBufferContext, IMemoryDescriptorLayoutContext, IBootstrapPageTableWorkspaceContext, IKernelImageContext
    {
        if (!KernelPhysicalMemory.Initialize(boot, boot, boot)) return false;
        KernelPhysicalMemoryStatistics physicalMemory = KernelPhysicalMemory.GetStatistics();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Physical memory managed/free/reserved: ")) return false;
        if (!KernelConsole.WriteByteSize(physicalMemory.ManagedPages * 4096UL)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteByteSize(physicalMemory.FreePages * 4096UL)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteByteSize(physicalMemory.ReservedPages * 4096UL)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("memory","BootStartup.Initialize","Physical memory manager initialized from final UEFI map.")) return false;
        if (!KernelVirtualMemory.Initialize()) return false;
        if (!KernelStructuredLogging.InfoLine("virtual-memory","BootStartup.Initialize","Virtual memory manager attached to active x64 page tables.")) return false;
        Boolean addressSpaceReady = KernelAddressSpace.Initialize();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Kernel address-space status: ")) return false;
        if (!KernelConsole.WriteLine(KernelAddressSpace.GetLastStatusName())) return false;
        if (!addressSpaceReady)
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("Virtual memory status: ")) return false;
            if (!KernelConsole.WriteLine(KernelVirtualMemory.GetLastStatusName())) return false;
            return false;
        }
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Kernel image base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.KernelImageBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Kernel heap base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.KernelHeapBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Kernel stacks base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.KernelStacksBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Direct map base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.DirectMapBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("MMIO base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.MmioBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Page-table window: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.PageTableWindowBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelEarlyAllocator.Initialize()) return false;
        if (!KernelEarlyAllocator.TryAllocate(256UL, 16UL, out UInt64 earlyAddress)) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Early allocator sample: ")) return false;
        if (!KernelConsole.WriteHex(earlyAddress)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        Boolean heapReady = KernelHeap.Initialize();
        if (heapReady)
        {
            if (!KernelTelemetry.ConfigureFreestanding(&KernelTelemetryTransport.TryEmit, &KernelTelemetryTransport.TryGetContext)) return false;
            KernelTelemetry.KernelBootEvent("Kernel heap", 8UL, KernelBootPhase.End, KernelHeap.GetLastStatusName());
            KernelTelemetry.KernelDiagnosticEvent("telemetry", "runtime-online", 0UL, "Structured kernel telemetry v1.1 online");
        }
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Kernel heap status: ")) return false;
        if (!KernelConsole.WriteLine(KernelHeap.GetLastStatusName())) return false;
        if (!heapReady) return false;

        // 0.42.0: the allocation-free boot stage ends only after KernelHeap is ready.
        if (!NativeAotRuntime.Initialize())
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Critical,"runtime","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("Managed runtime failure: ")) return false;
            if (!KernelConsole.WriteLine(NativeAotRuntime.GetLastFailureName())) return false;
            return false;
        }
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"runtime","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Managed runtime phase: ")) return false;
        if (!KernelConsole.WriteLine(NativeAotRuntime.GetPhaseName())) return false;

        // 0.0.77: bind the NativeAOT ReadyToRun header directly instead of relying on
        // COFF .modules$A/.modules$I/.modules$Z subsection retention. The 0.0.76 QEMU
        // trace proved the sentinel range contained one null slot and no ILC module entry.
        // Referencing __ReadyToRunHeader from Entry.asm also makes the linker retain the
        // exact module header and its GCStaticRegion metadata.
        UInt64 readyToRunHeader = Native.GetManagedReadyToRunHeader();
        if (readyToRunHeader == 0UL)
        {
            KernelConsole.WriteLine("NOBT:FAIL:RTR-HEADER");
            return false;
        }
        IntPtr* moduleHeaders = stackalloc IntPtr[1];
        moduleHeaders[0] = (IntPtr)(void*)(nuint)readyToRunHeader;
        KernelConsole.WriteLine("NOBT:MODULES:RUN");
        NativeAotExceptionRuntime.TraceValue(0x18CUL, 1UL);
        NativeAotExceptionRuntime.TraceValue(0x18DUL, readyToRunHeader);
                global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.InitializeModules(
            (IntPtr)(void*)(nuint)boot.GetKernelImageBase(),
            moduleHeaders,
            1,
            null,
            0);
        KernelConsole.WriteLine("NOBT:MODULES:OK");

        KernelConsole.WriteLine("NOBT:CONF:BASE");
        if (!NativeAotExceptionRuntime.ConfigureImageBase(boot.GetKernelImageBase())) { KernelConsole.WriteLine("NOBT:FAIL:EHBASE"); return false; }
        KernelConsole.WriteLine("NOBT:CONF:RUN");

        if (!ManagedRuntimeConformance.Run(out UInt32 runtimePassed, out UInt32 runtimeFailed, out UInt32 abiPassed, out UInt32 abiFailed))
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Critical,"runtime","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write(".NET conformance passed/failed: ")) return false;
            if (!KernelConsole.WriteUInt64(runtimePassed)) return false;
            if (!KernelConsole.Write("/")) return false;
            if (!KernelConsole.WriteUInt64(runtimeFailed)) return false;
            if (!KernelConsole.WriteLine("")) return false;
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Critical,"runtime","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write(".NET ABI baseline passed/failed: ")) return false;
            if (!KernelConsole.WriteUInt64(abiPassed)) return false;
            if (!KernelConsole.Write("/")) return false;
            if (!KernelConsole.WriteUInt64(abiFailed)) return false;
            if (!KernelConsole.WriteLine("")) return false;
            return false;
        }
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"runtime","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write(".NET conformance passed/failed: ")) return false;
        if (!KernelConsole.WriteUInt64(runtimePassed)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(runtimeFailed)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(abiFailed == 0U ? KernelLogLevel.Info : KernelLogLevel.Warning,"runtime","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write(".NET ABI baseline passed/failed: ")) return false;
        if (!KernelConsole.WriteUInt64(abiPassed)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(abiFailed)) return false;
        if (!KernelConsole.WriteLine("")) return false;

        return true;
    }
}
