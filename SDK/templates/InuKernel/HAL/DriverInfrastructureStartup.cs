using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Console;
using Inu.Kernel.Drivers;
using Inu.Kernel.Pci;
#if INU_KERNELAREA_DRIVERS
using Inu.Kernel.InterruptBroker;
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Initializes the selected device framework, PCI transports and interrupt-routing infrastructure.</summary>
public static class DriverInfrastructureStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_DRIVERS
        if (!KernelDrivers.Initialize()) return false;
#if INU_COMPONENT_PCI_LEGACY_CONFIGURATION
        if (!KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.LegacyIo) && !KernelPciLegacyConfigurationProvider.Register()) return false;
#endif
#if INU_COMPONENT_PCI_ECAM_CONFIGURATION
        if (!KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.PcieEcam) && !KernelPciEcamConfigurationProvider.Register()) return false;
#endif
        if (!KernelPci.Initialize()) return false;
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

        KernelDriverCapabilities drivers = KernelDrivers.GetCapabilities();
        PciCapabilities pci = KernelPci.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"hal-detail","DriverInfrastructureStartup.Initialize")) return false;
        if (!KernelConsole.Write("Kernel driver framework drivers/devices: ")) return false;
        if (!KernelConsole.WriteUInt64(drivers.RegisteredDrivers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(drivers.RegisteredDevices)) return false;
        if (!KernelConsole.Write("; PCI devices: ")) return false;
        if (!KernelConsole.WriteUInt64(pci.DeviceCount)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#endif
        return true;
    }
}
