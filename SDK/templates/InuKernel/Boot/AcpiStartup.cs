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

/// <summary>Initializes the selected ACPI root and only the ACPI providers compiled into this OS.</summary>
public static unsafe class AcpiStartup
{
    public static Boolean Initialize<TBoot>(TBoot boot)
        where TBoot : IAcpiRootPointerContext
    {
        if (!KernelAcpi.Initialize(boot)) { KernelConsole.WriteLine("NOBT:FAIL:ACPI"); return false; }
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
        KernelConsole.WriteLine("NOBT:ACPI");
        if (!KernelAcpiNmi.InitializeBootstrapProcessor()) { KernelConsole.WriteLine("NOBT:FAIL:NMI"); return false; }
        KernelConsole.WriteLine("NOBT:NMI");
        AcpiNmiDiagnostics nmiDiagnostics = KernelAcpiNmi.GetDiagnostics();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("MADT NMI sources/local/applied: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetNmiSourceCount())) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetLocalNmiCount())) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpiNmi.GetAppliedLocalNmiCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Firmware NMI LVT LINT0/LINT1/thermal/perf: ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialLint0)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialLint1)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialThermal)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialPerformance)) return false;
        if (!KernelConsole.WriteLine(nmiDiagnostics.FirmwareNmiArmed ? " (firmware NMI route was armed; Inu replaced it)" : " (no unmasked firmware NMI LVT)")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("NMI observations / legacy port61 / gate: ")) return false;
        if (!KernelConsole.WriteUInt64(nmiDiagnostics.Count)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(nmiDiagnostics.InitialPort61)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(KernelAcpiNmi.IsLegacyNmiGateUnmasked() ? "unmasked" : "quarantined")) return false;
        if (!KernelConsole.WriteLine(nmiDiagnostics.LegacyParityOrChannelCheckAsserted ? " (parity/channel-check asserted)" : "")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("ACPI status: ")) return false;
        if (!KernelConsole.WriteLine(KernelAcpi.GetLastStatusName())) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("ACPI RSDP: ")) return false;
        if (!KernelConsole.WriteHex(KernelAcpi.GetRootPointerAddress())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("ACPI root table: ")) return false;
        if (!KernelConsole.WriteHex(KernelAcpi.GetRootTableAddress())) return false;
        if (!KernelConsole.WriteLine(KernelAcpi.UsesXsdt() ? " XSDT" : " RSDT")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("ACPI processors: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetProcessorCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("ACPI I/O APICs: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelAcpi.GetIoApicCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (KernelAcpi.TryGetLocalApicAddress(out UInt64 localApicAddress))
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("Local APIC base: ")) return false;
            if (!KernelConsole.WriteHex(localApicAddress)) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
        if (KernelAcpi.TryGetPciEcam(0U, out AcpiPciEcamInfo ecam))
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("PCI ECAM base: ")) return false;
            if (!KernelConsole.WriteHex(ecam.BaseAddress)) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
        if (KernelAcpi.TryGetHpet(out AcpiHpetInfo hpet))
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("HPET base: ")) return false;
            if (!KernelConsole.WriteHex(hpet.BaseAddress)) return false;
            if (!KernelConsole.WriteLine("")) return false;
        }
#if INU_COMPONENT_ACPI_FADT
        if (!KernelAcpiFadtServices.Initialize()) { KernelConsole.WriteLine("NOBT:FAIL:FADT"); return false; }
        KernelConsole.WriteLine("NOBT:FADT");
#endif
#if INU_COMPONENT_ACPI_POWER
        if (!KernelPowerManagement.Initialize()) { KernelConsole.WriteLine("NOBT:FAIL:POWER"); return false; }
        KernelConsole.WriteLine("NOBT:POWER");
#endif
        Boolean ecReady = false;
#if INU_COMPONENT_ACPI_EMBEDDED_CONTROLLER
        ecReady = KernelAcpiEcServices.Initialize();
#endif
        AcpiPowerCapabilities power = KernelAcpiPowerServices.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("ACPI FADT power reset/shutdown/button: ")) return false;
        if (!KernelConsole.Write(power.ResetAvailable ? "yes" : "no")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(power.ShutdownAvailable ? "yes" : "no")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteLine(power.PowerButtonAvailable ? "yes" : "no")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("ACPI embedded controller: ")) return false;
        if (!KernelConsole.WriteLine(ecReady ? "ECDT online" : "not advertised by ECDT")) return false;
        if (!KernelStructuredLogging.InfoLine("acpi","BootStartup.Initialize","Selected ACPI platform services initialized.")) return false;
        return true;
    }
}
