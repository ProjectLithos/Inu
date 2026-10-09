using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.InterruptBroker;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Ps2;
using Inu.Kernel.Processes;
using Inu.Kernel.Drivers;
using Inu.Kernel.Storage;
#if INU_COMPONENT_FILESYSTEM_FATFS
using Inu.Filesystem.FatFs;
#endif
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
#if INU_COMPONENT_STORAGE_NVME_DRIVER
using Inu.Kernel.Nvme;
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
using Inu.Kernel.Ahci;
#endif
using Inu.Kernel.Virtio;
using Inu.Kernel.Virtio.Gpu;
using Inu.Kernel.Graphics;
using Inu.Kernel.Gui;
using Inu.Kernel.Audio;
#if INU_COMPONENT_NETWORK_E1000_DRIVER
using Inu.Kernel.E1000;
#endif
#if INU_COMPONENT_NETWORK_RTL8168_DRIVER
using Inu.Kernel.Rtl8168;
#endif
using Inu.Bus.Usb;
using Inu.Usb.Xhci;
using Inu.Usb.Hid;
#if INU_COMPONENT_USB_MASS_STORAGE
using Inu.Usb.MassStorage;
#endif
using Inu.Usb.Hub;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Bootstrap;

/// <summary>Defines the authoritative freestanding Inu bootstrap kernel.</summary>
public static unsafe partial class Kernel
{
    private const UInt64 KeyboardRepeatInitialDelayNanoseconds=300000000UL;
    private const UInt64 KeyboardRepeatIntervalNanoseconds=40000000UL;
    private static Boolean _ps2RepeatActive,_usbRepeatActive;
    private static KernelGraphicsDisplayHandle _consoleGraphicsDisplay;
    private static Ps2Key _ps2RepeatKey;
    private static Char _ps2RepeatCharacter;
    private static UInt64 _ps2RepeatDeadline;
    private static Byte _usbRepeatUsage;
    private static Char _usbRepeatCharacter;
    private static UInt64 _usbRepeatDeadline;
#if INU_COMPONENT_FILESYSTEM_FATFS
    private static Boolean TryMountSelectedFatRoot()
    {
        if(KernelVfs.MountCount!=0U)return true;
        KernelStorageCapabilities capabilities=KernelStorage.GetCapabilities();
        for(UInt32 ordinal=0U;ordinal<capabilities.Volumes;ordinal++)
        {
            if(!KernelStorage.TryGetVolumeByOrdinal(ordinal,out KernelStorageVolumeHandle volume))continue;
            if(KernelVfs.Mount(KernelVfs.DefaultNamespace,volume,KernelFileSystemType.Fat32,"/",out _))return true;
            if(KernelVfs.Mount(KernelVfs.DefaultNamespace,volume,KernelFileSystemType.Fat16,"/",out _))return true;
            if(KernelVfs.Mount(KernelVfs.DefaultNamespace,volume,KernelFileSystemType.Fat12,"/",out _))return true;
        }
        // The boot asset catalogue still permits the shell to run without a disk. A selected
        // filesystem with no compatible volume is therefore non-fatal, but VFS-backed commands
        // will correctly report that no mounted root exists.
        return true;
    }
#endif

    /// <summary>Initializes the kernel platform and enters the interrupt-driven interactive console.</summary>
    public static Boolean KMain<TBoot>(TBoot boot)
        where TBoot : IBootContext, IFinalMemoryMapBufferContext, IMemoryDescriptorLayoutContext, IBootstrapPageTableWorkspaceContext, IAcpiRootPointerContext, IApplicationProcessorTrampolineContext, ISystemAssetBundleContext, IKernelImageContext, IBootFramebufferContext
    {
        EmitKMainStageBody();
        return KMainBody(boot);
    }

    // Keep the exported KMain frame deliberately tiny. NativeAOT may reserve the full
    // frame before executing the first C# statement, so the large bootstrap body lives
    // behind this wrapper and the BSP bootstrap stack has independent headroom.
    private static Boolean KMainBody<TBoot>(TBoot boot)
        where TBoot : IBootContext, IFinalMemoryMapBufferContext, IMemoryDescriptorLayoutContext, IBootstrapPageTableWorkspaceContext, IAcpiRootPointerContext, IApplicationProcessorTrampolineContext, ISystemAssetBundleContext, IKernelImageContext, IBootFramebufferContext
    {
        EmitKMainStageConsole();
        // Bootstrap serial is authoritative until the managed CoreLib string ABI is proven.
        if (!global::Inu.Kernel.Console.Console.Run(ConsoleType.Serial)) { EmitKMainFailureConsole(); return false; }
        EmitKMainStageString();
        String probe = "NO";
        if (probe.Length != 2 || probe[0] != 'N' || probe[1] != 'O') { EmitKMainFailureString(); return false; }
        EmitKMainStageLogging();
        if (!KernelStructuredLogging.Initialize()) { EmitKMainFailureLogging(); return false; }
        if (!KernelPanicTransport.Initialize()) { EmitKMainFailurePanic(); return false; }
        EmitKMainStageBoot();
        if (!KernelStructuredLogging.TraceLine("console","Kernel.KMain","Kernel console initialized; structured diagnostic routing begins.")) return false;
        if (!KernelStructuredLogging.InfoLine("bootstrap","Kernel.KMain","Inu KMain started.")) return false;
        if (!boot.HasFinalMemoryMapBuffer() && boot.HasMemoryDescriptorLayout()) { KernelStructuredLogging.CriticalLine("boot","Kernel.KMain","Final UEFI memory-map capability is invalid; kernel startup cannot continue."); return false; }
        if (!KernelStructuredLogging.InfoLine("boot","Kernel.KMain","Final UEFI memory-map capability accepted.")) return false;
        if (!KernelPlatform.InitializeDescriptors()) return false;
        if (!KernelStructuredLogging.DebugLine("architecture","Kernel.KMain","GDT and TSS installed.")) return false;
        if (!KernelPlatform.InitializeInterrupts()) return false;
        if (!KernelStructuredLogging.DebugLine("interrupts","Kernel.KMain","IDT with 256 vectors installed.")) return false;
        if (!KernelPlatform.DisableLegacyPic()) return false;
        if (!KernelStructuredLogging.InfoLine("interrupts","Kernel.KMain","Legacy PIC masked; APIC/MSI controller layer ready.")) return false;
        if (!KernelAcpi.Initialize(boot)) return false;
#if INU_COMPONENT_ACPI_MADT
        if (!KernelAcpiMadtProvider.Register()) return false;
#endif
#if INU_COMPONENT_ACPI_MCFG
        if (!KernelAcpiMcfgProvider.Register()) return false;
#endif
#if INU_COMPONENT_ACPI_HPET_TABLE
        if (!KernelAcpiHpetProvider.Register()) return false;
#endif
#if INU_COMPONENT_ACPI_FADT
        if (!KernelAcpiFadtProvider.Register()) return false;
#endif
#if INU_COMPONENT_ACPI_POWER
        if (!KernelAcpiPowerProvider.Register()) return false;
#endif
#if INU_COMPONENT_ACPI_EMBEDDED_CONTROLLER
        if (!KernelAcpiEcProvider.Register()) return false;
#endif
        if (!KernelAcpiNmi.InitializeBootstrapProcessor()) return false;
        AcpiNmiDiagnostics nmiDiagnostics = KernelAcpiNmi.GetDiagnostics();
        if (!KernelConsole.Write("MADT NMI sources/local/applied: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetNmiSourceCount())) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetLocalNmiCount())) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpiNmi.GetAppliedLocalNmiCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Firmware NMI LVT LINT0/LINT1/thermal/perf: ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialLint0)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialLint1)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialThermal)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialPerformance)) return false;
        if (!KernelConsole.WriteLine(nmiDiagnostics.FirmwareNmiArmed ? " (firmware NMI route was armed; Inu replaced it)" : " (no unmasked firmware NMI LVT)")) return false;
        if (!KernelConsole.Write("NMI observations / legacy port61 / gate: ")) return false;
        if (!KernelConsole.WriteUInt64(nmiDiagnostics.Count)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialPort61)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(KernelAcpiNmi.IsLegacyNmiGateUnmasked() ? "unmasked" : "quarantined")) return false;
        if (!KernelConsole.WriteLine(nmiDiagnostics.LegacyParityOrChannelCheckAsserted ? " (parity/channel-check asserted)" : "")) return false;
        if (!KernelConsole.Write("ACPI status: ")) return false;
        if (!KernelConsole.WriteLine(KernelAcpi.GetLastStatusName())) return false;
        if (!KernelConsole.Write("ACPI RSDP: ")) return false;
        if (!KernelConsole.WriteHex(KernelAcpi.GetRootPointerAddress())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("ACPI root table: ")) return false;
        if (!KernelConsole.WriteHex(KernelAcpi.GetRootTableAddress())) return false;
        if (!KernelConsole.WriteLine(KernelAcpi.UsesXsdt() ? " XSDT" : " RSDT")) return false;
        if (!KernelConsole.Write("ACPI processors: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetProcessorCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("ACPI I/O APICs: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetIoApicCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (KernelAcpi.TryGetLocalApicAddress(out UInt64 localApicAddress))
        {
            if (!KernelConsole.Write("Local APIC base: ")) return false;
            if (!KernelConsole.WriteHex(localApicAddress)) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
        if (KernelAcpi.TryGetPciEcam(0U, out AcpiPciEcamInfo ecam))
        {
            if (!KernelConsole.Write("PCI ECAM base: ")) return false;
            if (!KernelConsole.WriteHex(ecam.BaseAddress)) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
        if (KernelAcpi.TryGetHpet(out AcpiHpetInfo hpet))
        {
            if (!KernelConsole.Write("HPET base: ")) return false;
            if (!KernelConsole.WriteHex(hpet.BaseAddress)) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
#if INU_COMPONENT_ACPI_FADT
        if (!KernelAcpiFadtServices.Initialize()) return false;
#endif
#if INU_COMPONENT_ACPI_POWER
        if (!KernelAcpiPowerServices.Initialize()) return false;
#endif
        Boolean ecReady = false;
#if INU_COMPONENT_ACPI_EMBEDDED_CONTROLLER
        ecReady = KernelAcpiEcServices.Initialize();
#endif
#if INU_COMPONENT_ACPI_EMBEDDED_CONTROLLER
        if (!ecReady) KernelStructuredLogging.WarningLine("acpi","Kernel.KMain","Selected ACPI embedded-controller provider found no usable ECDT controller; continuing without EC services.");
#endif
        AcpiPowerCapabilities power = KernelAcpiPowerServices.GetCapabilities();
        if (!KernelConsole.Write("ACPI FADT power reset/shutdown/button: ")) return false;
        if (!KernelConsole.Write(power.ResetAvailable ? "yes" : "no")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(power.ShutdownAvailable ? "yes" : "no")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteLine(power.PowerButtonAvailable ? "yes" : "no")) return false;
        if (!KernelConsole.Write("ACPI embedded controller: ")) return false;
        if (!KernelConsole.WriteLine(ecReady ? "ECDT online" : "not advertised by ECDT")) return false;
        if (!KernelStructuredLogging.InfoLine("acpi","Kernel.KMain","Selected ACPI platform services initialized.")) return false;
#if INU_COMPONENT_TIME_HPET_CLOCK_SOURCE
        if (!KernelHpetClockSource.Register()) return false;
#endif
#if INU_COMPONENT_TIME_INVARIANT_TSC_CLOCK_SOURCE
        if (!KernelInvariantTscClockSource.Register()) return false;
#endif
#if INU_COMPONENT_TIME_RTC_CMOS_WALL_CLOCK
        if (!KernelRtcCmosProvider.Register()) return false;
#endif
#if INU_COMPONENT_TIME_LOCAL_APIC_INTERRUPT_TIMER
        if (!KernelLocalApicTimerProvider.Register()) return false;
#endif
        if (!KernelTime.Initialize()) return false;
        KernelTimeCapabilities timeCapabilities = KernelTime.GetCapabilities();
        if (!KernelConsole.Write("HPET: ")) return false;
        if (!KernelConsole.Write(timeCapabilities.HasHpet ? "online @ " : "unavailable")) return false;
        if (timeCapabilities.HasHpet && !KernelConsole.WriteFrequency(KernelHpet.GetFrequencyHz())) return false;
        if (!KernelConsole.WriteLine("")) return false;
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
        if (!KernelConsole.Write("Monotonic clock source: ")) return false;
        if (!KernelConsole.Write(KernelTime.GetClockSourceName())) return false;
        if (!KernelConsole.Write(" @ ")) return false;
        if (!KernelConsole.WriteFrequency(KernelTime.GetClockFrequencyHz())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Local APIC timer: ")) return false;
        if (!KernelConsole.WriteLine(KernelLocalApicTimer.IsAvailable() ? "calibrated" : "unavailable")) return false;
        if (KernelLocalApicTimer.IsAvailable())
        {
            if (!KernelConsole.Write("Local APIC timer frequency: ")) return false;
            if (!KernelConsole.WriteFrequency(KernelLocalApicTimer.GetFrequencyHz())) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
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
        if (!KernelStructuredLogging.InfoLine("time","Kernel.KMain","HPET, Local APIC timer, TSC, RTC/CMOS and invariant-TSC clock source online.")) return false;
        if (!KernelPhysicalMemory.Initialize(boot, boot)) return false;
        KernelPhysicalMemoryStatistics physicalMemory = KernelPhysicalMemory.GetStatistics();
        if (!KernelConsole.Write("Physical memory managed/free/reserved: ")) return false;
        if (!KernelConsole.WriteByteSize(physicalMemory.ManagedPages * 4096UL)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteByteSize(physicalMemory.FreePages * 4096UL)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteByteSize(physicalMemory.ReservedPages * 4096UL)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("memory","Kernel.KMain","Physical memory manager initialized from final UEFI map.")) return false;
        if (!KernelVirtualMemory.Initialize()) return false;
        if (!KernelStructuredLogging.InfoLine("virtual-memory","Kernel.KMain","Virtual memory manager attached to active x64 page tables.")) return false;
        Boolean addressSpaceReady = KernelAddressSpace.Initialize();
        if (!KernelConsole.Write("Kernel address-space status: ")) return false;
        if (!KernelConsole.WriteLine(KernelAddressSpace.GetLastStatusName())) return false;
        if (!addressSpaceReady)
        {
            KernelStructuredLogging.ErrorLine("virtual-memory","Kernel.KMain","Kernel address-space initialization failed.");
            if (!KernelConsole.Write("Virtual memory status: ")) return false;
            if (!KernelConsole.WriteLine(KernelVirtualMemory.GetLastStatusName())) return false;
            return false;
        }
        if (!KernelConsole.Write("Kernel image base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.KernelImageBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Kernel heap base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.KernelHeapBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Kernel stacks base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.KernelStacksBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Direct map base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.DirectMapBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("MMIO base: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.MmioBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Page-table window: ")) return false;
        if (!KernelConsole.WriteHex(KernelAddressSpace.PageTableWindowBase)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelEarlyAllocator.Initialize()) return false;
        if (!KernelEarlyAllocator.TryAllocate(256UL, 16UL, out UInt64 earlyAddress)) return false;
        if (!KernelConsole.Write("Early allocator sample: ")) return false;
        if (!KernelConsole.WriteHex(earlyAddress)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        Boolean heapReady = KernelHeap.Initialize();
        if (heapReady)
        {
            if (!KernelTelemetry.ConfigureFreestanding(&KernelTelemetryTransport.TryEmit, &KernelTelemetryTransport.TryGetContext)) return false;
            KernelTelemetry.KernelBootEvent("Kernel heap", 8UL, KernelBootPhase.End, KernelHeap.GetLastStatusName());
            KernelTelemetry.KernelDiagnosticEvent("telemetry", "runtime-online", 0UL, "Structured kernel telemetry v1.1 online");
            if (!KernelStructuredLogging.InfoLine("logging","Kernel.KMain","Structured kernel logging is online with CPU/thread/process/time/source context.")) return false;
        }
        if (!KernelConsole.Write("Kernel heap status: ")) return false;
        if (!KernelConsole.WriteLine(KernelHeap.GetLastStatusName())) return false;
        if (!heapReady) { KernelStructuredLogging.CriticalLine("heap","Kernel.KMain","Kernel heap initialization failed; dynamic kernel services cannot start."); return false; }

        // 0.42.0: NoGcBootstrap ends here. From this point forward ILC's managed
        // allocation/reference helpers are backed by Inu.Runtime.NativeAot.
        if (!NativeAotRuntime.Initialize())
        {
            if (!KernelConsole.Write("Managed runtime failure: ")) return false;
            if (!KernelConsole.WriteLine(NativeAotRuntime.GetLastFailureName())) return false;
            KernelStructuredLogging.CriticalLine("runtime","Kernel.KMain","Inu.Runtime.NativeAot failed to leave NoGcBootstrap.");
            return false;
        }
        if (!KernelConsole.Write("Managed runtime phase: ")) return false;
        if (!KernelConsole.WriteLine(NativeAotRuntime.GetPhaseName())) return false;

        // NativeAOT nativelib output expects the host to initialize the merged
        // .modules table before managed statics are relied upon. Inu is its own
        // freestanding host, so perform the Runtime.Base bootstrap explicitly
        // after the GC allocator is online and before conformance/class use.
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

        // Visible console readiness is a boot prerequisite, not a post-conformance reward.
        // Initialize graphics/GOP/TrueType now so every subsequent runtime gate has visible output.
        if (!KernelGraphics.Initialize()) return false;
        Boolean firmwareFramebufferAvailable = boot.GetFramebufferAddress() != 0UL && boot.GetFramebufferSize() != 0UL && boot.GetFramebufferWidth() != 0U && boot.GetFramebufferHeight() != 0U;
#if INU_COMPONENT_GRAPHICS_FIRMWARE_FRAMEBUFFER
        if (firmwareFramebufferAvailable)
        {
            if (!FirmwareFramebuffer.Register(boot.GetFramebufferAddress(), boot.GetFramebufferSize(), boot.GetFramebufferWidth(), boot.GetFramebufferHeight(), boot.GetFramebufferPitchInPixels(), boot.GetFramebufferPixelFormat(), out KernelGraphicsDisplayHandle firmwareDisplay)) return false;
            if (!KernelConsole.Write("Generic framebuffer registered: ")) return false;
            if (!KernelConsole.WriteUInt64(firmwareDisplay.Value)) return false;
            if (!KernelConsole.Write(" @ ")) return false;
            if (!KernelConsole.WriteUInt64(boot.GetFramebufferWidth())) return false;
            if (!KernelConsole.Write("x")) return false;
            if (!KernelConsole.WriteUInt64(boot.GetFramebufferHeight())) return false;
            if (!KernelConsole.WriteLine(" (UEFI GOP generic framebuffer target).")) return false;
            // Bring the visible framebuffer console online before managed conformance.
            // Long-running runtime gates must never leave the user with a blank display.
            // Attach GOP to KernelConsole before querying the framebuffer byte count or
            // allocating software back buffers. Without this transition FrameByteCount remains 0.
            if (!KernelConsole.TryInitializeFramebuffer(framebufferBoot))
            {
                KernelConsole.WriteLine("NOBT:FAIL:FRAMEBUFFER-CONSOLE");
                return false;
            }
            KernelConsole.WriteLine("NOBT:FRAMEBUFFER-CONSOLE");
            UInt64 framebufferBufferBytes = KernelConsole.GetFramebufferBufferByteCount();
            if (framebufferBufferBytes == 0UL) return false;
            if (!KernelHeap.TryAllocate(framebufferBufferBytes, 4096UL, true, out KernelHeapAllocation framebufferBackBufferA)) return false;
            if (!KernelHeap.TryAllocate(framebufferBufferBytes, 4096UL, true, out KernelHeapAllocation framebufferBackBufferB)) return false;
            if (!KernelConsole.ConfigureFramebufferBuffers(framebufferBackBufferA.Address, framebufferBackBufferB.Address, framebufferBufferBytes)) return false;
            // The TrueType face is loaded from the boot asset bundle. Open the
            // catalogue before font lookup; userland service registration happens later.
            if (!SystemAssetCatalog.Initialize(boot))
            {
                KernelConsole.WriteLine("NOBT:FAIL:ASSETS");
                KernelStructuredLogging.ErrorLine("console","Kernel.KMain","The boot system asset bundle is missing or invalid; the TrueType console font cannot be loaded.");
                return false;
            }
            if (!Inu.Kernel.Console.Console.TryPromoteGraphics())
            {
                KernelConsole.WriteLine("NOBT:FAIL:TTF");
                KernelConsole.Write("TrueType activation state/font bytes: ");
                KernelConsole.WriteUInt64((UInt32)KernelConsole.GetTrueTypeConsoleState());
                KernelConsole.Write(" / ");
                KernelConsole.WriteUInt64(KernelConsole.GetTrueTypeFontLength());
                KernelConsole.WriteLine("");
                KernelStructuredLogging.ErrorLine("console","Kernel.KMain","Graphics console TrueType promotion failed; interactive graphics requires a valid TrueType face.");
                return false;
            }
            if (!KernelConsole.WriteLine("NOBT:TTF:OK")) return false;
            if (!KernelConsole.WriteHostControl("TTF_READY")) return false;
            FramebufferBufferCapabilities framebufferBuffers = KernelConsole.GetFramebufferBufferCapabilities();
            if (!KernelConsole.Write("Framebuffer buffers available/active: ")) return false;
            if (!KernelConsole.WriteUInt64(framebufferBuffers.AvailableBufferCount)) return false;
            if (!KernelConsole.Write(" / ")) return false;
            if (!KernelConsole.WriteUInt64((UInt32)framebufferBuffers.Mode)) return false;
            if (!KernelConsole.WriteLine(" (Auto policy selects double buffering for text; GOP scan-out is the front buffer).")) return false;
        }
        else
        {
            if (!KernelConsole.WriteLine("UEFI GOP unavailable; continuing with serial console until a graphics driver publishes a display.")) return false;
            KernelStructuredLogging.WarningLine("graphics","Kernel.KMain","UEFI GOP unavailable; graphics is optional and boot continues on serial.");
        }
#endif

        KernelConsole.WriteLine("NOBT:CONF:BASE");
        if (!NativeAotExceptionRuntime.ConfigureImageBase(boot.GetKernelImageBase())) { KernelConsole.WriteLine("NOBT:FAIL:EHBASE"); return false; }
        KernelConsole.WriteLine("NOBT:CONF:RUN");

        if (!ManagedRuntimeConformance.Run(out UInt32 runtimePassed, out UInt32 runtimeFailed, out UInt32 abiPassed, out UInt32 abiFailed))
        {
            if (!KernelConsole.Write(".NET conformance passed/failed: ")) return false;
            if (!KernelConsole.WriteUInt64(runtimePassed)) return false;
            if (!KernelConsole.Write("/")) return false;
            if (!KernelConsole.WriteUInt64(runtimeFailed)) return false;
            if (!KernelConsole.WriteLine("")) return false;
            if (!KernelConsole.Write(".NET ABI baseline passed/failed: ")) return false;
            if (!KernelConsole.WriteUInt64(abiPassed)) return false;
            if (!KernelConsole.Write("/")) return false;
            if (!KernelConsole.WriteUInt64(abiFailed)) return false;
            if (!KernelConsole.WriteLine("")) return false;
            KernelStructuredLogging.CriticalLine("runtime","Kernel.KMain",runtimeFailed!=0U?"Managed runtime semantic conformance rejected startup.":"System.Object NativeAOT ABI gate rejected startup.");
            return false;
        }
        if (!KernelConsole.Write(".NET conformance passed/failed: ")) return false;
        if (!KernelConsole.WriteUInt64(runtimePassed)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(runtimeFailed)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write(".NET ABI baseline passed/failed: ")) return false;
        if (!KernelConsole.WriteUInt64(abiPassed)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(abiFailed)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (abiFailed != 0U && !KernelStructuredLogging.WarningLine("runtime","Kernel.KMain","CoreLib ABI baseline contains known pre-reconciliation divergences; boot continues until the corresponding ABI audit items are implemented.")) return false;
        if (!KernelStructuredLogging.InfoLine("runtime","Kernel.KMain","Inu.Runtime.NativeAot managed phase and in-kernel conformance are online.")) return false;

        if (!Inu.Kernel.Audio.Audio.Initialize()) return false;
        if (!Inu.Kernel.Audio.Audio.Run()) return false;
        if (!KernelHeap.TryAllocate(256UL, 16UL, true, out KernelHeapAllocation heapSample)) return false;
        if (!KernelConsole.Write("Kernel heap sample: ")) return false;
        if (!KernelConsole.WriteHex(heapSample.Address)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelHeap.TryRelease(heapSample)) return false;
        if (!KernelSmp.Initialize(boot)) return false;
        // Apply the IDE-authoritative CPU-role masks after processor discovery and before scheduler workers are created.
        if (!Inu.Kernel.Bootstrap.GeneratedConfiguration.ApplyCpuRoles()) return false;
        if (!KernelConsole.Write("SMP status: ")) return false;
        if (!KernelConsole.WriteLine(KernelSmp.GetLastStatusName())) return false;
        if (!KernelConsole.Write("Processors online/total: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelSmp.GetOnlineProcessorCount())) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(KernelSmp.GetProcessorCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Bootstrap processor index: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelSmp.GetBootstrapProcessorIndex())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("AP trampoline: ")) return false;
        if (!KernelConsole.WriteHex(KernelSmp.GetCapabilities().TrampolineAddress)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","SMP and per-CPU state online.")) return false;
        KernelTelemetry.KernelBootEvent("SMP / per-CPU", 9UL, KernelBootPhase.End);
        KernelTelemetry.KernelCounter("smp", "processors-online", KernelSmp.GetOnlineProcessorCount());
#if INU_COMPONENT_SCHEDULER_PRIORITY_POLICY
        if (!KernelPrioritySchedulingPolicy.Register()) return false;
#endif
#if INU_COMPONENT_SCHEDULER_LOAD_AWARE_PLACEMENT
        if (!KernelLoadAwarePlacementPolicy.Register()) return false;
#endif
        if (!KernelScheduler.Initialize()) return false;
        // Install managed interrupt dispatch before Scheduler.Run wakes application processors with the reschedule IPI.
        if (!KernelInterruptDispatch.Initialize()) return false;
        if (!Inu.Kernel.Scheduler.Scheduler.Run()) return false;

        // 0.0.77: all NativeAOT startup/static/frozen-root registration is complete by
        // this post-scheduler boundary. Seal the root map before the first real collection.
        // The collector must never infer readiness from allocation pressure or scheduler state.
        // 0.0.77: report the actual static/root registries immediately before sealing.
        // This is diagnostic-only and does not add, remove, or retain any root.
        NativeAotGarbageCollector.TraceRootRegistrationState(0x18BUL);
        NativeAotGarbageCollector.SealRootMap();
        if (!NativeAotGarbageCollector.IsRootMapReady())
        {
            KernelConsole.WriteLine("NOBT:FAIL:GC:ROOTMAP");
            return false;
        }
        KernelConsole.WriteLine("NOBT:GC:ROOTMAP:OK");

        // 0.0.77 mature tracing-GC gate. It runs only after all scheduler CPUs can
        // participate in the stop-the-world rendezvous and the root map is sealed.
        KernelConsole.WriteLine("NOBT:GC:RUN");
        if (!ManagedRuntimeConformance.RunGarbageCollectorChecks(out UInt32 gcPassed, out UInt32 gcFailed))
        {
            if (!KernelConsole.Write("GC conformance passed/failed: ")) return false;
            if (!KernelConsole.WriteUInt64(gcPassed)) return false;
            if (!KernelConsole.Write("/")) return false;
            if (!KernelConsole.WriteUInt64(gcFailed)) return false;
            if (!KernelConsole.WriteLine("")) return false;
            KernelStructuredLogging.CriticalLine("runtime","Kernel.KMain","Mature tracing-GC conformance rejected startup.");
            return false;
        }
        if (!KernelConsole.Write("GC conformance passed/failed: ")) return false;
        if (!KernelConsole.WriteUInt64(gcPassed)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(gcFailed)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        KernelConsole.WriteLine("NOBT:GC:OK");

        if (!KernelConsole.Write("Scheduler threads active: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelScheduler.GetActiveThreadCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Scheduler quantum: ")) return false;
        if (!KernelConsole.WriteDurationNanoseconds(KernelScheduler.GetQuantumNanoseconds())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Timer preemption: ")) return false;
        if (!KernelConsole.WriteLine(KernelScheduler.GetCapabilities().HasTimerPreemption ? "available" : "cooperative only")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Scheduler and threads online.")) return false;
        KernelTelemetry.KernelBootEvent("Scheduler", 10UL, KernelBootPhase.End);
        KernelTelemetry.KernelCounter("scheduler", "active-threads", KernelScheduler.GetActiveThreadCount());
        if (!KernelProtection.Initialize()) return false;
        KernelProtectionCapabilities protection = KernelProtection.GetCapabilities();
        if (!KernelConsole.Write("User range: ")) return false;
        if (!KernelConsole.WriteHex(protection.MinimumUserAddress)) return false;
        if (!KernelConsole.Write(" - ")) return false;
        if (!KernelConsole.WriteHex(protection.MaximumUserAddress)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Ring 3 selectors code/data: ")) return false;
        if (!KernelConsole.WriteHex(protection.UserCodeSelector)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(protection.UserDataSelector)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("Supervisor protections WP/SMEP/SMAP: ")) return false;
        if (!KernelConsole.Write(protection.WriteProtectEnabled ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(protection.SmepEnabled ? "on" : (protection.SmepSupported ? "available" : "unsupported"))) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteLine(protection.SmapSupported ? "available for syscall copy guards" : "unsupported")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","User/kernel separation online.")) return false;
        KernelTelemetry.KernelBootEvent("Protection", 11UL, KernelBootPhase.End);
        if (!KernelSecurity.Initialize()) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Process isolation/security policy online.")) return false;
        if (!KernelSystemCalls.Initialize()) return false;
        if (!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ConsoleFont, &GetFontPresetSyscall)) return false;
        if (!KernelSystemCalls.RegisterSet(KernelSystemCallMessages.ConsoleFont, &SetFontPresetSyscall)) return false;
        if (!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ConsoleBuffering, &GetBufferingPresetSyscall)) return false;
        if (!KernelSystemCalls.RegisterSet(KernelSystemCallMessages.ConsoleBuffering, &SetBufferingPresetSyscall)) return false;
        KernelSystemCallCapabilities systemCalls = KernelSystemCalls.GetCapabilities();
        if (!KernelConsole.Write("System calls: Get/Set/Event + Linux-style + NT-style; syscall stack: ")) return false;
        if (!KernelConsole.WriteByteSize(systemCalls.SyscallStackBytes)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("SMAP guarded user copies: ")) return false;
        if (!KernelConsole.WriteLine(systemCalls.SmapEnabled ? "enabled" : "not supported")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","System calls online.")) return false;
        KernelTelemetry.KernelBootEvent("System calls", 12UL, KernelBootPhase.End);
#if INU_COMPONENT_INPUT_KEYBOARD_DECODER
        if (!KernelKeyboardDecoder.Initialize()) return false;
#endif
        if (!KernelPs2.Initialize()) return false;
        if (!KernelPs2.SetKeyboardEventHandler(&HandleKeyboardEvent)) return false;
        if (!KernelPs2.SetMouseEventHandler(&HandlePs2MouseEvent)) return false;
        Ps2Capabilities ps2 = KernelPs2.GetCapabilities();
        if (!KernelConsole.Write("PS/2 i8042 keyboard/mouse: ")) return false;
        if (!KernelConsole.Write(ps2.Controller ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(ps2.Keyboard ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteLine(ps2.Mouse ? "on" : "off")) return false;
        if (!KernelConsole.Write("Keyboard layout: ")) return false;
        if (!KernelConsole.WriteLine(KeyboardLayouts.GetName(ps2.Layout))) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Keyboard layouts loaded: English_UK, English_USA.")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Keyboard repeat: software controlled; 300 ms delay, 40 ms interval; key-up cancels immediately.")) return false;
        if (!KernelProcesses.Initialize()) return false;
        if (!KernelGui.Initialize()) return false;
        KernelProcessCapabilities processes = KernelProcesses.GetCapabilities();
        if (!KernelConsole.Write("Processes ready/max: ")) return false;
        if (!KernelConsole.WriteUInt64(processes.ActiveProcessCount)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(processes.MaximumProcesses)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Executable loading: ELF64 + PE32+ x64; private user address spaces online.")) return false;
        if (!KernelTimerDispatch.Initialize()) return false;
        if (!KernelTimerDispatch.Register(1000000UL, &ServiceConsoleInput, 0UL, out _)) return false;
        if (!KernelTimerDispatch.Register(2000000UL, &ServiceNetworkAdapters, 0UL, out _)) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Interrupt and timer dispatch online; background polling disabled.")) return false;
        if (!KernelDrivers.Initialize()) return false;
#if INU_COMPONENT_PCI_LEGACY_CONFIGURATION
        if (!KernelPciLegacyConfigurationProvider.Register()) return false;
#endif
#if INU_COMPONENT_PCI_ECAM_CONFIGURATION
        if (!KernelPciEcamConfigurationProvider.Register()) return false;
#endif
        if (!KernelPci.Initialize()) return false;
#if INU_COMPONENT_USB_ENUMERATION
        if (!KernelUsbEnumeration.Initialize()) return false;
#endif
        if (!KernelXhci.Initialize()) return false;
        if (!KernelXhci.ScanRootPorts()) return false;
        XhciCapabilities xhci = KernelXhci.GetCapabilities();
        if (!KernelConsole.Write("xHCI controllers/running/connected root ports: ")) return false;
        if (!KernelConsole.WriteUInt64(xhci.Controllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(xhci.RunningControllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(xhci.ConnectedPorts)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#if INU_COMPONENT_INTERRUPTS_AFFINITY_RESOLVER
        if (!KernelInterruptAffinityResolver.Initialize()) return false;
#endif
#if INU_COMPONENT_INTERRUPTS_IO_APIC_ROUTER
        if (!KernelIoApicRouter.Initialize()) return false;
#endif
#if INU_COMPONENT_INTERRUPTS_PCI_MESSAGE_ROUTER
        if (!KernelPciMessageSignaledRouter.Initialize()) return false;
#endif
        if (!KernelInterruptBroker.Initialize()) return false;
        Boolean keyboardIrq=KernelInterruptBroker.RegisterLegacyGsi(1U,false,false,&HandlePs2Interrupt,0UL,out _);
        Boolean mouseIrq=!ps2.Mouse||KernelInterruptBroker.RegisterLegacyGsi(12U,false,false,&HandlePs2Interrupt,0UL,out _);
        Boolean ps2HardwareIrqs=keyboardIrq&&mouseIrq&&KernelPs2.SetHardwareInterrupts(true);
        if (!KernelConsole.WriteLine(ps2HardwareIrqs ? "PS/2 interrupt delivery: hardware IRQs active." : "PS/2 interrupt delivery: timer-service fallback active.")) return false;
        KernelInterruptBrokerCapabilities interruptBroker = KernelInterruptBroker.GetCapabilities();
        if (!KernelConsole.Write("Interrupt broker Local APIC / I/O APIC / x2APIC / MSI / MSI-X: ")) return false;
        if (!KernelConsole.Write(interruptBroker.LocalApic ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(interruptBroker.IoApic ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(interruptBroker.X2Apic ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(interruptBroker.Msi ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteLine(interruptBroker.MsiX ? "on" : "off")) return false;
        KernelDriverCapabilities drivers = KernelDrivers.GetCapabilities();
        PciCapabilities pci = KernelPci.GetCapabilities();
        if (!KernelConsole.Write("Driver framework drivers/devices: ")) return false;
        if (!KernelConsole.WriteUInt64(drivers.RegisteredDrivers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(drivers.RegisteredDevices)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("PCI/PCIe devices / ECAM segments: ")) return false;
        if (!KernelConsole.WriteUInt64(pci.DeviceCount)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(pci.EcamSegments)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","PCI configuration, BAR discovery, capabilities, MSI/MSI-X discovery and MMIO mapping online.")) return false;
        KernelTelemetry.KernelBootEvent("Drivers / PCI", 13UL, KernelBootPhase.End);
        KernelTelemetry.KernelTrace("driver", "pci-online", "PCI discovery and interrupt capabilities ready");
        if (!KernelConsole.WriteLine(drivers.RegistryMode == KernelDriverRegistryMode.Dynamic ? "Driver framework online; heap-backed registries grow dynamically." : "Driver framework online; fixed registry policy active.")) return false;
        if (!KernelStorage.Initialize()) return false;
#if INU_COMPONENT_STORAGE_PARTITION_DISCOVERY
        if (!KernelPartitionDiscovery.Initialize()) return false;
#endif
#if INU_COMPONENT_FILESYSTEM_FATFS
        if (!FatFs.Install()) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Selected FAT filesystem provider installed; the first compatible discovered volume will be mounted at the canonical VFS root.")) return false;
#else
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Filesystem providers: none selected by this OS configuration.")) return false;
#endif
        if (!UsbHub.Initialize()) return false;
        if (!UsbHub.EnumerateDownstream()) return false;
        if (!UsbHid.Initialize()) return false;
#if INU_COMPONENT_USB_HID_KEYBOARD
        if (!UsbHidKeyboard.Initialize()) return false;
        if (!UsbHid.SetKeyboardEventHandler(&HandleUsbKeyboardEvent)) return false;
#endif
#if INU_COMPONENT_USB_HID_MOUSE
        if (!UsbHidMouse.Initialize()) return false;
        if (!UsbHid.SetMouseEventHandler(&HandleUsbMouseEvent)) return false;
#endif
#if INU_COMPONENT_USB_MASS_STORAGE
        if (!UsbMassStorage.Initialize()) return false;
#endif
        UsbBusCapabilities usb = KernelUsbBus.GetCapabilities();
        UsbHubCapabilities usbHubs = UsbHub.GetCapabilities();
        UsbHidCapabilities usbHid = UsbHid.GetCapabilities();
        UInt32 usbStorageDevices = 0U;
#if INU_COMPONENT_USB_MASS_STORAGE
        usbStorageDevices = UsbMassStorage.GetCapabilities().Devices;
#endif
        if (!KernelConsole.Write("USB hosts/devices/interfaces: ")) return false;
        if (!KernelConsole.WriteUInt64(usb.Hosts)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(usb.Devices)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(usb.Interfaces)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.Write("USB hubs/ports, HID keyboards/mice, mass-storage devices: ")) return false;
        if (!KernelConsole.WriteUInt64(usbHubs.Hubs)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(usbHubs.Ports)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(usbHid.Keyboards)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(usbHid.Mice)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(usbStorageDevices)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#if INU_COMPONENT_STORAGE_NVME_DRIVER
        if (!KernelNvme.Initialize()) return false;
        NvmeCapabilities nvme = KernelNvme.GetCapabilities();
        if (!KernelConsole.Write("NVMe controllers/namespaces: ")) return false;
        if (!KernelConsole.WriteUInt64(nvme.Controllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(nvme.Namespaces)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
        if (!KernelAhci.Initialize()) return false;
        AhciCapabilities ahci = KernelAhci.GetCapabilities();
        if (!KernelConsole.Write("AHCI controllers/SATA disks: ")) return false;
        if (!KernelConsole.WriteUInt64(ahci.Controllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(ahci.Disks)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#endif
#if INU_COMPONENT_NETWORK_SOCKET_SERVICE
        if (!KernelSockets.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_DHCP_CODEC
        if (!KernelDhcpCodec.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_DNS_CODEC
        if (!KernelDnsCodec.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_ARP_PROTOCOL
        if (!KernelArpProtocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_IPV4_PROTOCOL
        if (!KernelIpv4Protocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_ICMPV4_PROTOCOL
        if (!KernelIcmpv4Protocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_UDP_IPV4_PROTOCOL
        if (!KernelUdpIpv4Protocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_TCP_OBSERVATION_PROTOCOL
        if (!KernelTcpObservationProtocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_IPV6_NDP_PROTOCOL
        if (!KernelIpv6NdpProtocol.Register()) return false;
#endif
        if (!KernelNetworking.Initialize()) return false;
#if INU_COMPONENT_NETWORK_ROUTE_MANAGER
        if (!KernelRouteManager.Initialize()) return false;
#endif
#if INU_COMPONENT_NETWORK_NEIGHBOR_CACHE
        if (!KernelNeighborCache.Initialize()) return false;
#endif
        if (!KernelVirtio.Initialize()) return false;
        if (!KernelVirtioGpu.Initialize()) return false;
        if (KernelVirtioGpu.GetCapabilities().Displays != 0U)
        {
            if (TryUsePreferredGraphicsDisplay())
            {
                FramebufferBufferCapabilities preferredBuffers = KernelConsole.GetFramebufferBufferCapabilities();
                if (preferredBuffers.AvailableBufferCount < 3U)
                {
                    UInt64 preferredBufferBytes = KernelConsole.GetFramebufferBufferByteCount();
                    if (preferredBufferBytes != 0UL && KernelHeap.TryAllocate(preferredBufferBytes, 4096UL, true, out KernelHeapAllocation preferredBackBufferA))
                    {
                        if (KernelHeap.TryAllocate(preferredBufferBytes, 4096UL, true, out KernelHeapAllocation preferredBackBufferB))
                        {
                            if (!KernelConsole.ConfigureFramebufferBuffers(preferredBackBufferA.Address, preferredBackBufferB.Address, preferredBufferBytes))
                            {
                                KernelHeap.TryRelease(preferredBackBufferA);
                                KernelHeap.TryRelease(preferredBackBufferB);
                            }
                        }
                        else KernelHeap.TryRelease(preferredBackBufferA);
                    }
                }
                if (!KernelStructuredLogging.InfoLine("graphics","Kernel.KMain","VirtIO-GPU backing populated and activated as the primary QEMU graphics target; firmware GOP remains registered as fallback metadata.")) return false;
            }
            else if (!KernelStructuredLogging.WarningLine("graphics","Kernel.KMain","VirtIO-GPU was detected but could not be promoted; retaining UEFI GOP as the active console framebuffer.")) return false;
        }
        else if (KernelVirtioGpu.GetDetectedPciDeviceCount()!=0U && !KernelStructuredLogging.WarningLine("graphics","Kernel.KMain","VirtIO-GPU PCI device was detected but the driver could not start a scan-out; retaining UEFI GOP. Use 'display' for detected/start-failure counts.")) return false;
        if (!KernelDrivers.BindAndStartMatchingDevices()) return false;
#if INU_COMPONENT_FILESYSTEM_FATFS
        if (!TryMountSelectedFatRoot()) return false;
#endif
#if INU_COMPONENT_NETWORK_E1000_DRIVER
        if (!KernelE1000.Initialize()) return false;
#endif
#if INU_COMPONENT_NETWORK_RTL8168_DRIVER
        if (!KernelRtl8168.Initialize()) return false;
#endif
        VirtioCapabilities virtio = KernelVirtio.GetCapabilities();
        if (!KernelConsole.Write("VirtIO devices block/net/console/rng: ")) return false;
        if (!KernelConsole.WriteUInt64(virtio.BlockDevices)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(virtio.NetworkDevices)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(virtio.Consoles)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(virtio.EntropySources)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        VirtioGpuCapabilities virtioGpu = KernelVirtioGpu.GetCapabilities();
        if (!KernelConsole.Write("VirtIO GPU controllers/displays: ")) return false;
        if (!KernelConsole.WriteUInt64(virtioGpu.Controllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(virtioGpu.Displays)) return false;
        if (!KernelConsole.WriteLine(" (2D resources + mode changes).")) return false;
        KernelGraphicsCapabilities graphics = KernelGraphics.GetCapabilities();
        if (!KernelConsole.Write("Graphics displays firmware/simple/VirtIO/total: ")) return false;
        if (!KernelConsole.WriteUInt64(graphics.FirmwareFramebuffers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(graphics.SimpleFramebuffers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(graphics.VirtioGpus)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(graphics.Displays)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#if INU_COMPONENT_NETWORK_E1000_DRIVER
        E1000Capabilities e1000 = KernelE1000.GetCapabilities();
        if (!KernelConsole.Write("Intel E1000/E1000e controllers/interfaces: ")) return false;
        if (!KernelConsole.WriteUInt64(e1000.Controllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(e1000.Interfaces)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#endif
#if INU_COMPONENT_NETWORK_RTL8168_DRIVER
        Rtl8168Capabilities rtl8168 = KernelRtl8168.GetCapabilities();
        if (!KernelConsole.Write("Realtek RTL8168/RTL8111 controllers/interfaces: ")) return false;
        if (!KernelConsole.WriteUInt64(rtl8168.Controllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(rtl8168.Interfaces)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#endif
        KernelStorageCapabilities storage = KernelStorage.GetCapabilities();
        if (!KernelConsole.Write("Storage devices/volumes/mounts: ")) return false;
        if (!KernelConsole.WriteUInt64(storage.Devices)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(storage.Volumes)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(storage.Mounts)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Storage/VFS online; MBR + GPT discovery, FAT32 and VirtIO block ready.")) return false;
        KernelTelemetry.KernelBootEvent("Storage / VFS", 14UL, KernelBootPhase.End);
        KernelTelemetry.KernelTrace("storage", "storage-online");
        KernelNetworkCapabilities networking = KernelNetworking.GetCapabilities();
        if (!KernelConsole.Write("Networking interfaces/routes/sockets: ")) return false;
        if (!KernelConsole.WriteUInt64(networking.Interfaces)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(networking.Routes)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(networking.Sockets)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Networking online; Ethernet + ARP + IPv4 + ICMP + UDP + TCP with VirtIO-net, Intel E1000/E1000e and Realtek RTL8168/RTL8111 ready.")) return false;
        KernelTelemetry.KernelBootEvent("Networking", 15UL, KernelBootPhase.End);
        KernelTelemetry.KernelTrace("network", "network-stack-online");
        if (!KernelSubsystemRuntime.ValidateAll(out UInt32 readySubsystems,out UInt32 degradedSubsystems)) return false;
        if (!KernelConsole.Write("Formal subsystem contracts 1.0 active; ready/degraded: ")) return false;
        if (!KernelConsole.WriteUInt64(readySubsystems)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(degradedSubsystems)) return false;
        if (!KernelConsole.WriteLine(". Kernel runtime is gated by the public subsystem boundaries.")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Capability policy active: driver declarations are privilege ceilings; bound devices run only with kernel-issued grants.")) return false;
        if (!KernelStructuredLogging.InfoLine("kernel","Kernel.KMain","Interactive console ready. Defaults: font 3, buffering auto (double for text).")) return false;
        KernelTelemetry.KernelBootEvent("Interactive console", 16UL, KernelBootPhase.End);
        KernelTelemetry.KernelProfile("boot", "KMain-postheap", 1UL, KernelTime.GetMonotonicNanoseconds());
        if (!Startup.UserlandRuntimeStartup.Initialize(boot)) return false;
        if (!global::Inu.Kernel.InterruptDispatch.Interrupts.Run()) return false;
        // Select the graphical or text session only after the ordinary user-session
        // environment has reached the same point used by the interactive CLI.
        Boolean sessionReady=true;
        // At the user-session boundary the text console relinquishes only framebuffer presentation.
        // Its selected single/double/triple buffer policy and retained state remain untouched, and serial
        // diagnostics continue normally. This prevents stale console back buffers/caret updates from
        // racing the compositor after GUI takeover.
        Boolean consoleFramebufferReleased=!sessionReady||!KernelConsole.HasFramebuffer()||KernelConsole.SetFramebufferPresentationEnabled(false);
        if(sessionReady&&consoleFramebufferReleased&&KernelProcesses.BeginGraphicalSessionAsync())
        {
            if (!KernelStructuredLogging.InfoLine("gui","Kernel.KMain","Graphical session startup queued asynchronously on the OS-selected GUI CPU set.")) return false;
            while(KernelProcesses.IsGraphicalSessionStarting()) if (!Native.WaitForInterrupt()) return false;
            if(KernelProcesses.IsGraphicalSessionActive())
            {
                if (!KernelStructuredLogging.InfoLine("gui","Kernel.KMain","Userland desktop and graphical login applications are active as isolated ring-3 processes.")) return false;
                for (;;) if (!Native.WaitForInterrupt()) return false;
            }
        }
        if(consoleFramebufferReleased&&KernelConsole.HasFramebuffer()&&!KernelConsole.SetFramebufferPresentationEnabled(true))return false;
        if (!KernelStructuredLogging.WarningLine("gui","Kernel.KMain",sessionReady?"Graphical session autostart was unavailable; retaining the text recovery login.":"User-session filesystem preparation timed out; retaining the text recovery login.")) return false;
        return Startup.UserlandRuntimeStartup.RunShellSession();
    }
    
    
    
    

    
    
    
    
    // Decoded PS/2 events are consumed here; KernelConsole never rereads i8042 hardware.
    
    
    
    
    
    
    
    
    
    
    
    
    

    

    

    

    

    
    
    
    
    
    
    
    
    
    

}
