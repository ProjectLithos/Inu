using System;
using Inu.Kernel.Console;
#if INU_KERNELAREA_FILESYSTEMS
using Inu.Kernel.Storage;
#if INU_COMPONENT_FILESYSTEM_FATFS
using Inu.Filesystem.FatFs;
#endif
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Installs selected filesystem providers. Formatting and mount policy belongs to the coder-owned Kernel.cs.</summary>
public static class FilesystemHardwareStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_FILESYSTEMS
        if (!KernelStorage.IsInitialized())
        {
            KernelConsole.WriteHostControl("FILESYSTEM_STORAGE_UNAVAILABLE");
            KernelStructuredLogging.ErrorLine("filesystem","FilesystemHardwareStartup.Initialize","Filesystem provider selected without initialized storage.");
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
        if (!KernelStructuredLogging.InfoLine("filesystem","FilesystemHardwareStartup.Initialize","Selected filesystem providers are installed; Kernel.cs owns format and mount policy.")) return false;
#endif
        return true;
    }
}
