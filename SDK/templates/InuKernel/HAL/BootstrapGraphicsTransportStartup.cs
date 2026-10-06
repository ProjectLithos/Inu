using System;
using Inu.Kernel.Console;
using Inu.Kernel.Drivers;
using Inu.Kernel.Pci;
using Inu.Kernel.Virtio.Gpu;

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Starts only the graphics transport explicitly selected for use before general driver startup.</summary>
public static class BootstrapGraphicsTransportStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNEL_MICROKERNEL
        if (!KernelDrivers.Initialize()) { KernelConsole.WriteHostControl("BOOTSTRAP_GRAPHICS_DRIVERS_FAIL"); return false; }
#if INU_COMPONENT_PCI_LEGACY_CONFIGURATION
        if (!KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.LegacyIo) && !KernelPciLegacyConfigurationProvider.Register()) return false;
#endif
#if INU_COMPONENT_PCI_ECAM_CONFIGURATION
        if (!KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.PcieEcam) && !KernelPciEcamConfigurationProvider.Register()) return false;
#endif
        if (!KernelPci.Initialize()) { KernelConsole.WriteHostControl("BOOTSTRAP_GRAPHICS_PCI_FAIL"); return false; }
        if (!KernelVirtioGpu.Initialize()) { KernelConsole.WriteHostControl("BOOTSTRAP_GRAPHICS_VIRTIO_FAIL"); return false; }
        VirtioGpuCapabilities virtioGpu = KernelVirtioGpu.GetCapabilities();
        if (virtioGpu.Displays != 0U)
        {
            if (!KernelStructuredLogging.InfoLine("graphics","BootstrapGraphicsTransportStartup.Initialize","VirtIO-GPU detected and started; the existing bootstrap framebuffer remains active until an explicit graphics transition.")) return false;
        }
        else if (KernelVirtioGpu.GetDetectedPciDeviceCount() != 0U)
        {
            if (!KernelStructuredLogging.WarningLine("graphics","BootstrapGraphicsTransportStartup.Initialize","VirtIO-GPU PCI function detected but bind/start failed; retaining the existing bootstrap framebuffer.")) return false;
        }
#endif
        return true;
    }
}
