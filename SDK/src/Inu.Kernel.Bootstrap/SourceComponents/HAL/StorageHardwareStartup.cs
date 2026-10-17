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


    private static Boolean HasVolumeForDevice(KernelStorageDeviceHandle device)
    {
#if INU_KERNELAREA_STORAGE
        UInt32 volumeCount = KernelStorage.GetCapabilities().Volumes;
        for (UInt32 i = 0U; i < volumeCount; i++)
        {
            if (!KernelStorage.TryGetVolumeByOrdinal(i, out KernelStorageVolumeHandle volume)) continue;
            if (!KernelStorage.TryGetVolume(volume, out KernelStorageDeviceHandle owner, out _)) continue;
            if (owner.Value == device.Value) return true;
        }
#endif
        return false;
    }

    private static Boolean RediscoverMissingVolumes()
    {
#if INU_KERNELAREA_STORAGE && INU_COMPONENT_STORAGE_PARTITION_DISCOVERY
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_POST_DISCOVERY_ENTER")) return false;
        UInt32 driveCount = KernelStorage.GetCapabilities().Devices;
        UInt32 retried = 0U;
        UInt32 recovered = 0U;
        UInt32 failed = 0U;
        for (UInt32 i = 0U; i < driveCount; i++)
        {
            if (!KernelStorage.TryGetDeviceByOrdinal(i, out KernelStorageDeviceHandle drive, out KernelStorageDeviceKind kind, out KernelStorageGeometry geometry)) continue;
            if (HasVolumeForDevice(drive)) continue;
            retried++;
            if (!DiagnosticCount("Partition rediscovery drive ordinal: ", i)) return false;
            if (!DiagnosticCount("Partition rediscovery device kind: ", (UInt64)kind)) return false;
            if (!DiagnosticCount("Partition rediscovery logical block size: ", geometry.LogicalBlockSize)) return false;
            if (KernelStorage.RefreshPartitions(drive))
            {
                recovered++;
                if (!KernelStructuredLogging.InfoLine("storage-detail", "StorageHardwareStartup.Initialize", "Post-driver partition rediscovery registered at least one volume for this drive.")) return false;
            }
            else
            {
                failed++;
                KernelStructuredLogging.WarningLine("storage", "StorageHardwareStartup.Initialize", "Post-driver partition rediscovery could not register a volume for this drive; the OS policy may not be able to mount it.");
            }
        }
        if (!DiagnosticCount("Partition rediscovery drives retried: ", retried)) return false;
        if (!DiagnosticCount("Partition rediscovery drives recovered: ", recovered)) return false;
        if (!DiagnosticCount("Partition rediscovery drives still without volumes: ", failed)) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_POST_DISCOVERY_OK")) return false;
#endif
        return true;
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
        if (!KernelAhci.Initialize())
        {
            KernelStructuredLogging.ErrorLine("storage","StorageHardwareStartup.Initialize","AHCI initialization returned failure after its bounded controller/port/IDENTIFY waits.");
            KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE_AHCI_FAILED");
            return false;
        }
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
        if (!RediscoverMissingVolumes()) return false;
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
