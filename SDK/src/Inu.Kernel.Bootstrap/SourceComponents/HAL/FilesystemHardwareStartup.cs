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
    private static Boolean TryMountRoot()
    {
        for (UInt32 ordinal = 0U; KernelStorage.TryGetVolumeByOrdinal(ordinal, out KernelStorageVolumeHandle volume); ordinal++)
        {
#if INU_COMPONENT_FILESYSTEM_FATFS
            if (KernelVfs.Mount(KernelVfs.DefaultNamespace, volume, KernelFileSystemType.Fat32, "/", out _)) return true;
            if (KernelVfs.Mount(KernelVfs.DefaultNamespace, volume, KernelFileSystemType.Fat16, "/", out _)) return true;
            if (KernelVfs.Mount(KernelVfs.DefaultNamespace, volume, KernelFileSystemType.Fat12, "/", out _)) return true;
#endif
        }
        return false;
    }
#endif
}
