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
    private static Boolean DiagnosticCount(String label, UInt64 value)
    {
        if (!KernelStructuredLogging.Begin(Inu.Kernel.Contracts.KernelLogLevel.Info, "storage-detail", "StorageHardwareStartup.Initialize")) return false;
        if (!KernelConsole.Write(label)) return false;
        if (!KernelConsole.WriteUInt64(value)) return false;
        return KernelConsole.WriteLine(String.Empty);
    }

    public static Boolean Initialize()
    {
#if INU_KERNELAREA_STORAGE
        if (!KernelStructuredLogging.InfoLine("storage", "StorageHardwareStartup.Initialize", "Storage startup entered; every selected storage subsystem will publish ENTER/OK markers and capability counts.")) return false;
#if INU_COMPONENT_STORAGE_PARTITION_DISCOVERY
        if (!KernelStructuredLogging.InfoLine("storage-detail", "StorageHardwareStartup.Initialize", "Partition discovery selected.")) return false;
#endif
#if INU_COMPONENT_STORAGE_VIRTIO_BLOCK_DRIVER
        if (!KernelStructuredLogging.InfoLine("storage-detail", "StorageHardwareStartup.Initialize", "VirtIO block selected.")) return false;
#endif
#if INU_COMPONENT_STORAGE_NVME_DRIVER
        if (!KernelStructuredLogging.InfoLine("storage-detail", "StorageHardwareStartup.Initialize", "NVMe selected.")) return false;
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
        if (!KernelStructuredLogging.InfoLine("storage-detail", "StorageHardwareStartup.Initialize", "AHCI selected.")) return false;
#endif
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_CORE_ENTER")) return false;
        if (!KernelStorage.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_CORE_OK")) return false;
        KernelStorageCapabilities storageCore = KernelStorage.GetCapabilities();
        if (!DiagnosticCount("Storage registry device capacity: ", storageCore.DeviceCapacity)) return false;
        if (!DiagnosticCount("Storage registry volume capacity: ", storageCore.VolumeCapacity)) return false;
#if INU_COMPONENT_STORAGE_PARTITION_DISCOVERY
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_PARTITIONS_ENTER")) return false;
        if (!KernelPartitionDiscovery.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_PARTITIONS_OK")) return false;
#endif
#if INU_COMPONENT_STORAGE_VIRTIO_BLOCK_DRIVER
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_VIRTIO_ENTER")) return false;
        if (!KernelVirtio.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_VIRTIO_OK")) return false;
        VirtioCapabilities virtio = KernelVirtio.GetCapabilities();
        if (!DiagnosticCount("VirtIO devices started: ", virtio.Devices)) return false;
        if (!DiagnosticCount("VirtIO block devices started: ", virtio.BlockDevices)) return false;
#endif
#if INU_COMPONENT_STORAGE_NVME_DRIVER
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_NVME_ENTER")) return false;
        if (!KernelNvme.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_NVME_OK")) return false;
        NvmeCapabilities nvme = KernelNvme.GetCapabilities();
        if (!DiagnosticCount("NVMe controllers started: ", nvme.Controllers)) return false;
        if (!DiagnosticCount("NVMe namespaces registered: ", nvme.Namespaces)) return false;
        if (nvme.Namespaces == 0U)
        {
            KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","NVMe was selected, but no usable NVMe namespace was registered.");
        }
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_AHCI_ENTER")) return false;
        if (!KernelAhci.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_AHCI_OK")) return false;
        AhciCapabilities ahci = KernelAhci.GetCapabilities();
        if (!DiagnosticCount("AHCI controllers started: ", ahci.Controllers)) return false;
        if (!DiagnosticCount("AHCI disks registered: ", ahci.Disks)) return false;
        if (ahci.Controllers == 0U)
        {
            KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","AHCI was selected, but no AHCI PCI controller could be started.");
        }
        else if (ahci.Disks == 0U)
        {
            KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","AHCI controller started, but no SATA disk completed discovery and IDENTIFY.");
        }
#endif
        if (KernelStorage.GetCapabilities().Devices == 0U)
        {
            KernelStructuredLogging.WarningLine("storage","StorageHardwareStartup.Initialize","No block drives were registered. The OS-owned Kernel.cs will see an empty drive list.");
        }
        else if (!KernelStructuredLogging.InfoLine("storage","StorageHardwareStartup.Initialize","Detected block drives are registered; partition/format/mount policy is delegated to the OS-owned Kernel.cs.")) return false;
        KernelStorageCapabilities finalStorage = KernelStorage.GetCapabilities();
        if (!DiagnosticCount("Final block devices registered: ", finalStorage.Devices)) return false;
        if (!DiagnosticCount("Final storage volumes registered: ", finalStorage.Volumes)) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_OK")) return false;
#endif
        return true;
    }
}
