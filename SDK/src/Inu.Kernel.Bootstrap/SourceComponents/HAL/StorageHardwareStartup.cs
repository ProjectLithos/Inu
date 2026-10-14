using System;
using Inu.Kernel.Console;
#if INU_KERNELAREA_STORAGE
using Inu.Kernel.Storage;
#if INU_COMPONENT_STORAGE_NVME_DRIVER
using Inu.Kernel.Nvme;
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
using Inu.Kernel.Ahci;
#endif
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Starts only storage services and drivers selected into the kernel execution domain.</summary>
public static class StorageHardwareStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_STORAGE
        if (!KernelStorage.Initialize()) return false;
#if INU_COMPONENT_STORAGE_NVME_DRIVER
        if (!KernelNvme.Initialize()) return false;
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
        if (!KernelAhci.Initialize()) return false;
#endif
        if (!KernelStructuredLogging.InfoLine("storage","StorageHardwareStartup.Initialize","Selected kernel-domain storage services online.")) return false;
#endif
        return true;
    }
}
