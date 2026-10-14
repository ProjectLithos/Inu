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

/// <summary>Starts selected storage drivers and provisions blank selected data drives inside the kernel.</summary>
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
        for (UInt32 index=0U; KernelNvme.TryGetNamespace(index,out NvmeNamespaceInfo info); index++)
        {
            if (!KernelDiskProvisioning.EnsureTwoGptPartitions(info.Storage,out Boolean created))
            {
                KernelConsole.WriteHostControl("NVME_PARTITION_FAIL");
                KernelStructuredLogging.ErrorLine("storage","StorageHardwareStartup.Initialize","Selected NVMe disk could not be validated or provisioned with the requested two-partition GPT layout.");
                return false;
            }
            if (created) KernelStructuredLogging.InfoLine("storage","StorageHardwareStartup.Initialize","Kernel created the requested two-partition GPT layout on a blank NVMe disk.");
        }
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
        if (!KernelAhci.Initialize()) return false;
        for (UInt32 index=0U; KernelAhci.TryGetDisk(index,out AhciDiskInfo info); index++)
        {
            if (!KernelDiskProvisioning.EnsureTwoGptPartitions(info.Storage,out Boolean created))
            {
                KernelConsole.WriteHostControl("AHCI_PARTITION_FAIL");
                KernelStructuredLogging.ErrorLine("storage","StorageHardwareStartup.Initialize","Selected AHCI disk could not be validated or provisioned with the requested two-partition GPT layout.");
                return false;
            }
            if (created) KernelStructuredLogging.InfoLine("storage","StorageHardwareStartup.Initialize","Kernel created the requested two-partition GPT layout on a blank AHCI disk.");
        }
#endif
        if (!KernelStructuredLogging.InfoLine("storage","StorageHardwareStartup.Initialize","Selected kernel-domain storage services online.")) return false;
#endif
        return true;
    }
}
