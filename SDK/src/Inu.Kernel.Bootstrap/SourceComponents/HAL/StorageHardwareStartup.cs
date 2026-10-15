using System;
using Inu.Kernel.Console;
#if INU_KERNELAREA_STORAGE
using Inu.Kernel.Storage;
#if INU_COMPONENT_STORAGE_VIRTIO_BLOCK_DRIVER
using Inu.Kernel.Virtio;
#endif
#if INU_COMPONENT_STORAGE_NVME_DRIVER
using Inu.Kernel.Nvme;
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
using Inu.Kernel.Ahci;
#endif
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Starts selected block-device drivers. Disk layout policy belongs to the coder-owned Kernel.cs.</summary>
public static class StorageHardwareStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_STORAGE
        if (!KernelStorage.Initialize()) return false;
#if INU_COMPONENT_STORAGE_PARTITION_DISCOVERY
        if (!KernelPartitionDiscovery.Initialize()) return false;
#endif
#if INU_COMPONENT_STORAGE_VIRTIO_BLOCK_DRIVER
        if (!KernelVirtio.Initialize()) return false;
#endif
#if INU_COMPONENT_STORAGE_NVME_DRIVER
        if (!KernelNvme.Initialize()) return false;
        if (KernelNvme.GetCapabilities().Namespaces == 0U)
        {
            if (!KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","NVMe was selected, but no usable NVMe namespace was registered.")) return false;
        }
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
        if (!KernelAhci.Initialize()) return false;
        AhciCapabilities ahci = KernelAhci.GetCapabilities();
        if (ahci.Controllers == 0U)
        {
            if (!KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","AHCI was selected, but no AHCI PCI controller could be started.")) return false;
        }
        else if (ahci.Disks == 0U)
        {
            if (!KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","AHCI controller started, but no SATA disk completed discovery and IDENTIFY.")) return false;
        }
#endif
        if (KernelStorage.GetCapabilities().Devices == 0U)
        {
            if (!KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","No block drives were registered. The OS-owned Kernel.cs will see an empty drive list.")) return false;
        }
        else if (!KernelStructuredLogging.InfoLine("storage","StorageHardwareStartup.Initialize","Detected block drives are registered; partition/format/mount policy is delegated to the OS-owned Kernel.cs.")) return false;
#endif
        return true;
    }
}
