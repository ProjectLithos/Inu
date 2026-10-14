using System;
using Inu.Kernel.Console;
#if INU_KERNELAREA_FILESYSTEMS
using Inu.Kernel.Storage;
#if INU_COMPONENT_FILESYSTEM_FATFS
using Inu.Filesystem.FatFs;
#endif
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Installs and mounts only filesystem providers selected into the kernel execution domain.</summary>
public static class FilesystemHardwareStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_FILESYSTEMS
        if (!KernelStorage.IsInitialized())
        {
            KernelConsole.WriteHostControl("FILESYSTEM_STORAGE_UNAVAILABLE");
            KernelStructuredLogging.ErrorLine("filesystem","FilesystemHardwareStartup.Initialize","Kernel-domain filesystem selected without initialized kernel-domain storage.");
            return false;
        }
#if INU_COMPONENT_FILESYSTEM_FATFS
        if (!FatFs.Install())
        {
            KernelConsole.WriteHostControl("FILESYSTEM_PROVIDER_FAIL");
            KernelStructuredLogging.ErrorLine("filesystem","FilesystemHardwareStartup.Initialize","The selected FAT filesystem provider could not be installed.");
            return false;
        }
        if (!FormatNewlyProvisionedDataVolumes())
        {
            KernelConsole.WriteHostControl("FILESYSTEM_FORMAT_FAIL");
            KernelStructuredLogging.ErrorLine("filesystem","FilesystemHardwareStartup.Initialize","A data partition created by the kernel could not be formatted with the selected FAT32 filesystem.");
            return false;
        }
#endif
        if (KernelVfs.MountCount == 0U && !TryMountRoot())
        {
            KernelConsole.WriteHostControl("FILESYSTEM_MOUNT_FAIL");
            KernelStructuredLogging.ErrorLine("filesystem","FilesystemHardwareStartup.Initialize","No selected filesystem could be mounted as the root filesystem.");
            return false;
        }
        if (!KernelStructuredLogging.InfoLine("filesystem","FilesystemHardwareStartup.Initialize","Selected kernel-domain filesystem provider is mounted and ready.")) return false;
#endif
        return true;
    }

#if INU_KERNELAREA_FILESYSTEMS
    private const UInt64 BasicDataGuidLow = 0x4433B9E5EBD0A0A2UL;
    private const UInt64 BasicDataGuidHigh = 0xC79926B7B668C087UL;

#if INU_COMPONENT_FILESYSTEM_FATFS
    private static Boolean FormatNewlyProvisionedDataVolumes()
    {
        for (UInt32 ordinal=0U; KernelStorage.TryGetVolumeByOrdinal(ordinal,out KernelStorageVolumeHandle volume); ordinal++)
        {
            if (!KernelStorage.TryGetVolume(volume,out KernelStorageDeviceHandle device,out KernelPartitionInfo partition) ||
                partition.Scheme!=KernelPartitionScheme.Gpt || partition.TypeGuidLow!=BasicDataGuidLow || partition.TypeGuidHigh!=BasicDataGuidHigh ||
                !KernelDiskProvisioning.WasProvisionedThisBoot(device)) continue;
            Boolean seedDirectories=partition.Index==0U;
            if (!FatFs.EnsureFat32Formatted(volume,seedDirectories,out Boolean formatted)) return false;
            if (formatted && !KernelStructuredLogging.InfoLine("filesystem","FilesystemHardwareStartup.Initialize",seedDirectories
                ? "Kernel formatted data partition 1 as FAT32 and created USERS, WORK and TEMP directories."
                : "Kernel formatted data partition 2 as FAT32.")) return false;
        }
        return true;
    }
#endif

    private static Boolean TryMountRoot()
    {
        // Prefer an ordinary GPT data partition over the UEFI system/boot partition.
        // QEMU data disks produced by Inu.ImageBuilder use the Microsoft Basic Data GUID.
        for (UInt32 ordinal = 0U; KernelStorage.TryGetVolumeByOrdinal(ordinal, out KernelStorageVolumeHandle volume); ordinal++)
        {
            if (!KernelStorage.TryGetVolume(volume, out _, out KernelPartitionInfo partition) ||
                partition.Scheme != KernelPartitionScheme.Gpt ||
                partition.TypeGuidLow != BasicDataGuidLow || partition.TypeGuidHigh != BasicDataGuidHigh) continue;
            if (TryMount(volume, "/"))
            {
                MountAdditionalDataVolumes(ordinal + 1U);
                return true;
            }
        }

        // Compatibility fallback: a monolithic OS with no separate data drive may still
        // mount its FAT boot/system partition writable.
        for (UInt32 ordinal = 0U; KernelStorage.TryGetVolumeByOrdinal(ordinal, out KernelStorageVolumeHandle volume); ordinal++)
            if (TryMount(volume, "/")) return true;
        return false;
    }

    private static void MountAdditionalDataVolumes(UInt32 startOrdinal)
    {
        UInt32 mountNumber = 2U;
        for (UInt32 ordinal = startOrdinal; KernelStorage.TryGetVolumeByOrdinal(ordinal, out KernelStorageVolumeHandle volume); ordinal++)
        {
            if (!KernelStorage.TryGetVolume(volume, out _, out KernelPartitionInfo partition) ||
                partition.Scheme != KernelPartitionScheme.Gpt ||
                partition.TypeGuidLow != BasicDataGuidLow || partition.TypeGuidHigh != BasicDataGuidHigh) continue;
            String path = mountNumber == 2U ? "/Partition2" : "/Partition" + mountNumber.ToString();
            if (TryMount(volume, path)) mountNumber++;
        }
    }

    private static Boolean TryMount(KernelStorageVolumeHandle volume, String path)
    {
#if INU_COMPONENT_FILESYSTEM_FATFS
        if (KernelVfs.Mount(KernelVfs.DefaultNamespace, volume, KernelFileSystemType.Fat32, path, out _)) return true;
        if (KernelVfs.Mount(KernelVfs.DefaultNamespace, volume, KernelFileSystemType.Fat16, path, out _)) return true;
        if (KernelVfs.Mount(KernelVfs.DefaultNamespace, volume, KernelFileSystemType.Fat12, path, out _)) return true;
#endif
        return false;
    }
#endif
}
